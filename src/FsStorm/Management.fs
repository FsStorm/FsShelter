namespace Storm

open System.IO

module private Json =
    open Newtonsoft.Json

    let ofConf = function
        | Some conf -> JsonConvert.SerializeObject conf
        | _ -> null

/// File system utilities
module Package = 
    open System.IO.Compression
    let private extSet = [".exe";".dll";".config";".sh"] |> Set.ofList
    
    /// filter: ".exe",".dll",".config",".sh"
    let defaultExtensions fileName =
         extSet |> Set.contains (Path.GetExtension(fileName).ToLower())
    
    /// package the appropriate files from the 'binDir'
    /// into a JAR and return its name
    let makeJar filter binDir = 
        let jarDir = Path.Combine(binDir, "resources")
        if not <| Directory.Exists(jarDir) then Directory.CreateDirectory(jarDir) |> ignore
        Directory.GetFiles(jarDir) |> Seq.iter (File.Delete)
        Directory.GetFiles(binDir)
        |> Seq.filter filter
        |> Seq.map (fun f -> f, Path.Combine(jarDir,Path.GetFileName(f)))
        |> Seq.iter File.Copy
        let jarFn = Path.Combine(binDir, Path.GetFileName(binDir) + ".jar")
        if File.Exists jarFn then File.Delete jarFn
        ZipFile.CreateFromDirectory(jarDir, jarFn, CompressionLevel.Optimal, true)
        jarFn

/// Nimbus client module
module Nimbus = 
    open StormThrift

    let [<Literal>] DefaultPort = 6627
    
    /// sumit a topology
    let submit (nimbus:Nimbus.Client) conf uploadedJarLocation (name,topology:StormThrift.StormTopology) = 
        nimbus.submitTopology(name, uploadedJarLocation, Json.ofConf conf, topology)

    /// kill a running topology
    let kill (nimbus:Nimbus.Client) name = 
        nimbus.killTopology name
    
    /// upload the packaged jar to Nimbus and return the location on the server
    let uploadJar (nimbus : Nimbus.Client) jarFile = 
        let file = nimbus.beginFileUpload()
        use inStr = File.OpenRead(jarFile)
        let chunkSz = 307200
        let buffer = Array.zeroCreate (chunkSz)
        let mutable read = inStr.Read(buffer, 0, buffer.Length)
        while read > 0 do
            nimbus.uploadChunk (file, buffer.[0..read - 1])
            printfn "uploaded %d bytes" read
            read <- inStr.Read(buffer, 0, buffer.Length)
        nimbus.finishFileUpload (file)
        file
    
    /// establish Nimbus (a storm service) connection over thrift protocol and execute the passed action using it
    let withClient nimbus_host nimbus_port cont = 
        use tx = new Thrift.Transport.TSocket(nimbus_host, nimbus_port)
        use txf = new Thrift.Transport.TFramedTransport(tx)
        txf.Open()
        try 
            use tp = new Thrift.Protocol.TBinaryProtocol(txf)
            use client = new Nimbus.Client(tp)
            cont client
        finally
            txf.Close()

//check if running on mono
//    let isMono() = Type.GetType("Mono.Runtime") <> null
module ThriftModel = 
    open Topology
    open StormThrift
    open System
    open System.Collections.Generic
    
    let private toComponent exeName optionalArgs = function
        | Java (className,args) -> 
            let xs = args |> List.map (fun l -> JavaObjectArg(String_arg = l))
            ComponentObject(Java_object = JavaObject(Full_class_name = className,
                                                     Args_list = List(xs)))
        | Shell (prog,script) -> 
            ComponentObject(Shell = ShellComponent(Execution_command = prog,
                                                   Script = script))
        | FuncRef _ ->
            ComponentObject(Shell = ShellComponent(Execution_command = exeName,
                                                   Script = match optionalArgs with [] -> "" | xs -> String.Join(" ", xs)))
    
    let private toGrouping = function
        | Shuffle        -> Grouping(Shuffle = NullStruct())
        | Fields fields  -> Grouping(Fields = List(fields))
        | All            -> Grouping(All = NullStruct())
        | Direct         -> Grouping(Direct = NullStruct())

    let private toDict (s:seq<_*_>) = System.Linq.Enumerable.ToDictionary(s, fst, snd)
    
    let private toStreamInfo stream = 
        (stream.Schema |> List, match stream.Grouping with | Direct -> true | _ -> false)
        |> StreamInfo
    
    let private toBolt exe optionalArgs outs ins (cid,bolt) =
        let noop _ _ = []
        let inputs = ins |> Seq.map (fun (sid, s) -> (GlobalStreamId(s.Src, sid), toGrouping s.Grouping)) |> toDict
        let outputs = outs |> Seq.map (fun (sid, s) -> (sid, toStreamInfo s)) |> toDict
        cid,Bolt(toComponent exe optionalArgs (bolt.MkComp noop),
                ComponentCommon(Parallelism_hint = int bolt.Parallelism,
                                Inputs = inputs,
                                Streams = outputs,
                                Json_conf = Json.ofConf bolt.Conf))

    let private toSpout exe optionalArgs outs (cid:string,spout:Spout<_>) =
        let outputs = outs |> Seq.map (fun (sid, stm) -> (sid, toStreamInfo stm)) |> toDict
        cid,SpoutSpec(toComponent exe optionalArgs (spout.MkComp ()),
                     ComponentCommon(Parallelism_hint = int spout.Parallelism,
                                     Inputs = Dictionary(),
                                     Streams = outputs,
                                     Json_conf = Json.ofConf spout.Conf))

    /// Convert topology to Nimbus/thrift representation
    let ofTopology optionalArgs exeName (topology : Topology<'t>) = // TODO: Make it run on mono properly
        let seqOrEmpty k = Map.tryFind k >> Option.fold Seq.append Seq.empty
        let bySource = topology.Streams |> Map.toSeq |> Seq.groupBy (fun (_,s) -> s.Src) |> Map
        let byDest = topology.Streams |> Map.toSeq |> Seq.groupBy (fun (_,s) -> s.Dst) |> Map
        let bolts = topology.Bolts 
                    |> Map.toList
                    |> Seq.map (fun (cid,cmp) -> toBolt exeName optionalArgs (bySource |> seqOrEmpty cid) (byDest |> seqOrEmpty cid) (cid,cmp))

        let spouts = topology.Spouts 
                    |> Map.toList
                    |> Seq.map (fun (cid,cmp) -> toSpout exeName optionalArgs (bySource |> seqOrEmpty cid) (cid,cmp))

        topology.Name,
        StormTopology(Bolts = (bolts |> toDict),
                      Spouts = (spouts |> toDict),
                      State_spouts = Dictionary())

