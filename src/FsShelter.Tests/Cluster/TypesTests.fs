module FsShelter.Cluster.TypesTests

open NUnit.Framework
open Swensen.Unquote
open FsShelter.Cluster


module PeerEndpoint =

    [<Test>]
    let ``ofString accepts host:port``() =
        PeerEndpoint.ofString "localhost:7001" =! Ok { Host = "localhost"; Port = 7001 }

    [<Test>]
    let ``ofString accepts IPv4 host``() =
        PeerEndpoint.ofString "127.0.0.1:9000" =! Ok { Host = "127.0.0.1"; Port = 9000 }

    [<Test>]
    let ``ofString rejects missing port``() =
        match PeerEndpoint.ofString "localhost" with
        | Error _ -> ()
        | Ok ep -> Assert.Fail (sprintf "expected Error, got %A" ep)

    [<Test>]
    let ``ofString rejects empty``() =
        match PeerEndpoint.ofString "" with
        | Error _ -> ()
        | Ok ep -> Assert.Fail (sprintf "expected Error, got %A" ep)

    [<Test>]
    let ``ofStringList parses comma and semicolon``() =
        let input = "host1:1, host2:2 ; host3:3"
        match PeerEndpoint.ofStringList input with
        | Ok [ a; b; c ] ->
            a =! { Host = "host1"; Port = 1 }
            b =! { Host = "host2"; Port = 2 }
            c =! { Host = "host3"; Port = 3 }
        | other -> Assert.Fail (sprintf "expected 3 entries, got %A" other)

    [<Test>]
    let ``toString round-trips``() =
        let ep = { Host = "example.com"; Port = 42 }
        PeerEndpoint.ofString (PeerEndpoint.toString ep) =! Ok ep


module PeerId =

    [<Test>]
    let ``create yields unique ids``() =
        let a = PeerId.create()
        let b = PeerId.create()
        a <>! b

    [<Test>]
    let ``round-trips through text``() =
        let id = PeerId.create()
        PeerId.ofString (PeerId.toString id) =! Ok id


module Epoch =

    [<Test>]
    let ``next increments``() =
        Epoch.next Epoch.zero |> Epoch.value =! 1UL
        Epoch.next (Epoch.next Epoch.zero) |> Epoch.value =! 2UL


module Incarnation =

    [<Test>]
    let ``next increments``() =
        Incarnation.next Incarnation.zero |> Incarnation.value =! 1UL


module ClusterConfig =

    [<Test>]
    let ``tryFromConf None when seeds missing``() =
        let conf = FsShelter.Conf.empty
        match ClusterConfig.tryFromConf conf with
        | Ok None -> ()
        | other -> Assert.Fail (sprintf "%A" other)

    [<Test>]
    let ``tryFromConf picks defaults``() =
        let conf =
            FsShelter.Conf.ofList
                [ FsShelter.ConfOption.CLUSTER_LISTEN "127.0.0.1:7000"
                  FsShelter.ConfOption.CLUSTER_SEEDS "127.0.0.1:7001,127.0.0.1:7002" ]
        match ClusterConfig.tryFromConf conf with
        | Ok (Some cfg) ->
            cfg.Listen =! { Host = "127.0.0.1"; Port = 7000 }
            cfg.Seeds |> List.length =! 2
            cfg.Quorum =! 2
            cfg.HeartbeatMs =! 1000
        | other -> Assert.Fail (sprintf "%A" other)
