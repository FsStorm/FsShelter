module FsShelter.Cluster.MembershipTests

open NUnit.Framework
open Swensen.Unquote
open FsShelter.Cluster

let private peerA = PeerId.create()
let private peerB = PeerId.create()
let private peerC = PeerId.create()

let private ep host port : PeerEndpoint = { Host = host; Port = port }
let private epA = ep "a" 1
let private epB = ep "b" 2
let private epC = ep "c" 3

let private initA t = MemberView.init peerA epA t

let private digest id endpoint inc state : MemberDigest =
    { Id = id; Endpoint = endpoint; Incarnation = Incarnation inc; State = state }


module Init =

    [<Test>]
    let ``init seeds Self as Alive at Incarnation 0 and Epoch 0``() =
        let v = initA 0L
        v.Self =! peerA
        v.SelfIncarnation =! Incarnation.zero
        v.Epoch =! Epoch.zero
        MemberView.live v |> List.length =! 1
        (Map.find peerA v.Members).State =! Alive


module Join =

    [<Test>]
    let ``join of a new Alive peer emits PeerJoined and bumps epoch``() =
        let v0 = initA 0L
        let d = digest peerB epB 0UL Alive
        let v1, events = MemberView.join 100L d v0
        v1.Epoch =! Epoch.next Epoch.zero
        events |> List.exists (function PeerJoined m -> m.Id = peerB | _ -> false) =! true
        events |> List.exists (function EpochBumped _ -> true | _ -> false) =! true
        (Map.find peerB v1.Members).State =! Alive

    [<Test>]
    let ``join of a new Suspect peer emits PeerJoined but does not bump epoch``() =
        let v0 = initA 0L
        let d = digest peerB epB 0UL Suspect
        let v1, events = MemberView.join 100L d v0
        v1.Epoch =! Epoch.zero
        events |> List.exists (function EpochBumped _ -> true | _ -> false) =! false
        events |> List.exists (function PeerJoined _ -> true | _ -> false) =! true

    [<Test>]
    let ``join ignores digest whose id is Self``() =
        let v0 = initA 0L
        let d = digest peerA epA 99UL Suspect
        let v1, events = MemberView.join 100L d v0
        v1.Epoch =! Epoch.zero
        events =! []
        v1.SelfIncarnation =! Incarnation.zero


module Leave =

    [<Test>]
    let ``leave of Alive peer removes and bumps epoch``() =
        let v0, _ = MemberView.join 0L (digest peerB epB 0UL Alive) (initA 0L)
        match MemberView.leave 10L peerB v0 with
        | Ok (v1, events) ->
            Map.containsKey peerB v1.Members =! false
            events |> List.exists (function PeerLeft p -> p = peerB | _ -> false) =! true
            v1.Epoch =! Epoch.next v0.Epoch
        | Error e -> Assert.Fail (sprintf "%A" e)

    [<Test>]
    let ``leave of Suspect peer removes without bumping epoch``() =
        let v0, _ = MemberView.join 0L (digest peerB epB 0UL Alive) (initA 0L)
        let v1 = match MemberView.suspect 10L peerB v0 with Ok (v, _) -> v | _ -> failwith "setup"
        match MemberView.leave 20L peerB v1 with
        | Ok (v2, _) -> v2.Epoch =! v1.Epoch
        | Error e -> Assert.Fail (sprintf "%A" e)

    [<Test>]
    let ``leave unknown returns UnknownPeer``() =
        match MemberView.leave 0L peerB (initA 0L) with
        | Error (UnknownPeer p) -> p =! peerB
        | other -> Assert.Fail (sprintf "%A" other)

    [<Test>]
    let ``leave Self returns CannotRemoveSelf``() =
        match MemberView.leave 0L peerA (initA 0L) with
        | Error CannotRemoveSelf -> ()
        | other -> Assert.Fail (sprintf "%A" other)


module SuspectOp =

    [<Test>]
    let ``suspect Alive transitions to Suspect without epoch bump``() =
        let v0, _ = MemberView.join 0L (digest peerB epB 0UL Alive) (initA 0L)
        match MemberView.suspect 50L peerB v0 with
        | Ok (v1, events) ->
            (Map.find peerB v1.Members).State =! Suspect
            (Map.find peerB v1.Members).SuspectSinceMs =! ValueSome 50L
            v1.Epoch =! v0.Epoch
            events =! [ PeerSuspected peerB ]
        | Error e -> Assert.Fail (sprintf "%A" e)

    [<Test>]
    let ``suspect of already-Suspect peer is a no-op``() =
        let v0, _ = MemberView.join 0L (digest peerB epB 0UL Alive) (initA 0L)
        let v1 = match MemberView.suspect 50L peerB v0 with Ok (v, _) -> v | _ -> failwith "setup"
        match MemberView.suspect 60L peerB v1 with
        | Ok (v2, events) ->
            events =! []
            v2.Members =! v1.Members
        | Error e -> Assert.Fail (sprintf "%A" e)

    [<Test>]
    let ``suspect Self returns CannotSuspectSelf``() =
        match MemberView.suspect 0L peerA (initA 0L) with
        | Error CannotSuspectSelf -> ()
        | other -> Assert.Fail (sprintf "%A" other)

    [<Test>]
    let ``suspect unknown returns UnknownPeer``() =
        match MemberView.suspect 0L peerB (initA 0L) with
        | Error (UnknownPeer _) -> ()
        | other -> Assert.Fail (sprintf "%A" other)


module ConfirmDead =

    [<Test>]
    let ``confirmDead from Suspect bumps epoch``() =
        let v0, _ = MemberView.join 0L (digest peerB epB 0UL Alive) (initA 0L)
        let v1 = match MemberView.suspect 50L peerB v0 with Ok (v, _) -> v | _ -> failwith "setup"
        match MemberView.confirmDead 100L peerB v1 with
        | Ok (v2, events) ->
            (Map.find peerB v2.Members).State =! Dead
            v2.Epoch =! Epoch.next v1.Epoch
            events |> List.exists (function PeerConfirmedDead p -> p = peerB | _ -> false) =! true
            events |> List.exists (function EpochBumped _ -> true | _ -> false) =! true
        | Error e -> Assert.Fail (sprintf "%A" e)

    [<Test>]
    let ``confirmDead of already-Dead peer is a no-op``() =
        let v0, _ = MemberView.join 0L (digest peerB epB 0UL Dead) (initA 0L)
        match MemberView.confirmDead 100L peerB v0 with
        | Ok (v1, events) ->
            events =! []
            v1.Epoch =! v0.Epoch
        | Error e -> Assert.Fail (sprintf "%A" e)


module Refresh =

    [<Test>]
    let ``refresh on Suspect flips back to Alive without events``() =
        let v0, _ = MemberView.join 0L (digest peerB epB 0UL Alive) (initA 0L)
        let v1 = match MemberView.suspect 50L peerB v0 with Ok (v, _) -> v | _ -> failwith "setup"
        let v2 = MemberView.refresh 200L peerB v1
        (Map.find peerB v2.Members).State =! Alive
        (Map.find peerB v2.Members).SuspectSinceMs =! ValueNone
        (Map.find peerB v2.Members).LastHeardMs =! 200L
        v2.Epoch =! v1.Epoch

    [<Test>]
    let ``refresh on unknown peer is a no-op``() =
        let v0 = initA 0L
        MemberView.refresh 100L peerB v0 =! v0


module RefuteSelf =

    [<Test>]
    let ``refuteSelf bumps SelfIncarnation and emits PeerRefuted``() =
        let v0 = initA 0L
        let v1, events = MemberView.refuteSelf 100L v0
        v1.SelfIncarnation =! Incarnation.next v0.SelfIncarnation
        (Map.find peerA v1.Members).Incarnation =! v1.SelfIncarnation
        events |> List.exists (function PeerRefuted (p, i) -> p = peerA && i = v1.SelfIncarnation | _ -> false) =! true
        v1.Epoch =! v0.Epoch


module Merge =

    [<Test>]
    let ``merge of multiple new Alive peers emits at most one EpochBumped``() =
        let v0 = initA 0L
        let ds = [ digest peerB epB 0UL Alive; digest peerC epC 0UL Alive ]
        let v1, events = MemberView.merge 50L ds v0
        v1.Epoch =! Epoch.next Epoch.zero
        events |> List.filter (function EpochBumped _ -> true | _ -> false) |> List.length =! 1
        events |> List.filter (function PeerJoined _ -> true | _ -> false) |> List.length =! 2

    [<Test>]
    let ``merge accepts higher incarnation``() =
        let v0, _ = MemberView.join 0L (digest peerB epB 0UL Alive) (initA 0L)
        let v1, _ = MemberView.merge 10L [ digest peerB epB 5UL Suspect ] v0
        (Map.find peerB v1.Members).Incarnation =! Incarnation 5UL
        (Map.find peerB v1.Members).State =! Suspect

    [<Test>]
    let ``merge rejects lower incarnation``() =
        let v0, _ = MemberView.join 0L (digest peerB epB 5UL Alive) (initA 0L)
        let v1, events = MemberView.merge 10L [ digest peerB epB 1UL Dead ] v0
        (Map.find peerB v1.Members).State =! Alive
        events =! []

    [<Test>]
    let ``merge at equal incarnation prefers Dead over Suspect over Alive``() =
        let v0, _ = MemberView.join 0L (digest peerB epB 3UL Alive) (initA 0L)
        let v1, _ = MemberView.merge 10L [ digest peerB epB 3UL Suspect ] v0
        (Map.find peerB v1.Members).State =! Suspect
        let v2, _ = MemberView.merge 20L [ digest peerB epB 3UL Dead ] v1
        (Map.find peerB v2.Members).State =! Dead

    [<Test>]
    let ``merge ignores digests about Self``() =
        let v0 = initA 0L
        let v1, events = MemberView.merge 50L [ digest peerA epA 99UL Dead ] v0
        v1.SelfIncarnation =! Incarnation.zero
        (Map.find peerA v1.Members).State =! Alive
        events =! []


module Digest =

    [<Test>]
    let ``digest projects every member including Self``() =
        let v, _ = MemberView.join 0L (digest peerB epB 3UL Suspect) (initA 0L)
        let ds = MemberView.digest v
        ds |> List.length =! 2
        ds |> List.exists (fun d -> d.Id = peerA && d.State = Alive) =! true
        ds |> List.exists (fun d -> d.Id = peerB && d.State = Suspect && d.Incarnation = Incarnation 3UL) =! true


module LiveAndQuorum =

    [<Test>]
    let ``live excludes Suspect and Dead``() =
        let v0, _ = MemberView.join 0L (digest peerB epB 0UL Alive) (initA 0L)
        let v1, _ = MemberView.join 10L (digest peerC epC 0UL Alive) v0
        let v2 = match MemberView.suspect 20L peerB v1 with Ok (v, _) -> v | _ -> failwith "setup"
        MemberView.live v2 |> List.map (fun m -> m.Id) |> List.sort
            =! ([ peerA; peerC ] |> List.sort)

    [<Test>]
    let ``hasQuorum compares against live count``() =
        let v0, _ = MemberView.join 0L (digest peerB epB 0UL Alive) (initA 0L)
        MemberView.hasQuorum 2 v0 =! true
        MemberView.hasQuorum 3 v0 =! false


module Sweep =

    [<Test>]
    let ``sweep promotes stale Alive peers to Suspect``() =
        let v0, _ = MemberView.join 0L (digest peerB epB 0UL Alive) (initA 0L)
        // heartbeat 100ms, K=3 → cutoff 300ms
        let v1, events = MemberView.sweep 500L 100 10000 v0
        (Map.find peerB v1.Members).State =! Suspect
        events |> List.exists (function PeerSuspected p -> p = peerB | _ -> false) =! true
        v1.Epoch =! v0.Epoch

    [<Test>]
    let ``sweep confirms Dead after Suspect passes suspectTimeout, bumping epoch``() =
        let v0, _ = MemberView.join 0L (digest peerB epB 0UL Alive) (initA 0L)
        let v1 = match MemberView.suspect 100L peerB v0 with Ok (v, _) -> v | _ -> failwith "setup"
        let v2, events = MemberView.sweep 10200L 1000 1000 v1
        (Map.find peerB v2.Members).State =! Dead
        v2.Epoch =! Epoch.next v1.Epoch
        events |> List.exists (function PeerConfirmedDead p -> p = peerB | _ -> false) =! true
        events |> List.exists (function EpochBumped _ -> true | _ -> false) =! true

    [<Test>]
    let ``sweep leaves fresh Alive peers alone``() =
        let v0, _ = MemberView.join 100L (digest peerB epB 0UL Alive) (initA 0L)
        let v1, events = MemberView.sweep 150L 100 1000 v0
        (Map.find peerB v1.Members).State =! Alive
        events =! []
        v1.Epoch =! v0.Epoch
