namespace FsShelter

open System.IO

module private Json =
    open Newtonsoft.Json

    let ofConf = function
        | Some conf -> JsonConvert.SerializeObject conf
        | _ -> null

/// File system utilities
module Includes = 
    let private extSet = [".exe";".dll";".config";".sh";".cmd"] |> Set.ofList
    
    /// filter: include most commmon .NET files only
    let defaultExtensions fileName =
            extSet |> Set.contains (Path.GetExtension(fileName).ToLower())

    /// generate list of files (from*to) using the given filter and the typical build output folder
    /// filter: filename filter function (returns true to include).
    /// binDir: binaries (assemblies) folder.
    let buildOutput filter binDir =
        Directory.GetFiles binDir
        |> Seq.filter filter
        |> Seq.map (fun f -> f, Path.Combine("resources",Path.GetFileName f))

    /// aggreate result of multiple includes
    let aggregate includes binDir =
        includes 
        |> Seq.map (fun f -> f binDir)
        |> Seq.collect id

    open System.IO.Compression
    /// include a content of another JAR file with absolute pathname.
    let jarContents pathname binDir =
        let jarDir = Path.Combine(binDir, "jar")
        
        use jar = ZipFile.OpenRead(pathname)
        jar.Entries 
        |> Seq.where (fun entry -> not <| System.String.IsNullOrEmpty entry.Name)
        |> Seq.iter (fun entry -> 
                        let dst = Path.Combine(jarDir, entry.FullName)
                        Directory.CreateDirectory(Path.GetDirectoryName dst) |> ignore
                        entry.ExtractToFile(dst, true))
            
        Directory.GetFiles(jarDir, "*.*", SearchOption.AllDirectories)
        |> Seq.map (fun f -> f, (f.Replace(jarDir,"").Replace("\\","/").Substring(1)))
    
/// JAR package creation
module Package = 
    open System.IO.Compression

    /// Package the included files into a JAR and return its name
    /// includes: get list of files (src*dst) to include given the specified `binDir`.
    /// binDir: binaries (assemblies) folder.
    let makeJar (includes:string->seq<string*string>) binDir jarPathname = 
        if File.Exists jarPathname then File.Delete jarPathname 
        use jar = ZipFile.Open(jarPathname, ZipArchiveMode.Create)
        
        includes binDir
        |> Seq.iter (jar.CreateEntryFromFile >> ignore)

        jarPathname

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
            read <- inStr.Read(buffer, 0, buffer.Length)
        printfn "uploaded %d bytes into: %s" inStr.Position file
        nimbus.finishFileUpload (file)
        file
    
    /// establish Nimbus (a storm service) connection over thrift protocol and execute the passed action using it
    let withClient nimbusHost nimbusPort cont = 
        use tx = new Thrift.Transport.TSocket(nimbusHost, nimbusPort)
        use txf = new Thrift.Transport.TFramedTransport(tx)
        txf.Open()
        try 
            use tp = new Thrift.Protocol.TBinaryProtocol(txf)
            use client = new Nimbus.Client(tp)
            cont client
        finally
            txf.Close()

/// Converters into Nimbus (Thrift) model
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
    let ofTopology (exeName,args) (topology : Topology<'t>) = // TODO: Make it run on mono properly
        let seqOrEmpty k = Map.tryFind k >> Option.fold Seq.append Seq.empty
        let bySource = topology.Streams |> Map.toSeq |> Seq.groupBy (fun (_,s) -> s.Src) |> Map
        let byDest = topology.Streams |> Map.toSeq |> Seq.groupBy (fun (_,s) -> s.Dst) |> Map
        let bolts = topology.Bolts 
                    |> Map.toList
                    |> Seq.map (fun (cid,cmp) -> toBolt exeName args (bySource |> seqOrEmpty cid) (byDest |> seqOrEmpty cid) (cid,cmp))

        let spouts = topology.Spouts 
                    |> Map.toList
                    |> Seq.map (fun (cid,cmp) -> toSpout exeName args (bySource |> seqOrEmpty cid) (cid,cmp))

        topology.Name,
        StormTopology(Bolts = (bolts |> toDict),
                      Spouts = (spouts |> toDict),
                      State_spouts = Dictionary())

/// Executable startup helpers
module Startup =
    /// make arguments suitable for running shell components on Windows
    let mkWindowsArgs args exe = 
        ("cmd", "/c"::(Path.GetFileName exe)::args)

    /// make arguments suitable for running shell components under Mono
    let mkMonoArgs args exe = 
        ("mono", (Path.GetFileName exe)::args)

