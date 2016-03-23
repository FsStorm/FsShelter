namespace FsShelter

module private Parsers =
    open Topology
    open TupleSchema
    open FSharp.Quotations
    open FSharp.Quotations.Patterns

    let inline raiseUnsupportedExpression expr = failwithf "Unsupported expression: %A" expr

    let (|PipeRight|_|) = function
        | Call (None,op_PipeRight,[Call (None,_,[ValueWithName src;ValueWithName dst]); call]) -> Some (src,dst,call)
        | _ -> None
    
    let (|PipeLeft|_|) = function
        | Call (None,op_PipeLeft,[call; Call (None,_,[ValueWithName src;ValueWithName dst])]) -> Some (src,dst,call)
        | _ -> None
        
    let rec (|UnionCase|_|) = function
        | Call (_,_,[UnionCase case]) -> Some case
        | Call (_,_,[UnionCase case;_]) -> Some case
        | Lambda(_, UnionCase case) -> Some case
        | Let (_,_,UnionCase case) -> Some case
        | NewUnionCase (case,_) -> Some case
        | IfThenElse (UnionCaseTest(_,case),_,_) -> Some case
        | _ -> None
        
    let (|StreamDef|_|) = function
        | PipeRight ((src,srcT,srcId),(dst,_,dstId),UnionCase case)
        | PipeLeft ((src,srcT,srcId),(dst,_,dstId),UnionCase case) -> 
            let spouts = if srcT.GetGenericTypeDefinition() = typedefof<Spout<_>> then [srcId,unbox<Spout<'t>> src] else []
            let bolts = if srcT.GetGenericTypeDefinition() = typedefof<Bolt<_>> then [srcId,unbox<Bolt<'t>> src] else []
            Some (spouts,(dstId, unbox<Bolt<'t>> dst)::bolts,case.Name,srcId,dstId)
        | _ -> None

    let toTopology name = function
        | WithValue (f,_,StreamDef (spouts,bolts,streamId,srcId,dstId)) -> 
            let stream = (srcId,dstId) ||> unbox<ComponentId->ComponentId->Stream<'t>> f
            {Name=name;
             Spouts = Map.ofSeq<ComponentId,Spout<'t>> spouts
             Bolts = Map.ofSeq<ComponentId,Bolt<'t>> bolts
             Streams = Map.ofSeq [streamId, stream] 
             Anchors = Map.ofSeq [streamId, if stream.Anchoring then fun (tid:TupleId) -> [tid] else fun (_:TupleId) -> [] ]}
        | exp -> raiseUnsupportedExpression exp
        
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

    let rec findCase = function
        | WithValue (_,_,exp) -> findCase exp
        | UnionCase case -> case
        | exp -> raiseUnsupportedExpression exp

/// Embedded DSL for defining the topologies
[<System.Diagnostics.CodeAnalysis.SuppressMessage("NameConventions", "TypeNamesMustBePascalCase")>]
[<System.Diagnostics.CodeAnalysis.SuppressMessage("NameConventions", "MemberNamesMustBePascalCase")>]
module DSL =
    open Multilang
    open Topology
    open Dispatch
    open FSharp.Quotations

    /// spout function signature
    type Next<'a,'t> = 'a->Async<'t option> 
    /// bolt function signature
    type Consume<'a> = 'a->Async<unit>
    /// emit signature
    type Emit<'t> = 't->unit
    /// ack signature
    type Ack = TupleId->unit
    /// nack signature
    type Nack = TupleId->unit
    /// ack/nack tuple
    type Acker = Ack*Nack

    /// wrap (external) shell component definition
    let shell (command : string) (args : string) = Shell(command,args)
    /// wrap (Storm native) java component definition
    let java (className : string) (args : string list) = Java (className, args)

    /// define a reliable spout
    let runReliableSpout mkArgs (mkAcker:_->Acker) (next:Next<_,_*'t>) :Spout<'t> =
        {MkComp = fun () -> FuncRef (reliableSpoutLoop mkArgs mkAcker next TupleSchema.toStreamName<'t>)
         Parallelism=1u; 
         Conf = None}

    /// define spout with no processing guarantees
    let runSpout mkArgs (next:Next<_,'t>):Spout<'t> =
        {MkComp = fun () -> FuncRef (unreliableSpoutLoop mkArgs next TupleSchema.toStreamName<'t>)
         Parallelism=1u; 
         Conf = None}

    /// define a bolt
    let runBolt mkArgs (consume:Consume<_>):Bolt<'t> =
        {MkComp = fun toAnchors -> FuncRef (autoAckBoltLoop mkArgs consume toAnchors TupleSchema.toStreamName<'t>)
         Parallelism=1u; 
         Conf = None}

    /// define a spout for a (external) shell or java component
    let asSpout<'t> comp:Spout<'t> =
        {Spout.MkComp = (fun _ -> comp); Parallelism=1u; Conf = None}

    /// define a bolt for a (external) shell or java component
    let asBolt<'t> comp:Bolt<'t> =
        {Bolt.MkComp = (fun _ -> comp); Parallelism=1u; Conf = None}

    /// override default parallelism
    let inline withParallelism parallelism (spec:^s) =
        (^s : (static member WithParallelism : ^s*uint32 -> 's) (spec, uint32 parallelism))

    /// supply component configuration/overrides
    let inline withConf conf (spec:^s) =
        (^s : (static member WithConf : ^s*Conf -> 's) (spec, conf |> Seq.map (fun (k,v)->(k,box v)) |> Map.ofSeq))

    /// define shuffle grouping
    type shuffle =
        static member on ([<ReflectedDefinition(true)>] case:Expr<_->'t>):bool->ComponentId->ComponentId->Stream<'t> =
            fun anchor src dst -> 
                {Grouping = Grouping.Shuffle
                 Src = src
                 Dst = dst
                 Anchoring = anchor
                 Schema = Parsers.findCase case |> TupleSchema.toNames}

    /// define all grouping
    type all =
        static member on ([<ReflectedDefinition(true)>] case:Expr<_->'t>):bool->ComponentId->ComponentId->Stream<'t> =
            fun anchor src dst -> 
                {Grouping = Grouping.All
                 Src = src
                 Dst = dst
                 Anchoring = anchor
                 Schema = Parsers.findCase case |> TupleSchema.toNames}

    /// define direct grouping
    type direct =
        static member on ([<ReflectedDefinition(true)>] case:Expr<_->'t>):bool->ComponentId->ComponentId->Stream<'t> =
            fun anchor src dst -> 
                {Grouping = Grouping.Direct
                 Src = src
                 Dst = dst
                 Anchoring = anchor
                 Schema = Parsers.findCase case |> TupleSchema.toNames}

    /// define fields grouping
    type group =
        static member by ([<ReflectedDefinition(true)>] select:Expr<'t->'p>):bool->ComponentId->ComponentId->Stream<'t> =
            fun anchor src dst -> 
                {Grouping = Parsers.toGroup select
                 Src = src
                 Dst = dst
                 Anchoring = anchor
                 Schema = Parsers.findCase select |> TupleSchema.toNames}

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

    /// TopologyBuilder computation expression
    type TopologyBuilder(name) =
        member __.Combine(t:Topology<'t>, t2:Topology<'t>) = 
            {Name=t.Name;
             Streams= t.Streams |> mapJoin t2.Streams
             Spouts = t.Spouts |> mapJoin t2.Spouts
             Bolts = t.Bolts |> mapJoin t2.Bolts
             Anchors = t.Anchors |> mapJoin t2.Anchors}

        member __.Yield([<ReflectedDefinition(true)>] expr:Expr<ComponentId->ComponentId->Stream<'t>>):Topology<'t> = 
            Parsers.toTopology name expr
    
        member __.Delay(f) = f()
    
        member __.Zero() = 
            {Name=name;
             Streams= Map.empty<ComponentId,Stream<'t>>
             Spouts = Map.empty<ComponentId,Spout<'t>>
             Bolts = Map.empty<ComponentId,Bolt<'t>>
             Anchors = Map.empty}

    /// topology builder instance
    let topology name = 
        TopologyBuilder name

