namespace FsShelter.Cluster

// Supervisor: glue between Membership, Election, Transport, and Hosting.
//
// The pure `Supervisor.step` function encodes the master/standby state
// machine; `Supervisor.runCluster` is the impure entry point that wires
// together the transport, periodic heartbeats, election recompute, and
// (on the elected master only) the existing single-process `Hosting`
// runtime. When `CLUSTER_SEEDS` is empty, `runCluster` falls through to
// `Hosting.runWith` so single-process behaviour is unchanged.

open System
open System.Threading
open FsShelter
open FsShelter.Topology

/// Lifecycle phase of the local peer with respect to the topology.
type SupervisorState =
    /// Not the elected master — only gossip/transport machinery is running.
    | Standby
    /// Just won the election; debouncing for `StabilizeMs` before activating
    /// the topology. `since` is the monotonic-ms timestamp of the election win.
    | Activating of since: int64
    /// Master and topology is running.
    | Master
    /// Shutdown requested; no further transitions.
    | Stopping

/// External inputs the orchestrator feeds into the pure step function.
type SupervisorInput =
    /// Latest election decision after a membership change or tick.
    | ElectionUpdate of selfIsMaster: bool * nowMs: int64
    /// Stabilize timer fired at `nowMs`.
    | StabilizeElapsed of nowMs: int64
    /// Caller invoked the Shutdown returned by `runCluster`.
    | Shutdown

/// Effects the orchestrator must apply after `step` returns.
type SupervisorEffect =
    /// Activate the topology via `Hosting.runWith` (master path).
    | StartTopology
    /// Stop and shutdown the running topology (lost mastership or shutting down).
    | StopTopology
    /// Schedule a `StabilizeElapsed` input to fire after the given delay.
    | ScheduleStabilizeIn of delayMs: int
    /// Emit a log line.
    | Log of LogLevel * string

/// External capabilities the cluster supervisor depends on. `Supervisor.runCluster`
/// wires these to their production defaults; tests (and alternate hosts) can
/// supply `runClusterWith` directly.
type ClusterDeps<'t> =
    { NowMs: unit -> int64
      LoadOrCreatePeerId: string -> Result<PeerId, StoreError>
      LoadPeersCache: string -> Result<PeerEndpoint list, StoreError>
      SavePeersCache: string -> PeerEndpoint list -> Result<unit, StoreError>
      CreateTransport:
        (LogLevel -> (unit -> string) -> unit)
            -> PeerId -> PeerEndpoint -> int -> int -> PeerTransport
      RunTopology: (int -> Log) -> Topology<'t> -> (unit -> unit) }

[<RequireQualifiedAccess>]
module Supervisor =

    /// Pure transition: `(stabilizeMs, state) -> input -> (state, effects)`.
    /// All time arithmetic uses `nowMs` from the input; the function does not
    /// read any clock itself.
    let step (stabilizeMs: int) (state: SupervisorState) (input: SupervisorInput)
            : SupervisorState * SupervisorEffect list =
        match state, input with
        // Standby -------------------------------------------------------------
        | Standby, ElectionUpdate (true, t) ->
            Activating t,
            [ Log (LogLevel.Info, sprintf "elected master; activating in %dms" stabilizeMs)
              ScheduleStabilizeIn stabilizeMs ]
        | Standby, ElectionUpdate (false, _) -> state, []
        | Standby, StabilizeElapsed _ -> state, []

        // Activating ----------------------------------------------------------
        | Activating _, ElectionUpdate (true, _) -> state, []
        | Activating _, ElectionUpdate (false, _) ->
            Standby,
            [ Log (LogLevel.Info, "lost mastership before stabilize; remaining standby") ]
        | Activating since, StabilizeElapsed t when t - since >= int64 stabilizeMs ->
            Master,
            [ StartTopology
              Log (LogLevel.Info, "stabilize window elapsed; activating topology") ]
        | Activating _, StabilizeElapsed _ -> state, []

        // Master --------------------------------------------------------------
        | Master, ElectionUpdate (true, _) -> state, []
        | Master, ElectionUpdate (false, _) ->
            Standby,
            [ StopTopology
              Log (LogLevel.Info, "lost mastership; stopping topology, returning to standby") ]
        | Master, StabilizeElapsed _ -> state, []

        // Shutdown ------------------------------------------------------------
        | Master, Shutdown ->
            Stopping,
            [ StopTopology; Log (LogLevel.Info, "shutdown requested while master") ]
        | (Standby | Activating _), Shutdown ->
            Stopping, [ Log (LogLevel.Info, "shutdown requested") ]
        | Stopping, _ -> state, []


    // ---- impure orchestration ------------------------------------------------

    /// Internal messages serialized through the supervisor mailbox. All state
    /// mutations happen inside the mailbox's fold loop — no locks needed.
    type private Msg =
        | Input of SupervisorInput
        | Inbound of WireEnvelope * int64
        | HeartbeatTick of int64
        | PeersCacheSaved of Result<unit, StoreError>

    /// Run a topology under cluster supervision using the provided external
    /// capabilities. If `CLUSTER_SEEDS` is empty (and `CLUSTER_LISTEN`
    /// likewise), this falls through to `deps.RunTopology` for single-process
    /// parity. Startup failures (bad cluster config, peer-id store I/O,
    /// transport bind) are returned as `Error`.
    let runClusterWith
            (deps: ClusterDeps<'t>)
            (startLog: int -> Log)
            (topology: Topology<'t>)
            : Result<unit -> unit, string> =
        let log = System.Diagnostics.Process.GetCurrentProcess().Id |> startLog
        let nowMs = deps.NowMs

        match ClusterConfig.tryFromConf topology.Conf with
        | Error msg ->
            log LogLevel.Error (fun _ -> sprintf "Invalid cluster configuration: %s" msg)
            Error (sprintf "Invalid cluster configuration: %s" msg)

        | Ok None ->
            log LogLevel.Info (fun _ -> "CLUSTER_SEEDS empty; falling through to single-process Hosting.runWith")
            Ok (deps.RunTopology startLog topology)

        | Ok (Some cfg) ->
            log LogLevel.Info (fun _ ->
                sprintf "Starting cluster supervisor: listen=%s seeds=[%s] quorum=%d"
                    (PeerEndpoint.toString cfg.Listen)
                    (cfg.Seeds |> List.map PeerEndpoint.toString |> String.concat ",")
                    cfg.Quorum)

            // Persistent peer identity.
            match deps.LoadOrCreatePeerId cfg.StateDir with
            | Error e -> Error (sprintf "PeerIdStore.loadOrCreate failed: %A" e)
            | Ok selfId ->

            // Cached peers (last known endpoints) merged with configured seeds.
            let cachedPeers =
                match deps.LoadPeersCache cfg.StateDir with
                | Ok eps -> eps
                | Error e ->
                    log LogLevel.Warn (fun _ -> sprintf "PeersCache.load failed (%A); ignoring" e)
                    []

            let initialPeerEndpoints =
                cfg.Seeds @ cachedPeers
                |> List.distinct
                |> List.filter (fun ep -> ep <> cfg.Listen)

            // Transport.
            let transport =
                deps.CreateTransport
                    (fun lvl thunk -> log lvl thunk)
                    selfId
                    cfg.Listen
                    cfg.SendQueueBound
                    cfg.MaxFrameBytes

            // Forward-declared mailbox reference so callbacks can post before
            // the mailbox value is bound (set immediately after MailboxProcessor.Start).
            let mutable mbRef : MailboxProcessor<Msg> option = None
            let post msg =
                match mbRef with
                | Some mb -> mb.Post msg
                | None -> ()

            // One-shot stabilize timer: self-disposes on fire.
            let scheduleStabilize (delayMs: int) =
                let timer : Timer ref = ref Unchecked.defaultof<_>
                timer.Value <-
                    new Timer(
                        TimerCallback(fun _ ->
                            post (Input (StabilizeElapsed (nowMs ())))
                            try timer.Value.Dispose() with _ -> ()),
                        null, int64 delayMs, Timeout.Infinite |> int64)

            // Inbound transport handler — pure post to mailbox.
            transport.Subscribe(fun ev ->
                match ev with
                | Received (_, env) -> post (Inbound (env, nowMs ()))
                | PeerDropped _ -> ()) |> ignore

            match transport.Start() with
            | Error e -> Error (sprintf "Transport.Start failed: %A" e)
            | Ok () ->

            // Heartbeat timer — periodic HeartbeatTick into the mailbox.
            let heartbeatTimer =
                new Timer(
                    TimerCallback(fun _ -> post (HeartbeatTick (nowMs ()))),
                    null, int64 cfg.HeartbeatMs, int64 cfg.HeartbeatMs)

            // Fold the inbound envelope into the view. Returns (view', epochChanged).
            let applyInbound (now: int64) (env: WireEnvelope) (view: MemberView) =
                let prevEpoch = view.Epoch
                let view' =
                    match env with
                    | Hello (peer, ep, _epoch) ->
                        let v', _ =
                            MemberView.join now
                                { Id = peer; Endpoint = ep; Incarnation = Incarnation 0UL; State = Alive }
                                view
                        MemberView.refresh now peer v'
                    | Welcome (peer, digests, _epoch)
                    | Ping (peer, _epoch, digests)
                    | Ack (peer, _epoch, digests) ->
                        let v' = MemberView.refresh now peer view
                        let v'', _ = MemberView.merge now digests v'
                        v''
                    | PingReq _ | PingReqAck _ -> view
                    | Leaving (peer, _epoch) ->
                        match MemberView.leave now peer view with
                        | Ok (v', _) -> v'
                        | Error _ -> view
                view', view'.Epoch <> prevEpoch

            // Compute selfIsMaster from a view.
            let selfIsMaster (view: MemberView) =
                let elected = Election.elect topology.Name view
                MemberView.hasQuorum cfg.Quorum view && elected.Master = ValueSome selfId

            // The mailbox fold loop. All state lives in the loop args — no locks.
            let mb =
                MailboxProcessor<Msg>.Start(fun inbox ->
                    let applyEffect (running: (unit -> unit) option) (eff: SupervisorEffect) =
                        match eff with
                        | Log (lvl, msg) ->
                            log lvl (fun _ -> msg)
                            running
                        | ScheduleStabilizeIn d ->
                            scheduleStabilize d
                            running
                        | StartTopology ->
                            match running with
                            | Some _ -> running
                            | None -> Some (deps.RunTopology startLog topology)
                        | StopTopology ->
                            match running with
                            | Some stop ->
                                try stop () with ex ->
                                    log LogLevel.Error (fun _ -> sprintf "topology stop failed: %A" ex)
                                None
                            | None -> None

                    let drive (sup: SupervisorState) (running: (unit -> unit) option) (input: SupervisorInput) =
                        let sup', effs = step cfg.StabilizeMs sup input
                        let running' = effs |> List.fold applyEffect running
                        sup', running'

                    // Fire-and-forget PeersCache.save on the thread pool, signalling
                    // the result back through the mailbox so failures are noticed and
                    // overlapping saves are avoided. Async.Start is the sanctioned
                    // fire-and-forget entry point (cold async that self-roots once started).
                    let kickSave (endpoints: PeerEndpoint list) =
                        async {
                            let result =
                                try deps.SavePeersCache cfg.StateDir endpoints
                                with ex -> Error (IoFailed (cfg.StateDir, ex))
                            post (PeersCacheSaved result)
                        }
                        |> Async.Start

                    let handleInbound
                            (sup: SupervisorState)
                            (view: MemberView)
                            (running: (unit -> unit) option)
                            (env: WireEnvelope)
                            (now: int64) =
                        let view', changed = applyInbound now env view
                        let sup', running' =
                            if changed then
                                drive sup running (ElectionUpdate (selfIsMaster view', now))
                            else sup, running
                        sup', view', running'

                    let handleHeartbeat
                            (sup: SupervisorState)
                            (view: MemberView)
                            (running: (unit -> unit) option)
                            (saveInFlight: bool)
                            (now: int64) =
                        let view1, _ = MemberView.sweep now cfg.HeartbeatMs cfg.SuspectTimeoutMs view
                        let digests = MemberView.digest view1
                        let peerEndpoints =
                            let fromView =
                                view1.Members
                                |> Map.toList
                                |> List.choose (fun (pid, m) ->
                                    if pid = selfId then None else Some m.Endpoint)
                            let configuredOnly =
                                initialPeerEndpoints
                                |> List.filter (fun ep -> not (List.contains ep fromView))
                            fromView @ configuredOnly
                        let ping = Ping (selfId, view1.Epoch, digests)
                        for ep in peerEndpoints do
                            transport.Send ep ping
                        // Persist peers.dat off-thread; skip if a previous save is
                        // still in flight (next tick will try again with fresher data).
                        let saveInFlight' =
                            if saveInFlight then saveInFlight
                            else
                                let knownEndpoints =
                                    view1.Members
                                    |> Map.toList
                                    |> List.choose (fun (pid, m) ->
                                        if pid = selfId then None else Some m.Endpoint)
                                kickSave knownEndpoints
                                true
                        // Sweep may have confirmed Dead peers → epoch bump → re-elect.
                        let sup', running' =
                            drive sup running (ElectionUpdate (selfIsMaster view1, now))
                        sup', view1, running', saveInFlight'

                    let rec loop (sup: SupervisorState)
                                 (view: MemberView)
                                 (running: (unit -> unit) option)
                                 (saveInFlight: bool) = async {
                        let! msg = inbox.Receive()
                        if sup = Stopping then
                            // Drain but ignore further messages; closure disposes mailbox.
                            return! loop sup view running saveInFlight
                        else
                        match msg with
                        | Input input ->
                            let sup', running' = drive sup running input
                            return! loop sup' view running' saveInFlight

                        | Inbound (env, now) ->
                            let sup', view', running' = handleInbound sup view running env now
                            return! loop sup' view' running' saveInFlight

                        | HeartbeatTick now ->
                            let sup', view', running', saveInFlight' =
                                handleHeartbeat sup view running saveInFlight now
                            return! loop sup' view' running' saveInFlight'

                        | PeersCacheSaved result ->
                            match result with
                            | Ok () -> ()
                            | Error e ->
                                log LogLevel.Warn (fun _ -> sprintf "PeersCache.save failed: %A" e)
                            return! loop sup view running false
                    }
                    loop Standby (MemberView.init selfId cfg.Listen (nowMs ())) None false)
            mbRef <- Some mb

            // Initial greet + decision (singleton case: Self alone with quorum=1
            // will activate after stabilize).
            let helloEnvelope = Hello (selfId, cfg.Listen, Epoch.zero)
            for ep in initialPeerEndpoints do
                transport.Send ep helloEnvelope
            post (Input (ElectionUpdate (selfIsMaster (MemberView.init selfId cfg.Listen (nowMs ())), nowMs ())))

            // Shutdown closure — idempotent.
            let mutable stopRequested = false
            Ok (fun () ->
                if not stopRequested then
                    stopRequested <- true
                    log LogLevel.Info (fun _ -> "Cluster supervisor shutdown requested")
                    // Send Leaving to peers so they evict us promptly.
                    let leaving = Leaving (selfId, Epoch.zero)
                    for ep in initialPeerEndpoints do
                        try transport.Send ep leaving with _ -> ()
                    try heartbeatTimer.Dispose() with _ -> ()
                    post (Input Shutdown)
                    // Give the mailbox a moment to process Shutdown and stop the topology.
                    try (mb :> IDisposable).Dispose() with _ -> ()
                    transport.Stop())

    /// Production defaults for `ClusterDeps`: system clock, on-disk peer-id
    /// store, on-disk peers cache, real TCP transport, real topology host.
    let defaultDeps<'t> : ClusterDeps<'t> =
        { NowMs = fun () -> DateTime.UtcNow.Ticks / 10_000L
          LoadOrCreatePeerId = PeerIdStore.loadOrCreate
          LoadPeersCache = PeersCache.load
          SavePeersCache = PeersCache.save
          CreateTransport = PeerTransport.create
          RunTopology = Hosting.runWith }

    /// Run a topology under cluster supervision with production defaults.
    /// Equivalent to `runClusterWith defaultDeps`.
    let runCluster (startLog: int -> Log) (topology: Topology<'t>) : Result<unit -> unit, string> =
        runClusterWith defaultDeps startLog topology
