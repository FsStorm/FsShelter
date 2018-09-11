namespace FsShelter

/// Identifies a stream that should be "unfolded" into individual substream for each nested DU case.
/// Only applies to single-argument constructor case that takes another DU case as nested.
type NestedStreamAttribute() =
    inherit System.Attribute()

/// DU/Stream schema mapping functions
module TupleSchema =
    open System.Reflection
    open FSharp.Reflection

    let private forRecords = Option.Some >> Option.filter FSharpType.IsRecord
    let private no _ = None

    /// Format a field name
    let formatName name = function
        | None -> name
        | Some pref -> sprintf "%s.%s" pref name

    /// Map a case to field names
    let toNames (case:UnionCaseInfo,_) =
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
            
    /// format a case using its DisplayNameAttribute or name
    let formatNestedCaseName (outer:UnionCaseInfo option) (inner:UnionCaseInfo) =
        let fmt = 
            outer
            |> Option.map (fun p ->
                p.GetCustomAttributes(typeof<NestedStreamAttribute>) 
                |> Array.tryHead
                |> Option.map (fun _-> sprintf "%s+%s" p.Name)
                |> Option.defaultValue (fun _ -> p.Name))
            |> Option.defaultValue id
        inner.GetCustomAttributes(typeof<System.ComponentModel.DisplayNameAttribute>) 
        |> Array.tryHead
        |> Option.bind (fun a -> a :?> System.ComponentModel.DisplayNameAttribute |> Option.ofObj)
        |> Option.map (fun a -> a.DisplayName)
        |> Option.defaultValue (fmt inner.Name)
    
    /// format a case using its DisplayNameAttribute or name
    let formatInnerCaseName = formatNestedCaseName None

    /// Map a case to stream name
    let toStreamName<'t> :'t->string =
        let outerTagReader = FSharpValue.PreComputeUnionTagReader typeof<'t>
        let innerTagReader = 
            FSharpType.GetUnionCases typeof<'t>
            |> Array.mapi (fun i outerCase ->
                match outerCase.GetFields() with
                | [|inner|] when FSharpType.IsUnion inner.PropertyType ->
                    let tagReader = FSharpValue.PreComputeUnionTagReader inner.PropertyType
                    let toFields = FSharpValue.PreComputeUnionReader outerCase
                    fun o -> i, toFields o |> Array.head |> tagReader
                | _  -> fun _ -> i, 0
            )
        let names =
            FSharpType.GetUnionCases typeof<'t>
            |> Array.collect (fun outerCase -> 
                match outerCase.GetFields() with
                | [|inner|] when FSharpType.IsUnion inner.PropertyType ->
                    //let rdr = FSharpValue.PreComputeUnionReader outerCase
                    let innerCases = FSharpType.GetUnionCases inner.PropertyType
                    innerCases
                    |> Array.map (fun c ->
                        (outerCase.Tag,c.Tag), formatNestedCaseName (Some outerCase) c) 
                | _  -> ((outerCase.Tag,0), formatInnerCaseName outerCase) |> Array.singleton)
            |> Map.ofArray

        fun o -> names |> Map.find (o |> innerTagReader.[outerTagReader o])

    let mkTick<'t> () : 't option = 
        FSharpType.GetUnionCases typeof<'t> |> Array.tryFind (fun c -> formatInnerCaseName c = "__tick")
        |> Option.map FSharpValue.PreComputeUnionConstructor
        |> Option.map (fun tick -> tick [||] |> unbox)
        

    /// Tuple field reader
    type FieldReader = System.Type->obj
    /// Tuple field writer
    type FieldWriter = obj->unit
    
    /// Map a [nested] union case to reader/writer functions
    let toTupleRW<'t> (case:UnionCaseInfo) (nested:UnionCaseInfo option) =
        let recurser = (forRecords >> Option.map (fun t -> FSharpType.GetRecordFields t,((FSharpValue.PreComputeRecordConstructor t),(FSharpValue.PreComputeRecordReader t))))
        let rec mapRW nest recurse (constr:obj[]->obj,deconst:obj->obj[]) (props:PropertyInfo[]) : (FieldReader->unit->obj)*(FieldWriter->obj->unit) =
            let mapSingle (p:PropertyInfo) = 
                match nest, recurse p.PropertyType with
                | None, Some (properties, fs) -> properties |> mapRW None no fs
                | Some (c:UnionCaseInfo), _ -> c.GetFields() |> mapRW None recurser ((FSharpValue.PreComputeUnionConstructor c),(FSharpValue.PreComputeUnionReader c))
                | _,_ -> (fun r ()-> r p.PropertyType), fun w v -> w v
            let acc = props |> Array.map mapSingle
            (fun r () -> acc |> Array.map (fun (pr,_) -> pr r ()) |> constr),
             fun w o -> deconst o |> Array.zip (acc |> Array.map snd) |> Array.iter (fun (f,v) -> f w v)

        let (read,write) =
            case.GetFields()
            |> mapRW nested recurser ((FSharpValue.PreComputeUnionConstructor case),(FSharpValue.PreComputeUnionReader case))
        
        (fun r () -> read r () |> unbox<'t>),write

    /// Map descriminated union cases to reader*writer functions
    let mapSchema<'t> () =
        let rec map outerCase fmt t =
            FSharpType.GetUnionCases t 
            |> Array.collect (fun case -> 
                let hasNestedAttr = case.GetCustomAttributes(typeof<NestedStreamAttribute>) |> Array.isEmpty |> not
                if case.DeclaringType.IsGenericType && hasNestedAttr then
                    case.DeclaringType.GenericTypeArguments // TODO Can something else be used here?
                    |> Array.collect (map (Some case) (formatNestedCaseName <| Some case))
                else
                    match outerCase with
                    | Some outer -> [|(fmt case),(Some case |> toTupleRW<'t> outer)|]
                    | None -> [|(fmt case),(None |> toTupleRW<'t> case)|])
        map None formatInnerCaseName typeof<'t>

module private Parsers =
    open Topology
    open TupleSchema
    open FSharp.Quotations
    open FSharp.Quotations.Patterns

    let inline raiseUnsupportedExpression expr = failwithf "Unsupported expression: %A" expr

    let (|PipeRight|_|) = function
        | Call (None,op_PipeRight,[Call (None,_,[PropertyGet (src_o,src_p,_); PropertyGet (dst_o,dst_p,_)]); call]) -> 
            Some ((src_p.GetValue src_o, src_p.PropertyType, src_p.Name),
                  (dst_p.GetValue dst_o, dst_p.PropertyType, dst_p.Name),
                  call)
        | Call (None,op_PipeRight,[Call (None,_,[ValueWithName src;ValueWithName dst]); call]) -> 
            Some (src,dst,call)
        | Call (None,op_PipeRight,[Call (None,_,[Call (src_o,src_p,_); Call (dst_o,dst_p,_)]); call]) -> 
            Some ((src_p.Invoke(src_o, [||]), src_p.ReturnType, src_p.Name),
                  (dst_p.Invoke(dst_o, [||]), dst_p.ReturnType, dst_p.Name),
                  call)
        | Call (None,op_PipeRight,[Call (None,_,[Call (src_o,src_p,_); ValueWithName dst]); call]) -> 
            Some ((src_p.Invoke(src_o, [||]), src_p.ReturnType, src_p.Name),
                  dst,
                  call)
        | _ -> None
    
    let (|PipeLeft|_|) = function
        | Call (None,op_PipeLeft,[call; Call (None,_,[PropertyGet (dst_o,dst_p,_); PropertyGet (src_o,src_p,_)])]) -> 
            Some ((src_p.GetValue src_o, src_p.PropertyType, src_p.Name),
                  (dst_p.GetValue dst_o, dst_p.PropertyType, dst_p.Name),
                  call)
        | Call (None,op_PipeLeft,[call; Call (None,_,[ValueWithName dst;ValueWithName src])]) -> 
            Some (src,dst,call)
        | Call (None,op_PipeLeft,[Call (None,_,[Call (src_o,src_p,_); Call (dst_o,dst_p,_)]); call]) -> 
            Some ((src_p.Invoke(src_o, [||]), src_p.ReturnType, src_p.Name),
                  (dst_p.Invoke(dst_o, [||]), dst_p.ReturnType, dst_p.Name),
                  call)
        | Call (None,op_PipeLeft,[Call (None,_,[Call (src_o,src_p,_); ValueWithName dst]); call]) -> 
            Some ((src_p.Invoke(src_o, [||]), src_p.ReturnType, src_p.Name),
                  dst,
                  call)
        | _ -> None
        
    let rec (|UnionCase|_|) = fun arg ->
        match arg with
        | NewUnionCase (case,_) -> Some (case,None)
        | Call (None,compose,[UnionCase (case,_); UnionCase (pre,_)]) when compose.Name = "op_ComposeRight" -> Some (case,Some pre)
        | Call (None,compose,[UnionCase (pre,_); UnionCase (case,_)]) when compose.Name = "op_ComposeLeft" -> Some (case,Some pre)
        | Call (_,_,[UnionCase (case,pre)]) -> Some (case,pre)
        | Call (_,_,[UnionCase (case,pre);_]) -> Some (case,pre)
        | Lambda(_, UnionCase (case,pre)) -> Some (case,pre)
        | Let (_,_,UnionCase (case,pre)) -> Some (case,pre)
        | IfThenElse (UnionCaseTest(_,pre),UnionCase (case,_),_) -> Some (case, Some pre)
        | IfThenElse (UnionCaseTest(_,case),_,_) -> Some (case,None)
        | _ -> None
        
    let (|StreamDef|_|) = fun e ->
        match e with
        | PipeRight ((src,srcT,srcId),(dst,_,dstId),UnionCase (case,pre))
        | PipeLeft ((src,srcT,srcId),(dst,_,dstId),UnionCase (case,pre)) -> 
            let spouts = if srcT.GetGenericTypeDefinition() = typedefof<Spout<_>> then [srcId,unbox<Spout<'t>> src] else []
            let bolts = if srcT.GetGenericTypeDefinition() = typedefof<Bolt<_>> then [srcId,unbox<Bolt<'t>> src] else []
            Some (spouts,(dstId, unbox<Bolt<'t>> dst)::bolts,formatNestedCaseName pre case,srcId,dstId)
        | _ -> None

    let toTopology name = 
        function
        | WithValue (f,_,StreamDef (spouts,bolts,streamId,srcId,dstId)) -> 
            let stream = (srcId,dstId) ||> unbox<ComponentId->ComponentId->Stream<'t>> f
            { Name=name;
              Spouts = Map.ofSeq<ComponentId,Spout<'t>> spouts
              Bolts = Map.ofSeq<ComponentId,Bolt<'t>> bolts
              Streams = Map.ofSeq [((srcId,streamId),dstId), stream] 
              Anchors = Map.ofSeq [(srcId,streamId), if stream.Anchoring then fun (tid:TupleId) -> [tid] else fun (_:TupleId) -> [] ]
              Conf = Map.empty }
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
        | Lambda (_,exp) -> parseProjection exp 
        | IfThenElse (UnionCaseTest _,exp,_) -> parseProjection exp 
        | exp -> raiseUnsupportedExpression exp

    let toGroup (select:Expr<'t->'p>) =
        let (f,names) =
            match select with
            | WithValue (v,_,exp) -> v :?> ('t->'p),parseProjection exp
            | exp -> raiseUnsupportedExpression exp
        Fields (f >> box,names)

    let rec findCase = function
        | WithValue (_,_,exp) -> findCase exp
        | UnionCase case -> case
        | exp -> raiseUnsupportedExpression exp

/// Embedded DSL for defining the topologies
[<System.Diagnostics.CodeAnalysis.SuppressMessage("NameConventions", "TypeNamesMustBePascalCase")>]
[<System.Diagnostics.CodeAnalysis.SuppressMessage("NameConventions", "MemberNamesMustBePascalCase")>]
[<AutoOpen>]
module DSL =
    open Multilang
    open Topology
    open Dispatch
    open FSharp.Quotations

    /// spout function signature
    type Next<'a,'t> = 'a->'t option
    /// bolt function signature
    type Consume<'a> = 'a->unit
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

    module Spout =
        /// define a reliable spout
        /// mkArgs: one-time construction of arguments that will be passed into each next() call.
        /// mkAcker: one time construction of `Ack*Nack` handlers (using the args).
        /// next: spout function that returns an id*tuple option.
        let runReliable mkArgs (mkAcker:'args->Acker) (deactivate:'args->unit) (next:Next<_,_*'t>) :Spout<'t> =
            { MkComp = fun () -> FuncRef (reliableSpout mkArgs mkAcker deactivate next TupleSchema.toStreamName<'t>)
              Parallelism = 1u 
              Conf = Conf.empty }

        /// define spout with no processing guarantees
        /// mkArgs: one-time construction of arguments that will be passed into each next() call.
        /// next: spout function that returns a tuple option.
        let runUnreliable mkArgs (deactivate:'args->unit) (next:Next<_,'t>):Spout<'t> =
            { MkComp = fun () -> FuncRef (unreliableSpout mkArgs deactivate next TupleSchema.toStreamName<'t>)
              Parallelism = 1u
              Conf = Conf.empty }

        /// define a spout for a (external) shell or java component
        let ofExternal<'t> comp:Spout<'t> =
            { Spout.MkComp = (fun _ -> comp); Parallelism=1u; Conf = Conf.empty }

    module Bolt =
        /// define a bolt that auto-acks
        /// mkArgs: curried construction of arguments (log and conf applied only once) that will be passed into each next() call.
        /// consume: bolt function that will receive incoming tuples.
        let run mkArgs (consume:Consume<_>):Bolt<'t> =
            { MkComp = fun (toAnchors,act,deact) -> FuncRef (autoAckBolt mkArgs consume (toAnchors,act,deact) TupleSchema.toStreamName<'t>)
              Parallelism = 1u 
              Conf = Conf.empty
              Activate = None
              Deactivate = None }
        
        /// define a bolt that always nacks
        /// mkArgs: curried construction of arguments (log and conf applied only once) that will be passed into each next() call.
        /// consume: bolt function that will receive incoming tuples.
        let runTerminator mkArgs (consume:Consume<_>):Bolt<'t> =
            { MkComp = fun _ -> FuncRef (autoNackBolt mkArgs consume)
              Parallelism = 1u 
              Conf = Conf.empty
              Activate = None
              Deactivate = None }

        /// define a bolt for a (external) shell or java component
        let ofExternal<'t> comp:Bolt<'t> =
            { Bolt.MkComp = (fun _ -> comp); Parallelism=1u; Conf = Conf.empty; Activate = None; Deactivate = None }

    open System

    [<Obsolete("Use `Bolt.runTerminator` instead")>]
    let runTerminator = Bolt.runTerminator

    [<Obsolete("Use `Bolt.run` instead")>]
    let runBolt = Bolt.run

    [<Obsolete("Use `Bolt.ofExternal` instead")>]
    let asBolt<'t> = Bolt.ofExternal<'t>

    [<Obsolete("Use `Spout.ofExternal` instead")>]
    let asSpout<'t> = Spout.ofExternal<'t>

    [<Obsolete("Use `Spout.runUnreliable` instead")>]
    let runSpout = Spout.runUnreliable

    [<Obsolete("Use `Spout.runReliable` instead")>]
    let runReliableSpout = Spout.runReliable

   /// override default parallelism
    let inline withParallelism parallelism (spec:^s) =
        (^s : (static member WithParallelism : ^s*uint32 -> ^s) (spec, uint32 parallelism))

    /// supply component configuration/overrides
    let inline withConf conf (spec:^s) =
        (^s : (static member WithConf : ^s*Conf -> ^s) (spec, conf |> Conf.ofList ))

    /// supply component Activation tuple
    let inline withActivation tuple (spec:^s) =
        (^s : (static member WithActivation : ^s*'t -> ^s) (spec, tuple))

    /// supply component Deactivation tuple
    let inline withDeactivation tuple (spec:^s) =
        (^s : (static member WithDeactivation : ^s*'t -> ^s) (spec, tuple))

    /// define shuffle grouping
    type Shuffle =
        static member on ([<ReflectedDefinition(true)>] case:Expr<_->'t>):bool->ComponentId->ComponentId->Stream<'t> =
            fun anchor src dst -> 
                {Grouping = Grouping.Shuffle
                 Src = src
                 Dst = dst
                 Anchoring = anchor
                 Schema = Parsers.findCase case |> TupleSchema.toNames}

    /// define all grouping
    type All =
        static member on ([<ReflectedDefinition(true)>] case:Expr<_->'t>):bool->ComponentId->ComponentId->Stream<'t> =
            fun anchor src dst -> 
                {Grouping = Grouping.All
                 Src = src
                 Dst = dst
                 Anchoring = anchor
                 Schema = Parsers.findCase case |> TupleSchema.toNames}

    /// define direct grouping
    type Direct =
        static member on ([<ReflectedDefinition(true)>] case:Expr<_->'t>):bool->ComponentId->ComponentId->Stream<'t> =
            fun anchor src dst -> 
                {Grouping = Grouping.Direct
                 Src = src
                 Dst = dst
                 Anchoring = anchor
                 Schema = Parsers.findCase case |> TupleSchema.toNames}

    /// define fields grouping
    type Group =
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

    /// combine two topologies under the first topology's name
    let (++) (t:Topology<'t>) (t2:Topology<'t>) =
        { Name = t.Name;
          Streams= t.Streams |> Map.join t2.Streams
          Spouts = t.Spouts |> Map.join t2.Spouts
          Bolts = t.Bolts |> Map.join t2.Bolts
          Anchors = t.Anchors |> Map.join t2.Anchors
          Conf = t.Conf |> Map.join t2.Conf }

    /// TopologyBuilder computation expression
    type TopologyBuilder(name) =
        member __.Combine(t:Topology<'t>, t2:Topology<'t>) = 
            t ++ t2

        member __.Yield([<ReflectedDefinition(true)>] expr:Expr<ComponentId->ComponentId->Stream<'t>>):Topology<'t> = 
            Parsers.toTopology name expr
    
        member __.Delay(f) = f()
    
        member __.Zero() = 
            { Name=name;
              Streams= Map.empty<_,Stream<'t>>
              Spouts = Map.empty<_,Spout<'t>>
              Bolts = Map.empty<_,Bolt<'t>>
              Anchors = Map.empty
              Conf = Map.empty }

    /// topology builder instance
    let topology name = 
        TopologyBuilder name

