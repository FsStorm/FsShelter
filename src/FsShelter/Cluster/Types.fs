namespace FsShelter.Cluster

open System
open System.Net

/// Stable per-process identity. Persisted to `peer-id.dat` on first boot and
/// reused across restarts so HRW rank survives IP/host changes.
[<Struct>]
type PeerId = PeerId of Guid

[<RequireQualifiedAccess>]
module PeerId =
    let create () = Guid.NewGuid() |> PeerId

    let value (PeerId g) = g

    let toString (PeerId g) = g.ToString("N")

    let ofString (s: string) : Result<PeerId, string> =
        match Guid.TryParse s with
        | true, g -> Ok (PeerId g)
        | _ -> Error (sprintf "invalid PeerId: %s" s)


/// Network address a peer listens on.
[<Struct>]
type PeerEndpoint = { Host: string; Port: int }

[<RequireQualifiedAccess>]
module PeerEndpoint =
    let make (host: string) (port: int) = { Host = host; Port = port }

    let toString (ep: PeerEndpoint) = sprintf "%s:%d" ep.Host ep.Port

    let ofString (s: string) : Result<PeerEndpoint, string> =
        match (s |> Option.ofObj |> Option.map (fun s -> s.Trim())) with
        | Some t when t.Length > 0 ->
            let i = t.LastIndexOf ':'
            if i <= 0 || i = t.Length - 1 then
                Error (sprintf "invalid endpoint: '%s' (expected host:port)" s)
            else
                let host = t.Substring(0, i)
                let portStr = t.Substring(i + 1)
                match Int32.TryParse portStr with
                | true, p when p > 0 && p < 65536 ->
                    Ok { Host = host; Port = p }
                | _ ->
                    Error (sprintf "invalid port in endpoint: '%s'" s)
        | _ -> Error "empty endpoint"

    let ofStringList (s: string) : Result<PeerEndpoint list, string> =
        match s |> Option.ofObj with
        | None -> Ok []
        | Some s ->
            s.Split([| ','; ';' |], StringSplitOptions.RemoveEmptyEntries ||| StringSplitOptions.TrimEntries)
            |> Array.toList
            |> List.fold
                (fun acc item ->
                    match acc with
                    | Error _ -> acc
                    | Ok eps ->
                        match ofString item with
                        | Ok ep -> Ok (ep :: eps)
                        | Error e -> Error e)
                (Ok [])
            |> Result.map List.rev


/// Monotonic epoch attached to every `MemberView`. Incremented on any
/// add/remove; used in HRW hash to make mastership sticky across non-fatal
/// membership changes.
[<Struct>]
type Epoch = Epoch of uint64

[<RequireQualifiedAccess>]
module Epoch =
    let zero = Epoch 0UL
    let next (Epoch e) = Epoch (e + 1UL)
    let value (Epoch e) = e


/// SWIM per-member "incarnation": a peer bumps its own incarnation when it
/// sees itself as Suspect elsewhere, overriding stale gossip.
[<Struct>]
type Incarnation = Incarnation of uint64

[<RequireQualifiedAccess>]
module Incarnation =
    let zero = Incarnation 0UL
    let next (Incarnation i) = Incarnation (i + 1UL)
    let value (Incarnation i) = i


/// Per-member SWIM state.
type MemberState =
    | Alive
    | Suspect
    | Dead


/// Gossip payload: one row per member the sender knows about. Wire-format
/// data contract shared between the Membership domain and the Transport.
type MemberDigest =
    { Id: PeerId
      Endpoint: PeerEndpoint
      Incarnation: Incarnation
      State: MemberState }


/// Parsed cluster config, derived from `FsShelter.Conf` at startup so the
/// Cluster modules never reach back into the Conf map at runtime.
type ClusterConfig =
    { Listen: PeerEndpoint
      Seeds: PeerEndpoint list
      HeartbeatMs: int
      SuspectTimeoutMs: int
      /// Majority threshold; default ⌊|Seeds|/2⌋+1.
      Quorum: int
      /// Post-election debounce before a would-be master activates.
      StabilizeMs: int
      /// Directory for `peer-id.dat` and `peers.dat`.
      StateDir: string
      /// Per-peer outbound queue bound (drops oldest on overflow).
      SendQueueBound: int
      /// Maximum single-frame size on the wire.
      MaxFrameBytes: int }

[<RequireQualifiedAccess>]
module ClusterConfig =
    open FsShelter

    /// Parse cluster settings from the topology's `Conf`. Returns `None`
    /// when `CLUSTER_SEEDS` is absent/empty — the signal for `runCluster`
    /// to fall through to single-process `runWith`.
    let tryFromConf (conf: Conf) : Result<ClusterConfig option, string> =
        let seeds =
            conf |> Conf.option ConfOption.CLUSTER_SEEDS |> Option.defaultValue ""
        let listenText =
            conf |> Conf.option ConfOption.CLUSTER_LISTEN |> Option.defaultValue ""

        if String.IsNullOrWhiteSpace seeds && String.IsNullOrWhiteSpace listenText then
            Ok None
        else
            match PeerEndpoint.ofString listenText with
            | Error e -> Error (sprintf "cluster.listen: %s" e)
            | Ok listen ->
                match PeerEndpoint.ofStringList seeds with
                | Error e -> Error (sprintf "cluster.seeds: %s" e)
                | Ok seedList ->
                    let defaultQuorum =
                        let n = List.length seedList
                        if n <= 0 then 1 else (n / 2) + 1
                    let cfg =
                        { Listen = listen
                          Seeds = seedList
                          HeartbeatMs = conf |> Conf.optionOrDefault ConfOption.CLUSTER_HEARTBEAT_MS
                          SuspectTimeoutMs = conf |> Conf.optionOrDefault ConfOption.CLUSTER_SUSPECT_TIMEOUT_MS
                          Quorum = conf |> Conf.option ConfOption.CLUSTER_QUORUM |> Option.defaultValue defaultQuorum
                          StabilizeMs = conf |> Conf.optionOrDefault ConfOption.CLUSTER_STABILIZE_MS
                          StateDir = conf |> Conf.option ConfOption.CLUSTER_STATE_DIR |> Option.defaultValue "."
                          SendQueueBound = conf |> Conf.optionOrDefault ConfOption.CLUSTER_SEND_QUEUE_BOUND
                          MaxFrameBytes = conf |> Conf.optionOrDefault ConfOption.CLUSTER_MAX_FRAME_BYTES }
                    Ok (Some cfg)
