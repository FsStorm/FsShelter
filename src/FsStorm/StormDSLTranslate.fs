module StormDSLTranslate
open StormThrift
open System.Collections.Generic
open System
open FsJson

let makeComponent exeName optionalArgs compSpec =
    let c = new ComponentObject()
    match compSpec with
    | StormDSL.Java (className,args) -> 
        let j = new JavaObject()
        j.Full_class_name <- className
        j.Args_list <- 
            let xs = args |> List.map (fun l -> let a = new JavaObjectArg() in a.String_arg <- l;a )
            let axs = new System.Collections.Generic.List<JavaObjectArg>(xs |> List.toArray)
            axs
        c.Java_object <- j
    | StormDSL.Shell (prog,script) -> 
        let sh = ShellComponent()
        sh.Execution_command <- prog
        sh.Script <- script
        c.Shell <- sh
    | StormDSL.Local ref ->
        let sh = ShellComponent()
        sh.Execution_command <- exeName
        sh.Script <- match optionalArgs with [] -> "" | xs -> String.Join(" ", xs)
        c.Shell <- sh
    c

let makeGlobalId streamId = 
    let gid = new GlobalStreamId()
    match streamId with
    | StormDSL.DefaultStream id ->
        gid.ComponentId <- id
        gid.StreamId    <- "default"
    | StormDSL.Stream (id,streamName) ->
        gid.ComponentId <- id
        gid.StreamId    <- streamName
    gid

let makeGrouping grp =
    let grpng = new Grouping()
    match grp with
    | StormDSL.Shuffle        -> grpng.Shuffle <- new NullStruct()  
    | StormDSL.Fields fields  -> grpng.Fields <- new List<string>(fields |> List.toArray)
    | StormDSL.All            -> grpng.All <- new NullStruct()
    | StormDSL.Direct         -> grpng.Direct <- new NullStruct()
    grpng

let makeStreamInfo stream =
    let si = new StreamInfo()
    match stream with
    | StormDSL.Default fields ->
        si.Output_fields <- new List<string>(fields)
        "default",si
    | StormDSL.Named (name,fields) ->
        si.Output_fields <- new List<string>(fields)
        name,si

let translate exeName optionalArgs  (topology:StormDSL.Topology) : StormThrift.StormTopology =
    StormDSLValidate.validate topology
    let tp = new StormTopology()
    let bolts = topology.Bolts |> List.map (fun b ->
        let blt = new Bolt()
        blt.Bolt_object <- makeComponent exeName optionalArgs b.Bolt
        let cmn = new ComponentCommon()
        //inputs
        let inputs = b.Inputs |> Seq.map (fun (id,grp) -> makeGlobalId id, makeGrouping grp )
        let dinputs = new Dictionary<GlobalStreamId,Grouping>()
        for id,grp in inputs do dinputs.Add(id,grp)
        //outputs
        let outstrms = b.Outputs |> List.map makeStreamInfo
        let doutstrms = new Dictionary<string,StreamInfo>()
        for id,si in outstrms do doutstrms.Add(id,si)
        //common
        cmn.Parallelism_hint <- b.Parallelism
        cmn.Inputs <- dinputs
        cmn.Streams <- doutstrms
        cmn.Json_conf <- FsJson.serialize b.Config
        //bolt properties
        blt.Common <- cmn
        b.Id,blt
        )
    let spouts = topology.Spouts |> List.map (fun s ->
        let spt = new SpoutSpec()
        spt.Spout_object <- makeComponent exeName optionalArgs s.Spout
        let cmn = new ComponentCommon()
        //outputs
        let outstrms = s.Outputs |> List.map makeStreamInfo
        let doutstrms = new Dictionary<string,StreamInfo>()
        for id,si in outstrms do doutstrms.Add(id,si)
        //common
        cmn.Parallelism_hint <- s.Parallelism
        cmn.Inputs <- new Dictionary<GlobalStreamId,Grouping>()
        cmn.Streams <- doutstrms
        cmn.Json_conf <- FsJson.serialize s.Config
        //spout properties
        spt.Common <- cmn
        s.Id,spt)
    let bd = new Dictionary<string,Bolt>()
    for id,b in bolts do bd.Add(id,b)
    let sd = new Dictionary<string,SpoutSpec>()
    for id,s in spouts do sd.Add(id,s)
    tp.Bolts <- bd
    tp.Spouts <- sd
    tp.State_spouts <- new Dictionary<string,StateSpoutSpec>()
    tp

   
    
