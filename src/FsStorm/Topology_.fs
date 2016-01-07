namespace Storm

module Topology =
    open Multilang

    type Next<'a,'t> = 'a->Async<'t option>
    type Consume<'a> = 'a->Async<unit>
    type Emit<'t> = 't->unit
    type TupleId = string
    type StreamId = string
    type ComponentId = string
    type Ack = TupleId->unit
    type Nack = TupleId->unit
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
//        Construct:obj [] -> 't
//        Deconstruct:'t -> obj []
    }

    type Topology<'t> = { 
        Name:string 
        Spouts:Map<ComponentId,Spout<'t>>
        Bolts:Map<ComponentId,Bolt<'t>>
        Streams:Map<StreamId,Stream<'t>>
        Anchors:Map<StreamId,ToAnchors>
    }// with static member Map (t:Topology<'t>,f) = f (t.name,(t.spouts |> Map.toSeq),(t.bolts |> Map.toSeq),(t.streams |> Map.toSeq))

module private Parsers =
    open Topology
    open System.Reflection
    open FSharp.Quotations
    open FSharp.Quotations.Patterns
    open FSharp.Reflection

    let inline raiseUnsupportedExpression expr = failwithf "Unsupported expression: %A" expr

    let (|PipeRight|_|) = function
        | Call (None,op_PipeRight,[Call (None,_,[ValueWithName src;ValueWithName dst]); Call call]) -> Some (src,dst,call)
        | _ -> None
    
    let (|PipeLeft|_|) = function
        | Call (None,op_PipeLeft,[Call call; Call (None,_,[ValueWithName src;ValueWithName dst])]) -> Some (src,dst,call)
        | _ -> None
        
    let (|UnionCase|_|) = function
        | Call (_,_,[Lambda(_, NewUnionCase (case,_));_]) -> Some case
        | Call (_,_,[Lambda(_, IfThenElse (UnionCaseTest(_,case),_,_));_]) -> Some case
        | _ -> None
        
    let (|StreamDef|_|) = function
        | PipeRight ((src,srcT,srcId),(dst,_,dstId),call)
        | PipeLeft ((src,srcT,srcId),(dst,_,dstId),call) -> 
            let spouts = if srcT.GetGenericTypeDefinition() = typedefof<Spout<_>> then [srcId,unbox<Spout<'t>> src] else []
            let bolts = if srcT.GetGenericTypeDefinition() = typedefof<Bolt<_>> then [srcId,unbox<Bolt<'t>> src] else []
            match call with
            | (_,_,[UnionCase case]) -> Some (spouts,(dstId, unbox<Bolt<'t>> dst)::bolts,case.Name,srcId,dstId)
            | _ -> None
        | _ -> None

    let toTopology name = function
        | WithValue (f,_,StreamDef (spouts,bolts,streamId,srcId,dstId)) -> 
            let stream = (srcId,dstId) ||> unbox<Expr<_->_>->ComponentId->ComponentId->Stream<'t>> f
            {Name=name;
             Spouts = Map.ofSeq<ComponentId,Spout<'t>> spouts
             Bolts = Map.ofSeq<ComponentId,Bolt<'t>> bolts
             Streams = Map.ofSeq [streamId, stream] 
             Anchors = Map.ofSeq [streamId, if stream.Anchoring then fun (tid:TupleId) -> [tid] else fun (_:TupleId) -> [] ]}
        | exp -> raiseUnsupportedExpression exp
        
    let formatName name = function
        | None -> name
        | Some pref -> sprintf "%s.%s" pref name

    let rec (|Projection|_|) (bindings:list<string*string>) = function
        | Let (v, PropertyGet (_, prop, _), exp) -> (|Projection|_|) ((v.Name,prop.Name)::bindings) exp
        | PropertyGet (_, prop, _) -> Some [prop.Name]
        | Var v -> bindings |> List.map snd |> Some
        | NewTuple projs -> 
            let bindingsMap = bindings |> Map.ofList
            Some (projs 
                  |> List.choose (|PropertyGet|_|) 
                  |> List.map (fun (v,p,_) -> v |> Option.bind (function Var b -> bindingsMap |> Map.tryFind b.Name | _ -> None) |> formatName p.Name))
        | _ -> None

    let rec parseProjection = function
        | Projection [] names -> names |> List.map (fun name -> None |> formatName name)
        | WithValue (v,ty,exp) -> parseProjection exp
        | Lambda (_,exp) -> parseProjection exp 
        | IfThenElse (UnionCaseTest _,exp,_) -> parseProjection exp 
        | exp -> raiseUnsupportedExpression exp

    let toGroup (select:Expr<'t->'p>) =
        Fields (parseProjection select) //(unbox<'t->'p> v) >> box

    let toSchema expr =
        let no _ = false
        let rec mapNames recurse prefix f (props:PropertyInfo[]) =
            props
            |> Array.collect (fun p -> 
                if recurse p.PropertyType then p.PropertyType.GetProperties() |> mapNames no (Some (prefix |> formatName p.Name)) f
                else [|f prefix p|])
    
//    type TupleState = obj []
//    let toDescriptor (expr:Expr<'c->'t>):TupleDescriptor<'t> = 
//        let rec map prefix (fmt,constr,deconst) (props:PropertyInfo[]):(string []*(TupleState->(obj*TupleState))*((obj*TupleState)->TupleState)) =
//            props
//            |> Array.fold (fun s p -> ) (Array.empty,Array.empty,)
//                if Option.isNone prefix && FSharpType.IsRecord p.PropertyType then 
//                    p.PropertyType.GetProperties() 
//                    |> map (Some p.Name) (fmt,(FSharpValue.PreComputeRecordConstructor p.PropertyType),(FSharpValue.PreComputeRecordReader p.PropertyType))
//                else 
//                    [|fmt prefix p|], (fun vs -> Array.head vs, Array.tail vs), (fun (v,vs) -> vs |> Array.))
//            (fs |> Array.collect (fun (n,_,_) -> n)),
//            (fun vs -> fs |> Array.fold (fun s (_,c,d) -> c vs)),
//            (fun (x,vs) -> (deconst x) |> Array.append vs)
                    
        let rec findCase = function
            | WithValue (v,ty,exp) -> findCase exp
            | Lambda (_,exp) -> findCase exp 
            | NewUnionCase (case, _) -> case
            | IfThenElse (UnionCaseTest (_, case),_,_) -> case
            | exp -> raiseUnsupportedExpression exp

        let case = findCase expr
        case.GetFields() 
        |> mapNames FSharpType.IsRecord None (fun prefix (p:PropertyInfo) -> prefix |> formatName p.Name)
        |> Array.toList

//        let fmt,constr,deconst = (fun prefix (p:PropertyInfo) -> prefix |> formatName p.Name),
//                                 (FSharpValue.PreComputeUnionConstructor case),
//                                 (FSharpValue.PreComputeUnionReader case)
//        let fields,fold,unfold = 
//                case.GetFields() 
//                |> map None (fmt,constr,deconst)
//        {Schema = List.ofArray fields; Construct = (unbox fold) >> fst; Deconstruct = (fun o -> o,[]) >> (unbox unfold)}

    let toStreamName (t:'t) =
        let (uc,_) = FSharpValue.GetUnionFields(t, typeof<'t>)
        uc.Name

module DSL =
    open Multilang
    open Topology
    open Dispatch
    open FSharp.Quotations

    let shell (command : string) (args : string) = Shell(command,args)
    let java (className : string) (args : string list) = Java (className, args)

    let runReliably mkArgs mkAcker next :Spout<'t> =
        {MkComp = fun () -> FuncRef (reliableSpoutLoop mkArgs mkAcker next Parsers.toStreamName)
         Parallelism=1u; 
         Conf = None}

    let runUnreliably mkArgs (next:Next<'a,_>):Spout<'t> =
        {MkComp = fun () -> FuncRef (unreliableSpoutLoop mkArgs next Parsers.toStreamName)
         Parallelism=1u; 
         Conf = None}

    let runBolt mkArgs (consume:Consume<'a>):Bolt<'t> =
        {MkComp = fun toAnchors -> FuncRef (autoAckBoltLoop mkArgs consume toAnchors Parsers.toStreamName)
         Parallelism=1u; 
         Conf = None}
     
    let asSpout<'t> comp:Spout<'t> =
        {Spout.MkComp = (fun _ -> comp); Parallelism=1u; Conf = None}

    let asBolt<'t> comp:Bolt<'t> =
        {Bolt.MkComp = (fun _ -> comp); Parallelism=1u; Conf = None}

    let inline withParallelism parallelism (spec:^s) =
        (^s : (static member WithParallelism : ^s*uint32 -> 's) (spec, uint32 parallelism))

    let inline withConf conf (spec:^s) =
        (^s : (static member WithConf : ^s*Conf -> 's) (spec, conf |> Seq.map (fun (k,v)->(k,string v)) |> Map.ofSeq))

    let shuffle : bool->Expr<_->'t>->ComponentId->ComponentId->Stream<'t> =
        fun anchor case src dst -> 
            {Grouping = Grouping.Shuffle
             Src = src
             Dst = dst
             Anchoring = anchor
             Schema = Parsers.toSchema case}
//             Descriptor = Parsers.toDescriptor case}

    let all : bool->Expr<_->'t>->ComponentId->ComponentId->Stream<'t> =
        fun anchor case src dst -> 
            {Grouping = Grouping.All
             Src = src
             Dst = dst
             Anchoring = anchor
             Schema = Parsers.toSchema case}
    
    let direct : bool->Expr<_->'t>->ComponentId->ComponentId->Stream<'t> =
        fun anchor case src dst -> 
            {Grouping = Grouping.Direct
             Src = src
             Dst = dst
             Anchoring = anchor
             Schema = Parsers.toSchema case}

    let group : bool->Expr<'t->_>->ComponentId->ComponentId->Stream<'t> =
        fun anchor select src dst -> 
            {Grouping = Parsers.toGroup select
             Src = src
             Dst = dst
             Anchoring = anchor
             Schema = Parsers.toSchema select}

//    let inline (!*>) (anchor:bool) (case:Expr<_->'t>) =
//        all.on case anchor

    /// stream, subsequent emits are NOT anchored
    let inline (-->) _ (_:Bolt<'t>) = 
        false

    /// stream and anchor subsequent emits
    let inline (==>) _ (_:Bolt<'t>) = 
        true

    let internal mapJoin map1 map2 =
        Map.fold (fun acc key value -> Map.add key value acc) map1 map2

    type TopologyBuilder(name) =
        member __.Combine(t:Topology<'t>, t2:Topology<'t>) = 
            {Name=t.Name;
             Streams= t.Streams |> mapJoin t2.Streams
             Spouts = t.Spouts |> mapJoin t2.Spouts
             Bolts = t.Bolts |> mapJoin t2.Bolts
             Anchors = t.Anchors |> mapJoin t2.Anchors}

        member __.Yield([<ReflectedDefinition(true)>] expr:Expr<Expr<_->_>->ComponentId->ComponentId->Stream<'t>>):Topology<'t> = 
            Parsers.toTopology name expr
    
        member __.Delay(f) = f()
    
        member __.Zero() = 
            {Name=name;
             Streams= Map.empty<ComponentId,Stream<'t>>
             Spouts = Map.empty<ComponentId,Spout<'t>>
             Bolts = Map.empty<ComponentId,Bolt<'t>>
             Anchors = Map.empty}

    let topology name = 
        TopologyBuilder name

