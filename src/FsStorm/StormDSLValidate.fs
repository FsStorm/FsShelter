module StormDSLValidate
open FsJson
//validation of storm topologies built 
//with StormDSL

let ensureUniqueBoltInputIds (topology:StormDSL.Topology) =
    //bolt input stream references should be unique
    for b in topology.Bolts do
        let iids = b.Inputs |> List.map fst
        let id,count = iids |> Seq.countBy (fun x -> x) |> Seq.maxBy snd
        if count > 1 then failwithf "*** repeated id %A in inputs for bolt id %s" id b.Id

let private ensureUniqueComponentIds (topology:StormDSL.Topology) =
    let ids = 
        seq { 
            for s in topology.Spouts -> s.Id.Trim()
            for b in topology.Bolts  -> b.Id.Trim()
         }
    let (id,max) = ids |> Seq.countBy (fun x -> x) |> Seq.maxBy snd
    if max > 1 then failwithf "*** duplicate component id %s" id

let private ensureCorrectBoltRefs (topology:StormDSL.Topology) =
    //bolts refer to outputs of other spouts and bolts
    //ensure these are correct
    let boltMap       = topology.Bolts  |> Seq.map (fun b -> b.Id, b) |> Map.ofSeq
    let sptMap        = topology.Spouts |> Seq.map (fun s -> s.Id, s) |> Map.ofSeq
    let allBoltInputs = topology.Bolts  |> Seq.collect (fun b -> b.Inputs |> Seq.map (fun (k,v) -> b.Id,k,v))
    let ensureComponent referringId referencedId = 
        if not (boltMap.ContainsKey referencedId || sptMap.ContainsKey referencedId) then 
            failwithf "*** Bolt %s references another component with id %s which was not found" referringId referencedId
    //validate references at the component level
    let validateComponentReference = function
        | boltId,StormDSL.DefaultStream referencedComponent,_ -> ensureComponent boltId referencedComponent
        | boltId,StormDSL.Stream (referencedComponent,_),_    -> ensureComponent boltId referencedComponent
    allBoltInputs |> Seq.iter validateComponentReference
    //match fields between outputs and inputs
    let outputStreams streamId =
        let componentId = match streamId with StormDSL.DefaultStream id | StormDSL.Stream (id,_) -> id
        match sptMap |> Map.tryFind componentId with 
        | Some s -> s.Outputs 
        | _ -> 
            match boltMap |> Map.tryFind componentId with 
            | Some b -> b.Outputs 
            | _ -> []
    let outputFields outputs = function
        | StormDSL.DefaultStream _  -> outputs |> List.tryPick (function StormDSL.Default fields -> Some fields | _ -> None)
        | StormDSL.Stream (_,sid1)  -> outputs |> List.tryPick (function StormDSL.Named (sid2,fs) when sid1=sid2 -> Some fs | _ -> None)
    let isSubsetOf superSet subSet = Set.isSubset (set subSet) (set superSet)
    let getGroupingFields = function
        | streamId, StormDSL.Fields fields -> Some fields, (outputStreams streamId |> outputFields) streamId
        | _ -> None,None
    let fieldMatch boltId = function
        | Some l1, Some l2 -> if l1 |> isSubsetOf l2 |> not then failwithf "*** Bolt %s input fields %A don't match output %A" boltId l1 l2
        | None, None -> ()
        | _, _       -> failwithf "*** at least one reference for Bolt %s is not correct" boltId
    allBoltInputs |> Seq.iter (fun (boltId,streamRef,grouping) -> getGroupingFields (streamRef,grouping) |> fieldMatch boltId)


let validateConfigs (topology:StormDSL.Topology) =
    let configs =
        seq {
                for s in topology.Spouts -> s.Id,s.Config
                for b in topology.Bolts -> b.Id,b.Config
            }
    configs |> Seq.iter (fun (id,cfg) ->
        match cfg with
        | JsonNull | JsonObject _ -> ()
        | _ -> failwithf "*** error in component %s config - should be either a JsonObject or JsonNull" id
        )

let validate (topology:StormDSL.Topology) =
    ensureUniqueComponentIds topology
    ensureUniqueBoltInputIds topology
    ensureCorrectBoltRefs    topology
    validateConfigs          topology
