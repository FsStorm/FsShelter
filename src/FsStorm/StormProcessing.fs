module StormProcessing
open StormThrift
open FsJson
open System.IO

//sumit the topology using nimbus thrift protocol
let submit name (topology:StormThrift.StormTopology) host port uploadedJarLocation =
    use tx = new Thrift.Transport.TSocket(host,port)
    use txf = new Thrift.Transport.TFramedTransport(tx)
    txf.Open()
    try
        use tp = new Thrift.Protocol.TBinaryProtocol(txf)
        use c = new Nimbus.Client(tp)
        c.submitTopology(name, uploadedJarLocation, "", topology)
    finally
        txf.Close()


//maps script names to runner functions
let runMap (topology:StormDSL.Topology) = 
        seq {
            for b in topology.Bolts -> b.Id,b.Bolt
            for s in topology.Spouts -> s.Id,s.Spout
        }
        |> Seq.choose (function | id,StormDSL.Local ref -> Some (id,ref) | _ -> None)
        |> Seq.map (fun (n,{Func=f}) -> n,f)
        |> Map.ofSeq

//hangs the main thread to keep the process alive
let sleep() =
    let rec sleep() =
        async {
            do! Async.Sleep (5*60*1000)
            return! sleep()
            }
    sleep() |> Async.RunSynchronously

let findComponent (cfg:Storm.Configuration) =
    let taskMap = cfg.Json?context?``task->component``
    let taskName = taskMap.Named cfg.TaskId
    taskName.Val

//run a specific component (spout or bolt).
//the component to run is determined
//from the configuration data passed in by storm
//in the handshake
let runComponent runMap =
    let tag = "runComponent"
    async {
        try 
            let! cfg = Storm.readHandshake()
            do! Storm.processPid cfg.PidDir
            let scriptName = findComponent cfg
            let func = runMap |> Map.find scriptName
            Storm.stormLog (sprintf "PID %A: running %A" (Storm.pid()) scriptName) Storm.LogLevel.Info
            (func cfg) |> Async.Start
        with ex ->
            //better to exit process if something goes wrong 
            //at this point
            Storm.stormLog (Storm.nestedExceptionTrace ex) Storm.LogLevel.Error
            System.Console.WriteLine(tag)
            System.Console.WriteLine(ex.Message)
            System.Console.WriteLine(ex.StackTrace)
            System.Environment.Exit(1)
    }
    |> Async.Start


///meant to be invoked from the main function of the host exe
let run exeName optionalArgs topology commandLineArgs = 
    try
        Storm.initIO()
        let runMap = runMap topology
        let argsList = Array.toList commandLineArgs
        match argsList with
        | "submit"::host::port::jarLocation::_ -> 
            printfn "%A" commandLineArgs
            let tp = StormDSLTranslate.translate exeName optionalArgs topology
            submit topology.TopologyName tp host (int port) jarLocation
            0
        | _ ->
            runComponent runMap
            sleep()
            0
    with ex ->
        Storm.stormLog (Storm.nestedExceptionTrace ex) Storm.LogLevel.Error
        printfn "%s" ex.Message
        1
