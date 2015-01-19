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
            for b in topology.Bolts -> b.Bolt
            for s in topology.Spouts -> s.Spout
        }
        |> Seq.choose (function | StormDSL.Local ref -> Some ref | _ -> None)
        |> Seq.map (fun {Name=n; Func=f} -> n,f)
        |> Map.ofSeq

//hangs the main thread to keep the process alive
let sleep() =
    let rec sleep() =
        async {
            do! Async.Sleep (5*60*1000)
            return! sleep()
            }
    sleep() |> Async.RunSynchronously

let setupLogging (cfg:Storm.Configuration) =
    let tag = "setupLogging"
    let pid = Storm.pid().ToString() //used to differentiate logs from different instances of the same component
    match cfg.Json?conf?(Storm.FSLOGDIR) with
    | JsonString dir ->  
        Logging.terminateLog(); 
        let logPath = Path.Combine(dir,pid)
        Logging.log_path <- logPath
    | _ -> ()

//run a specific component (spout or bolt).
//the component to run is determined
//from the configuration data passed in by storm
//in the handshake
let runComponent runMap =
    let tag = "runComponent"
    async {
        try 
            let! cfg = Storm.readHandshake()
            setupLogging cfg
            Logging.log tag (sprintf "%A" cfg.Json)
            do! Storm.processPid cfg.PidDir
            let scriptName = cfg.Json?conf?(Storm.FSCOMPONENT).Val
            Logging.log tag (sprintf "running %s" scriptName)
            let func = runMap |> Map.find scriptName
            (func cfg) |> Async.Start
        with ex ->
            //better to exit process if something goes wrong 
            //at this point
            Logging.logex tag ex
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
        Logging.logex "run" ex
        printfn "%s" ex.Message
        1
