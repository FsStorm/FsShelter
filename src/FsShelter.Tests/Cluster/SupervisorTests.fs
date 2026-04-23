module FsShelter.Cluster.SupervisorTests

open System
open System.Threading
open NUnit.Framework
open Swensen.Unquote
open FsShelter
open FsShelter.Cluster


module Step =

    let private stabilize = 100

    [<Test>]
    let ``Standby + ElectionUpdate(true) -> Activating with stabilize scheduled``() =
        let s', effs = Supervisor.step stabilize Standby (ElectionUpdate (true, 1000L))
        match s' with
        | Activating 1000L -> ()
        | other -> Assert.Fail (sprintf "expected Activating 1000, got %A" other)
        effs |> List.exists (function ScheduleStabilizeIn d -> d = stabilize | _ -> false) =! true

    [<Test>]
    let ``Standby + ElectionUpdate(false) -> no change``() =
        let s', effs = Supervisor.step stabilize Standby (ElectionUpdate (false, 1000L))
        s' =! Standby
        effs |> List.exists (function StartTopology | StopTopology -> true | _ -> false) =! false

    [<Test>]
    let ``Activating + ElectionUpdate(false) -> Standby (no StopTopology)``() =
        let s', effs = Supervisor.step stabilize (Activating 0L) (ElectionUpdate (false, 50L))
        s' =! Standby
        effs |> List.exists (function StopTopology -> true | _ -> false) =! false

    [<Test>]
    let ``Activating + StabilizeElapsed before window -> stays Activating``() =
        let s', effs = Supervisor.step stabilize (Activating 0L) (StabilizeElapsed 50L)
        match s' with Activating 0L -> () | other -> Assert.Fail (sprintf "%A" other)
        effs |> List.exists (function StartTopology -> true | _ -> false) =! false

    [<Test>]
    let ``Activating + StabilizeElapsed after window -> Master with StartTopology``() =
        let s', effs = Supervisor.step stabilize (Activating 0L) (StabilizeElapsed 100L)
        s' =! Master
        effs |> List.exists (function StartTopology -> true | _ -> false) =! true

    [<Test>]
    let ``Master + ElectionUpdate(false) -> Standby with StopTopology``() =
        let s', effs = Supervisor.step stabilize Master (ElectionUpdate (false, 200L))
        s' =! Standby
        effs |> List.exists (function StopTopology -> true | _ -> false) =! true

    [<Test>]
    let ``Master + ElectionUpdate(true) -> no change``() =
        let s', effs = Supervisor.step stabilize Master (ElectionUpdate (true, 200L))
        s' =! Master
        effs |> List.exists (function StopTopology | StartTopology -> true | _ -> false) =! false

    [<Test>]
    let ``Shutdown from Master -> Stopping with StopTopology``() =
        let s', effs = Supervisor.step stabilize Master Shutdown
        s' =! Stopping
        effs |> List.exists (function StopTopology -> true | _ -> false) =! true

    [<Test>]
    let ``Shutdown from Standby -> Stopping (no StopTopology)``() =
        let s', effs = Supervisor.step stabilize Standby Shutdown
        s' =! Stopping
        effs |> List.exists (function StopTopology -> true | _ -> false) =! false

    [<Test>]
    let ``Stopping is terminal``() =
        let s', effs = Supervisor.step stabilize Stopping (ElectionUpdate (true, 0L))
        s' =! Stopping
        effs =! []


module RunClusterFallthrough =

    [<Test>]
    let ``runCluster with no CLUSTER_SEEDS falls through to Hosting.runWith``() =
        // TestTopology.t1 has no cluster config; runCluster should behave like runWith.
        match Supervisor.runCluster (fun _ _ -> ignore) FsShelter.TestTopology.t1 with
        | Error e -> failwithf "runCluster returned Error: %s" e
        | Ok stop ->
            // Give it a moment to stand up.
            Thread.Sleep 200
            stop ()


module RunClusterSingleton =

    let private freePort () =
        let l = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0)
        l.Start()
        let p = (l.LocalEndpoint :?> System.Net.IPEndPoint).Port
        l.Stop()
        p

    let private tempDir () =
        let p = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "fsshelter-sup-" + Guid.NewGuid().ToString("N"))
        System.IO.Directory.CreateDirectory p |> ignore
        p

    [<Test>]
    [<Category("interactive")>]
    let ``runCluster singleton (quorum=1) activates topology after stabilize``() =
        // Singleton cluster: Self alone with quorum=1 should win election and
        // activate the topology after the stabilize window. We assert no
        // exception is thrown across stand-up + tear-down.
        let port = freePort ()
        let dir = tempDir ()
        let listen = sprintf "127.0.0.1:%d" port
        let topo =
            FsShelter.TestTopology.t1
            |> withConf
                [ ConfOption.CLUSTER_LISTEN listen
                  ConfOption.CLUSTER_SEEDS listen
                  ConfOption.CLUSTER_QUORUM 1
                  ConfOption.CLUSTER_HEARTBEAT_MS 100
                  ConfOption.CLUSTER_SUSPECT_TIMEOUT_MS 500
                  ConfOption.CLUSTER_STABILIZE_MS 200
                  ConfOption.CLUSTER_STATE_DIR dir ]
        try
            match Supervisor.runCluster (fun _ _ -> ignore) topo with
            | Error e -> failwithf "runCluster returned Error: %s" e
            | Ok stop ->
                // Wait for activation: stabilize 200ms + slack.
                Thread.Sleep 1500
                stop ()
        finally
            try System.IO.Directory.Delete(dir, true) with _ -> ()
