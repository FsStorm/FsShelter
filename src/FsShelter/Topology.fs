namespace FsShelter

/// Topology data model
module Topology =
    open Multilang

    type TupleId = string
    type StreamId = string
    type ComponentId = string
    type ToAnchors = TupleId->TupleId list
    type IO<'t> = (unit->Async<InCommand<'t>>)*(OutCommand<'t>->unit)
    type Runnable<'t> = IO<'t>->Conf->Async<unit>

    type Component<'t> = 
        | FuncRef of Runnable<'t>
        | Shell of command : string * args : string
        | Java of className : string * args : string list

    type Spout<'t> = {
        MkComp:unit->Component<'t>
        Parallelism:uint32
        Conf:Conf option
    } with static member WithConf (s,conf) = {s with Conf = Some conf}
           static member WithParallelism (s,p) = {s with Parallelism = p}

    type Bolt<'t> = {
        MkComp:(StreamId->ToAnchors)->Component<'t>
        Parallelism:uint32
        Conf:Conf option
    } with static member WithConf (s,conf) = {s with Bolt.Conf = Some conf}
           static member WithParallelism (s,p) = {s with Bolt.Parallelism = p}

    type Grouping<'t> = 
        | Shuffle
        | Fields of names:string list
        | All
        | Direct

    type Stream<'t> = {
        Src:ComponentId
        Dst:ComponentId
        Grouping:Grouping<'t>
        Anchoring:bool
        Schema:string list
    }

    type Topology<'t> = { 
        Name:string 
        Spouts:Map<ComponentId,Spout<'t>>
        Bolts:Map<ComponentId,Bolt<'t>>
        Streams:Map<StreamId,Stream<'t>>
        Anchors:Map<StreamId,ToAnchors>
    }

/// DU/Stream schema functions
module TupleSchema =
    open System.Reflection
    open FSharp.Reflection

    let private forRecords = Option.Some >> Option.filter FSharpType.IsRecord 
    let private no _ = None

    let formatName name = function
        | None -> name
        | Some pref -> sprintf "%s.%s" pref name

    let toNames (case:UnionCaseInfo) =
        let rec mapNames recurse prefix f (props:PropertyInfo[]) =
            props
            |> Array.collect (fun p -> 
                match recurse p.PropertyType with
                | Some properties -> properties |> mapNames no (Some (prefix |> formatName p.Name)) f
                | _ -> [|f prefix p|])

        case.GetFields() 
            |> mapNames (forRecords >> Option.map FSharpType.GetRecordFields) 
                        None 
                        (fun prefix (p:PropertyInfo) -> prefix |> formatName p.Name)
            |> Array.toList

    let toStreamName<'t> :'t->string = 
        let reader = FSharpValue.PreComputeUnionTagReader typeof<'t>
        let names = FSharpType.GetUnionCases typeof<'t> |> Array.map (fun case -> case.Name)
        reader >> names.GetValue >> unbox

    type FieldReader = System.Type->obj
    type FieldWriter = obj->unit

    let toTupleRW<'t> (case:UnionCaseInfo) =
        let rec mapRW recurse (constr:obj[]->obj,deconst:obj->obj[]) (props:PropertyInfo[]):(FieldReader->unit->obj)*(FieldWriter->obj->unit) =
            let mapSingle (p:PropertyInfo) = 
                match recurse p.PropertyType with
                | Some (properties, fs) -> properties |> mapRW no fs
                | _ -> (fun r ()-> r p.PropertyType), fun w v -> w v
            let acc = props |> Array.map mapSingle
            (fun r () -> acc |> Array.map (fun (pr,_) -> pr r ()) |> constr),
             fun w o -> deconst o |> Array.zip (acc |> Array.map snd) |> Array.iter (fun (f,v) -> f w v)
                    
        let (read,write) =
            case.GetFields()
            |> mapRW (forRecords >> Option.map (fun t -> FSharpType.GetRecordFields t,((FSharpValue.PreComputeRecordConstructor t),(FSharpValue.PreComputeRecordReader t))))
                     ((FSharpValue.PreComputeUnionConstructor case),(FSharpValue.PreComputeUnionReader case))
        
        (fun r () -> read r () |> unbox<'t>),write

    let mapSchema<'t> () =
        FSharpType.GetUnionCases typeof<'t> |> Array.map (fun case -> case.Name,(toTupleRW<'t> case))