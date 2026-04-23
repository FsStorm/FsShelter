namespace FsShelter.Cluster

// SWIM-lite membership and failure detection.
//
// Background: Das, Gupta & Motivala, "SWIM: Scalable Weakly-consistent
// Infection-style Process Group Membership Protocol" (DSN 2002).
//   https://www.cs.cornell.edu/projects/Quicksilver/public_pdfs/SWIM.pdf
//
// This module implements the membership/incarnation/suspicion state machine
// only; the periodic ping / ping-req scheduling and gossip transport live
// in Cluster/Transport and (later) Cluster/Supervisor.

open System

/// A single member row in the local view. Extends MemberDigest with local
/// bookkeeping (last-heard timestamps) that never crosses the wire.
type Member =
    { Id: PeerId
      Endpoint: PeerEndpoint
      Incarnation: Incarnation
      State: MemberState
      /// Monotonic millis from the caller's clock; when this row was last refreshed.
      LastHeardMs: int64
      /// Monotonic millis; set when State transitions to Suspect, VNone otherwise.
      SuspectSinceMs: int64 voption }

/// The local view of the cluster at one peer. Pure value; all mutations are
/// functional updates. Self is never Dead in our own view.
type MemberView =
    { Self: PeerId
      SelfEndpoint: PeerEndpoint
      SelfIncarnation: Incarnation
      Epoch: Epoch
      Members: Map<PeerId, Member> }

/// Errors returned by MemberView operations that take a caller-supplied PeerId.
type MembershipError =
    | UnknownPeer of PeerId
    | CannotRemoveSelf
    | CannotSuspectSelf
    | CannotConfirmDeadSelf

/// Events emitted by state transitions. Consumed by the Supervisor to drive
/// election recompute and topology activation. Only EpochBumped fires when
/// the live set actually changes; Suspect flaps never bump epoch.
type MembershipEvent =
    | PeerJoined of Member
    | PeerLeft of PeerId
    | PeerSuspected of PeerId
    | PeerConfirmedDead of PeerId
    /// We saw ourselves reported Suspect/Dead and bumped our incarnation to refute.
    | PeerRefuted of PeerId * Incarnation
    | EpochBumped of Epoch

[<RequireQualifiedAccess>]
module MemberView =

    let private suspectMissK = 3

    let private stateRank s =
        match s with
        | Alive -> 0
        | Suspect -> 1
        | Dead -> 2

    let private makeMember (d: MemberDigest) (nowMs: int64) : Member =
        { Id = d.Id
          Endpoint = d.Endpoint
          Incarnation = d.Incarnation
          State = d.State
          LastHeardMs = nowMs
          SuspectSinceMs = if d.State = Suspect then ValueSome nowMs else ValueNone }

    /// Per-peer transition event (if any) and whether the change moves the
    /// Alive set (for epoch accounting). Suspect flaps and Dead↔Suspect churn
    /// never bump epoch per the module contract.
    let private classifyTransition (before: MemberState option) (after: MemberState) (m: Member) =
        match before, after with
        | None, Alive -> [ PeerJoined m ], true
        | None, _ -> [ PeerJoined m ], false
        | Some Alive, Alive -> [], false
        | Some Alive, Suspect -> [ PeerSuspected m.Id ], false
        | Some Alive, Dead -> [ PeerConfirmedDead m.Id ], true
        | Some Suspect, Alive -> [], false
        | Some Suspect, Suspect -> [], false
        | Some Suspect, Dead -> [ PeerConfirmedDead m.Id ], true
        | Some Dead, Alive -> [ PeerJoined m ], true
        | Some Dead, _ -> [], false

    /// Apply a single digest under SWIM precedence; returns the updated view,
    /// per-peer events, and whether the Alive set changed.
    let private applyDigest (nowMs: int64) (d: MemberDigest) (view: MemberView) : MemberView * MembershipEvent list * bool =
        if d.Id = view.Self then
            view, [], false
        else
            match Map.tryFind d.Id view.Members with
            | None ->
                let m = makeMember d nowMs
                let view' = { view with Members = Map.add d.Id m view.Members }
                let events, bump = classifyTransition None d.State m
                view', events, bump
            | Some existing ->
                let accept =
                    if d.Incarnation > existing.Incarnation then true
                    elif d.Incarnation < existing.Incarnation then false
                    else stateRank d.State > stateRank existing.State
                if not accept then
                    view, [], false
                else
                    let m' =
                        { existing with
                            Endpoint = d.Endpoint
                            Incarnation = d.Incarnation
                            State = d.State
                            LastHeardMs = nowMs
                            SuspectSinceMs =
                                if d.State = Suspect then ValueSome nowMs else ValueNone }
                    let view' = { view with Members = Map.add d.Id m' view.Members }
                    let events, bump = classifyTransition (Some existing.State) d.State m'
                    view', events, bump

    let private bumpEpoch (view: MemberView) (events: MembershipEvent list) =
        let view' = { view with Epoch = Epoch.next view.Epoch }
        view', events @ [ EpochBumped view'.Epoch ]

    /// Fresh view containing only Self as Alive at Incarnation 0, Epoch 0.
    let init (self: PeerId) (selfEndpoint: PeerEndpoint) (nowMs: int64) : MemberView =
        let selfMember =
            { Id = self
              Endpoint = selfEndpoint
              Incarnation = Incarnation.zero
              State = Alive
              LastHeardMs = nowMs
              SuspectSinceMs = ValueNone }
        { Self = self
          SelfEndpoint = selfEndpoint
          SelfIncarnation = Incarnation.zero
          Epoch = Epoch.zero
          Members = Map.ofList [ self, selfMember ] }

    /// Add or refresh a peer from an inbound digest / seed list. If the peer
    /// is new, emits PeerJoined and bumps epoch; if it is known, applies SWIM
    /// merge semantics on that single entry.
    let join (nowMs: int64) (digest: MemberDigest) (view: MemberView) : MemberView * MembershipEvent list =
        let view', events, bump = applyDigest nowMs digest view
        if bump then bumpEpoch view' events else view', events

    /// Mark a peer as having left cleanly (received a Leaving envelope). Removes
    /// from the view and bumps epoch. Errors if peer unknown or is Self.
    let leave (_nowMs: int64) (peer: PeerId) (view: MemberView) : Result<MemberView * MembershipEvent list, MembershipError> =
        if peer = view.Self then Error CannotRemoveSelf
        else
            match Map.tryFind peer view.Members with
            | None -> Error (UnknownPeer peer)
            | Some m ->
                let view' = { view with Members = Map.remove peer view.Members }
                let events = [ PeerLeft peer ]
                let result =
                    if m.State = Alive then bumpEpoch view' events
                    else view', events
                Ok result

    /// Promote Alive → Suspect when local pings time out. No epoch bump.
    /// Errors if peer unknown or is Self.
    let suspect (nowMs: int64) (peer: PeerId) (view: MemberView) : Result<MemberView * MembershipEvent list, MembershipError> =
        if peer = view.Self then Error CannotSuspectSelf
        else
            match Map.tryFind peer view.Members with
            | None -> Error (UnknownPeer peer)
            | Some m ->
                match m.State with
                | Alive ->
                    let m' =
                        { m with
                            State = Suspect
                            SuspectSinceMs = ValueSome nowMs
                            LastHeardMs = nowMs }
                    let view' = { view with Members = Map.add peer m' view.Members }
                    Ok (view', [ PeerSuspected peer ])
                | Suspect
                | Dead -> Ok (view, [])

    /// Confirm Suspect → Dead after indirect probes fail past suspectTimeoutMs.
    /// Bumps epoch because the live set shrinks. Errors if peer unknown or is Self.
    let confirmDead (nowMs: int64) (peer: PeerId) (view: MemberView) : Result<MemberView * MembershipEvent list, MembershipError> =
        if peer = view.Self then Error CannotConfirmDeadSelf
        else
            match Map.tryFind peer view.Members with
            | None -> Error (UnknownPeer peer)
            | Some m ->
                match m.State with
                | Dead -> Ok (view, [])
                | Alive
                | Suspect ->
                    let m' =
                        { m with
                            State = Dead
                            SuspectSinceMs = ValueNone
                            LastHeardMs = nowMs }
                    let view' = { view with Members = Map.add peer m' view.Members }
                    let view'', events = bumpEpoch view' [ PeerConfirmedDead peer ]
                    Ok (view'', events)

    /// Heard from a peer directly — bounces Suspect back to Alive if we had
    /// suspected it, and updates LastHeardMs in any case. No-op if unknown.
    let refresh (nowMs: int64) (peer: PeerId) (view: MemberView) : MemberView =
        if peer = view.Self then view
        else
            match Map.tryFind peer view.Members with
            | None -> view
            | Some m ->
                let m' =
                    { m with
                        State = (if m.State = Suspect then Alive else m.State)
                        LastHeardMs = nowMs
                        SuspectSinceMs = ValueNone }
                { view with Members = Map.add peer m' view.Members }

    /// We received a digest claiming *we* are Suspect or Dead. Bump our own
    /// incarnation so our next gossip refutes it. Emits PeerRefuted.
    let refuteSelf (nowMs: int64) (view: MemberView) : MemberView * MembershipEvent list =
        let newInc = Incarnation.next view.SelfIncarnation
        let members =
            match Map.tryFind view.Self view.Members with
            | Some m ->
                let m' =
                    { m with
                        State = Alive
                        Incarnation = newInc
                        LastHeardMs = nowMs
                        SuspectSinceMs = ValueNone }
                Map.add view.Self m' view.Members
            | None -> view.Members
        let view' = { view with SelfIncarnation = newInc; Members = members }
        view', [ PeerRefuted (view.Self, newInc) ]

    /// Fold a set of remote digests into our view — SWIM merge with
    /// incarnation precedence (higher wins); on equal incarnations, state
    /// precedence is Dead > Suspect > Alive, except Self is always Alive in
    /// its own view. Bumps epoch once if the live set actually changes.
    let merge (nowMs: int64) (digests: MemberDigest list) (view: MemberView) : MemberView * MembershipEvent list =
        let view', events, anyBump =
            digests
            |> List.fold
                (fun (v, es, b) d ->
                    let v', e, b' = applyDigest nowMs d v
                    v', es @ e, b || b')
                (view, [], false)
        if anyBump then bumpEpoch view' events else view', events

    /// Project the current view to a wire-ready digest list (strips local
    /// bookkeeping fields).
    let digest (view: MemberView) : MemberDigest list =
        view.Members
        |> Map.toList
        |> List.map (fun (_, m) ->
            { Id = m.Id
              Endpoint = m.Endpoint
              Incarnation = m.Incarnation
              State = m.State })

    /// Live members (Alive only; input for HRW and quorum counting).
    let live (view: MemberView) : Member list =
        view.Members
        |> Map.toList
        |> List.choose (fun (_, m) -> if m.State = Alive then Some m else None)

    /// True when live count ≥ requiredQuorum.
    let hasQuorum (requiredQuorum: int) (view: MemberView) : bool =
        List.length (live view) >= requiredQuorum

    /// Time-based sweep: promote Alive peers whose LastHeardMs is older than
    /// heartbeatMs * K to Suspect; confirm Dead those Suspect for more than
    /// suspectTimeoutMs. Returns all resulting state transitions as events
    /// and bumps epoch at most once if the live set changed.
    let sweep (nowMs: int64) (heartbeatMs: int) (suspectTimeoutMs: int) (view: MemberView) : MemberView * MembershipEvent list =
        let aliveCutoff = int64 heartbeatMs * int64 suspectMissK
        let folder (v: MemberView, evts: MembershipEvent list, anyDead: bool) (peerId: PeerId) (m: Member) =
            if peerId = v.Self then v, evts, anyDead
            else
                match m.State with
                | Alive when nowMs - m.LastHeardMs > aliveCutoff ->
                    let m' = { m with State = Suspect; SuspectSinceMs = ValueSome nowMs }
                    { v with Members = Map.add peerId m' v.Members }, evts @ [ PeerSuspected peerId ], anyDead
                | Suspect ->
                    match m.SuspectSinceMs with
                    | ValueSome since when nowMs - since >= int64 suspectTimeoutMs ->
                        let m' = { m with State = Dead; SuspectSinceMs = ValueNone }
                        { v with Members = Map.add peerId m' v.Members },
                        evts @ [ PeerConfirmedDead peerId ],
                        true
                    | _ -> v, evts, anyDead
                | _ -> v, evts, anyDead
        let view', events, anyDead =
            view.Members |> Map.fold folder (view, [], false)
        if anyDead then bumpEpoch view' events else view', events
