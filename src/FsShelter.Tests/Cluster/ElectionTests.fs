module FsShelter.Cluster.ElectionTests

open NUnit.Framework
open Swensen.Unquote
open FsShelter.Cluster

let private peerA = PeerId.create()
let private peerB = PeerId.create()
let private peerC = PeerId.create()
let private peerD = PeerId.create()

let private epA : PeerEndpoint = { Host = "a"; Port = 1 }
let private epB : PeerEndpoint = { Host = "b"; Port = 2 }
let private epC : PeerEndpoint = { Host = "c"; Port = 3 }
let private epD : PeerEndpoint = { Host = "d"; Port = 4 }

let private digest id endpoint state : MemberDigest =
    { Id = id; Endpoint = endpoint; Incarnation = Incarnation 0UL; State = state }

let private viewWithLive (self: PeerId) (selfEp: PeerEndpoint) (others: (PeerId * PeerEndpoint) list) =
    let v0 = MemberView.init self selfEp 0L
    others
    |> List.fold
        (fun v (p, ep) -> fst (MemberView.join 1L (digest p ep Alive) v))
        v0


module Elect =

    [<Test>]
    let ``empty live set yields no master``() =
        let v0 = MemberView.init peerA epA 0L
        // Mark Self Suspect via direct removal trick: simulate by removing Self from Members
        let v = { v0 with Members = Map.empty }
        let r = Election.elect "topo" v
        r.Master =! ValueNone
        r.LiveCount =! 0

    [<Test>]
    let ``deterministic: same view yields same master``() =
        let v = viewWithLive peerA epA [ peerB, epB; peerC, epC ]
        let r1 = Election.elect "topo" v
        let r2 = Election.elect "topo" v
        r1.Master =! r2.Master
        r1.Master |> ValueOption.isSome =! true

    [<Test>]
    let ``master is one of the live members``() =
        let v = viewWithLive peerA epA [ peerB, epB; peerC, epC ]
        let live = MemberView.live v |> List.map (fun m -> m.Id)
        match (Election.elect "topo" v).Master with
        | ValueSome m -> List.contains m live =! true
        | ValueNone -> Assert.Fail "expected a master"

    [<Test>]
    let ``different topology names may yield different masters``() =
        // With 3 live peers and SHA-256 derived scores, varying the name
        // will (with overwhelming probability) shift the argmax for some name.
        let v = viewWithLive peerA epA [ peerB, epB; peerC, epC ]
        let masters =
            [ "alpha"; "beta"; "gamma"; "delta"; "epsilon"; "zeta"; "eta"; "theta" ]
            |> List.map (fun n -> (Election.elect n v).Master)
            |> List.distinct
        masters |> List.length >! 1

    [<Test>]
    let ``epoch change may shift master``() =
        // Find an epoch where the master differs from epoch 0; with SHA-256
        // and 3 candidates, this should hit within a small range.
        let v0 = viewWithLive peerA epA [ peerB, epB; peerC, epC ]
        let m0 = (Election.elect "topo" v0).Master
        let shifted =
            seq { 1UL .. 64UL }
            |> Seq.exists (fun e ->
                let v = { v0 with Epoch = Epoch e }
                (Election.elect "topo" v).Master <> m0)
        shifted =! true

    [<Test>]
    let ``epoch-stickiness: same membership different epochs both deterministic``() =
        let v = viewWithLive peerA epA [ peerB, epB; peerC, epC ]
        for e in 0UL .. 5UL do
            let v' = { v with Epoch = Epoch e }
            (Election.elect "topo" v').Master =! (Election.elect "topo" v').Master

    [<Test>]
    let ``Suspect peers are excluded``() =
        let v0 = viewWithLive peerA epA [ peerB, epB; peerC, epC ]
        // Suspect peerB and peerC; only Self Alive remains.
        let v1 = match MemberView.suspect 10L peerB v0 with Ok (v, _) -> v | _ -> failwith "setup"
        let v2 = match MemberView.suspect 10L peerC v1 with Ok (v, _) -> v | _ -> failwith "setup"
        let r = Election.elect "topo" v2
        r.Master =! ValueSome peerA
        r.LiveCount =! 1


module SelfIsMaster =

    [<Test>]
    let ``selfIsMaster agrees with elect``() =
        let v = viewWithLive peerA epA [ peerB, epB; peerC, epC; peerD, epD ]
        let m = (Election.elect "topo" v).Master
        Election.selfIsMaster "topo" v =! (m = ValueSome peerA)

    [<Test>]
    let ``selfIsMaster is false on empty live set``() =
        let v0 = MemberView.init peerA epA 0L
        let v = { v0 with Members = Map.empty }
        Election.selfIsMaster "topo" v =! false

    [<Test>]
    let ``singleton view: Self is master``() =
        let v = MemberView.init peerA epA 0L
        Election.selfIsMaster "topo" v =! true
        (Election.elect "topo" v).Master =! ValueSome peerA
