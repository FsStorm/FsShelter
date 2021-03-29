module FsShelter.ProtoIOTests

open NUnit.Framework
open Swensen.Unquote
open FsShelter.TestTopology
open FsShelter.Multilang
open System
open System.IO
open Google.Protobuf
open Google.Protobuf.WellKnownTypes
open FsStorm.ProtoShell
open TupleSchema
open CommonTests

type V = Messages.Variant
type VL = WellKnownTypes.Value


let toStreams (memin:#Stream,memout:#Stream) =
    fun (o:obj) -> 
        match o with
        | :? IMessage as msg -> msg.WriteDelimitedTo memin
        | :? seq<IMessage> as msgs -> for msg in msgs do msg.WriteDelimitedTo memin
        | _ -> failwith "Unexpected argument"
        memin.Flush()
        memin.Seek(0L, SeekOrigin.Begin) |> ignore
        memin,memout

let ofStreams (_,memout:#Stream) =
    memout.Seek(0L, SeekOrigin.Begin) |> ignore
    Messages.ShellMsg.Parser.ParseDelimitedFrom memout 

let reverseIn (memin:#Stream) comp task :unit->InCommand<'t> =
    memin.Seek(0L, IO.SeekOrigin.Begin) |> ignore

    let streamRW = TupleSchema.mapSchema<'t>() |> Map.ofArray
    let findConstructor stream = 
        streamRW |> Map.find stream |> fst
        
    let toCommand (msg:Messages.ShellMsg) =
        let constr = findConstructor msg.Emit.Stream
        InCommand.Tuple ((ProtoIO.ofFields constr msg.Emit.Tuple)(), msg.Emit.Id, comp, msg.Emit.Stream, task)

    fun () -> 
        let msg = Messages.ShellMsg.Parser.ParseDelimitedFrom memin
        toCommand msg

[<Test>]
let ``reads handshake``() = 
    let ctx = Messages.Context(ComponentId = "SimpleSpout",TaskId = 5)
    ctx.TaskComponents.Add(toDict [1,"AddOneBolt"
                                   2,"AddOneBolt"
                                   3,"ResultBolt"
                                   4,"ResultBolt"
                                   5,"SimpleSpout"
                                   6,"__acker"])
    let handshake = Messages.Handshake(PidDir="pids", Context = ctx)
    handshake.Config.Add(toDict ["FsShelter.id",VL(StringValue ="Simple-2-1456522507")
                                 "dev.zookeeper.path",VL(StringValue="/tmp/dev-storm-zookeeper")
                                 "topology.tick.tuple.freq.secs",VL(NumberValue=30.)
                                 "topology.classpath",VL(NullValue=WellKnownTypes.NullValue.NullValue)])

    let (in',_) = 
        Messages.StormMsg(Handshake = handshake)
        |> toStreams (mkStreams())
        |> ProtoIO.startWith 
        <|| (syncOut,fun _ -> ignore)

    let expected = InCommand<Schema>.Handshake(
                    Conf ["FsShelter.id",box "Simple-2-1456522507"
                          "dev.zookeeper.path", box "/tmp/dev-storm-zookeeper"
                          "topology.tick.tuple.freq.secs", box 30.
                          "topology.classpath", null],
                    "pids",
                    {TaskId=5;ComponentId="SimpleSpout";Components=Map [1,"AddOneBolt"
                                                                        2,"AddOneBolt"
                                                                        3,"ResultBolt"
                                                                        4, "ResultBolt"
                                                                        5,"SimpleSpout"
                                                                        6,"__acker"]})
    in'() =! expected


[<Test>]
let ``reads next``() = 
    let (in',_) = 
        Messages.StormMsg(NextCmd=Messages.NextCommand())
        |> toStreams (mkStreams())
        |> ProtoIO.startWith
        <|| (syncOut,fun _ -> ignore)
    
    in'() =! InCommand<Schema>.Next


[<Test>]
let ``reads ack``() = 
    let (in',_) = 
        Messages.StormMsg(AckCmd=Messages.AckCommand(Id="zzz"))
        |> toStreams (mkStreams())
        |> ProtoIO.startWith
        <|| (syncOut,fun _ -> ignore)

    in'() =! InCommand<Schema>.Ack "zzz"


[<Test>]
let ``reads nack``() = 
    let (in',_) = 
        Messages.StormMsg(NackCmd=Messages.NackCommand(Id="zzz"))
        |> toStreams (mkStreams())
        |> ProtoIO.startWith
        <|| (syncOut,fun _ -> ignore)
    
    in'() =! InCommand<Schema>.Nack "zzz"


[<Test>]
let ``reads activate``() = 
    let (in',_) = 
        Messages.StormMsg(ActivateCmd=Messages.ActivateCommand())
        |> toStreams (mkStreams())
        |> ProtoIO.startWith
        <|| (syncOut,fun _ -> ignore)
    
    in'() =! InCommand<Schema>.Activate


[<Test>]
let ``reads deactivate``() = 
    let (in',_) = 
        Messages.StormMsg(DeactivateCmd=Messages.DeactivateCommand())
        |> toStreams (mkStreams())
        |> ProtoIO.startWith
        <|| (syncOut,fun _ -> ignore)
    
    in'() =! InCommand<Schema>.Deactivate

    
[<Test>]
let ``reads tuple``() = 
    let tuple = 
        Messages.StreamIn(
                Id="2651792242051038370",
                Stream="Original",
                Task=1,
                Comp="AddOneBolt")
    tuple.Tuple.Add([V(Int32Val=62)])

    let (in',_) = 
        Messages.StormMsg(StreamIn=tuple)
        |> toStreams (mkStreams())
        |> ProtoIO.startWith
        <|| (syncOut,fun _ -> ignore)

    in'() =! InCommand<Schema>.Tuple(Original {x=62},"2651792242051038370","AddOneBolt","Original",1)

[<Test>]
let ``reads generic tuple``() = 
    let t = Original {x=62}
    let tuple = 
        Messages.StreamIn(
                Id="2651792242051038370",
                Stream="Inner",
                Task=1,
                Comp="AddOneBolt")
    tuple.Tuple.Add([V(BytesVal = ByteString.CopyFrom (IO.Common.blobSerialize t))])

    let (in',_) = 
        Messages.StormMsg(StreamIn=tuple)
        |> toStreams (mkStreams())
        |> ProtoIO.startWith
        <|| (syncOut,fun _ -> ignore)

    in'() =! InCommand<GenericSchema<Schema>>.Tuple(GenericSchema.Inner(t),"2651792242051038370","AddOneBolt","Inner",1)

[<Test>]
let ``reads nested generic tuple``() = 
    let t = Original {x=62}
    let tuple = 
        Messages.StreamIn(
                Id="2651792242051038370",
                Stream="Inner+Original",
                Task=1,
                Comp="AddOneBolt")
    tuple.Tuple.Add([V(Int32Val=62)])

    let (in',_) = 
        Messages.StormMsg(StreamIn=tuple)
        |> toStreams (mkStreams())
        |> ProtoIO.startWith
        <|| (syncOut,fun _ -> ignore)

    in'() =! InCommand<GenericNestedSchema<Schema>>.Tuple(GenericNestedSchema.Inner(t),"2651792242051038370","AddOneBolt","Inner+Original",1)

    
[<Test>]
let ``writes tuple``() = 
    let streams = mkStreams()
    let (_,out) = ProtoIO.startWith streams syncOut (fun _ -> ignore)
    
    out <| Emit(Original {x=62},Some "2651792242051038370",["123"],"Original",None,None)
    Threading.Thread.Sleep(10)
    let emit = (ofStreams streams).Emit
    (emit.Id, emit.Anchors |> List.ofSeq, emit.NeedTaskIds, emit.Stream) =! ("2651792242051038370",["123"], false, "Original")
    emit.Tuple.Count =! 1
    emit.Tuple.[0].Int32Val =! 62

[<Test>]
let ``writes generic tuple``() = 
    let streams = mkStreams()
    let (_,out) = ProtoIO.startWith streams syncOut (fun _ -> ignore)
    
    out <| Emit(GenericSchema.Inner(Original {x=62}),Some "2651792242051038370",["123"],"Inner",None,None)
    Threading.Thread.Sleep(10)
    let emit = (ofStreams streams).Emit
    (emit.Id, emit.Anchors |> List.ofSeq, emit.NeedTaskIds, emit.Stream) =! ("2651792242051038370",["123"], false, "Inner")
    emit.Tuple.Count =! 1
    emit.Tuple.[0].BytesVal.ToByteArray() |> IO.Common.blobDeserialize typeof<Schema> |> unbox =! Original {x=62}
    
[<Test>]
let ``writes nested generic tuple``() = 
    let streams = mkStreams()
    let (_,out) = ProtoIO.startWith streams syncOut (fun _ -> ignore)
    
    out <| Emit(GenericNestedSchema.Inner(Original {x=62}),Some "2651792242051038370",["123"],"Inner+Original",None,None)
    Threading.Thread.Sleep(10)
    let emit = (ofStreams streams).Emit
    (emit.Id, emit.Anchors |> List.ofSeq, emit.NeedTaskIds, emit.Stream) =! ("2651792242051038370",["123"], false, "Inner+Original")
    emit.Tuple.Count =! 1
    emit.Tuple.[0].Int32Val =! 62

[<Test>]
let ``rw complex tuple``() = 
    let tuple = 
        Messages.StreamIn(
                Id="2651792242051038370",
                Stream="Even",
                Task=1,
                Comp="AddOneBolt")
    tuple.Tuple.Add([V(Int32Val=62);V(StrVal="a")])
    let streams = mkStreams()
    let (in',out') = 
        Messages.StormMsg(StreamIn=tuple)
        |> toStreams streams
        |> ProtoIO.startWith 
        <|| (syncOut,fun _ -> ignore)
    
    let even = Even({x=62},{str="a"})
    
    out'(Emit(even,Some "2651792242051038370",[],"Even",None,None))
    in'() =! InCommand<Schema>.Tuple(even,"2651792242051038370","AddOneBolt","Even",1)
    let emit = (ofStreams streams).Emit
    (emit.Id, emit.Stream) =! ("2651792242051038370", "Even")
    emit.Tuple.Count =! 2
    emit.Tuple.[0].Int32Val =! 62
    emit.Tuple.[1].StrVal =! "a"

[<Test>]
let ``roundtrip nested tuple``() = 
    use mem = new MemoryStream()
    let (_,out') = ProtoIO.startWith (mem,mem) syncOut (fun _ -> ignore)

    let nested = Nested(
                    {nested = {str="a"}
                     xs=[{x=1}; {x=2}]
                     m = Map [3, Some {x=3}; 0, None]
                     gxs = System.Collections.Generic.List([{x=4}])
                     d = toDict ["5", Some {x=5}; "0", None]})
    let emitted = 
        Emit(nested,Some "2651792242051038370",[],"Nested",None, None) |> out'

        let in' = reverseIn mem "AddOneBolt" 1
        in'()
    
    match emitted,nested with
    | InCommand.Tuple(Nested tuple, _, _, _, _),Nested original ->
        tuple.nested         =! original.nested
        tuple.xs             =! original.xs
        List.ofSeq tuple.gxs =! List.ofSeq original.gxs
        tuple.m              =! original.m
        List.ofSeq tuple.d   =! List.ofSeq original.d
    | _ -> failwith "?!"

[<Test>]
let ``roundtrip generic nested tuple``() = 
    use mem = new MemoryStream()
    let (_,out') = ProtoIO.startWith (mem,mem) syncOut (fun _ -> ignore)

    let nested =
        GenericNestedSchema<Schema>.Inner(
            Nested( {nested = {str="a"}
                     xs=[{x=1}; {x=2}]
                     m = Map [3, Some {x=3}; 0, None]
                     gxs = System.Collections.Generic.List([{x=4}])
                     d = toDict ["5", Some {x=5}; "0", None]}))
    let emitted = 
        Emit(nested,Some "2651792242051038370",[],"Inner+Nested",None, None) |> out'

        let in' = reverseIn mem "AddOneBolt" 1
        in'()
    
    match emitted,nested with
    | InCommand.Tuple(GenericNestedSchema.Inner(Nested tuple), _, _, _, _),GenericNestedSchema.Inner(Nested original) ->
        tuple.nested         =! original.nested
        tuple.xs             =! original.xs
        List.ofSeq tuple.gxs =! List.ofSeq original.gxs
        tuple.m              =! original.m
        List.ofSeq tuple.d   =! List.ofSeq original.d
    | _ -> failwith "?!"

[<Test>]
let ``rw option tuple``() = 
    let tuple = 
        Messages.StreamIn(
                Id="2651792242051038370",
                Stream="MaybeString",
                Task=1,
                Comp="AddOneBolt")
    tuple.Tuple.Add([V(StrVal="zzz")])
    let streams = mkStreams()
    let (in',out') = 
        Messages.StormMsg(StreamIn=tuple)
        |> toStreams streams
        |> ProtoIO.startWith 
        <|| (syncOut,fun _ -> ignore)
    
    let t = MaybeString(Some "zzz")
    
    out'(Emit(t,Some "2651792242051038370",[],"MaybeString",None,None))
    in'() =! InCommand<Schema>.Tuple(t,"2651792242051038370","AddOneBolt","MaybeString",1)
    let emit = (ofStreams streams).Emit
    (emit.Id, emit.Stream) =! ("2651792242051038370","MaybeString")
    emit.Tuple.Count =! 1
    emit.Tuple.[0].StrVal =! "zzz"

[<Test>]
let ``rw Nullable tuple``() = 
    let guid = Guid.NewGuid()
    let tuple = 
        Messages.StreamIn(
                Id="2651792242051038370",
                Stream="NullableGuid",
                Task=1,
                Comp="AddOneBolt")
    tuple.Tuple.Add([V(BytesVal=guid.ToByteString())])
    let streams = mkStreams()
    let (in',out') = 
        Messages.StormMsg(StreamIn=tuple)
        |> toStreams streams
        |> ProtoIO.startWith 
        <|| (syncOut,fun _ -> ignore)
    
    let t = NullableGuid(Nullable guid)
    
    out'(Emit(t,Some "2651792242051038370",[],"NullableGuid",None,None))
    in'() =! InCommand<Schema>.Tuple(t,"2651792242051038370","AddOneBolt","NullableGuid",1)
    let emit = (ofStreams streams).Emit
    (emit.Id, emit.Stream) =! ("2651792242051038370","NullableGuid")
    emit.Tuple.Count =! 1
    emit.Tuple.[0].BytesVal.ToGuid() =! guid

[<Test>]
[<Category("performance")>]
let ``roundtrip throughput``() =
    let count = 1000000 
    use mem = new MemoryStream()
    let (_,out') = ProtoIO.startWith (mem,mem) syncOut (fun _ -> ignore)

    let sw = System.Diagnostics.Stopwatch.StartNew()
    for i in 1..count do
        Emit(justFields,Some "2651792242051038370",[],"JustFields",None,None) |> out'
    let in':unit->InCommand<Schema> = reverseIn mem "AddOneBolt" 1 
    for i in 1..count do
        in'() |> ignore

    sw.Stop()
    (fun _ -> sprintf "[Proto] Ellapsed: %dms, %f/s\n" sw.ElapsedMilliseconds ((float count)/sw.Elapsed.TotalSeconds)) 
    |> TraceLog.asyncLog
    (fun _ -> sprintf "-- GC: %dKB, %d/%d/%d" ((GC.GetTotalMemory false)/1024L)  (GC.CollectionCount 0) (GC.CollectionCount 1) (GC.CollectionCount 2)) 
    |> TraceLog.asyncLog
