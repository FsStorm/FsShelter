module Storm.ProtoIOTests

open NUnit.Framework
open Swensen.Unquote
open Storm.TestTopology
open Storm.Multilang
open System
open System.IO
open Google.Protobuf
open Prolucid.ProtoShell
open TupleSchema

type V = Messages.Variant
type VL = WellKnownTypes.Value

let mkStreams() =
    let memin = new MemoryStream()
    let memout = new MemoryStream()
    (memin,memout)

let toStreams (memin:#Stream,memout:#Stream) =
    fun (o:obj) -> 
        match o with
        | :? IMessage as msg -> msg.WriteDelimitedTo memin
        | :? seq<IMessage> as msgs -> for msg in msgs do msg.WriteDelimitedTo memin
        | _ -> failwith "Unexpected argument"
        memin.Flush()
        memin.Seek(0L, SeekOrigin.Begin) |> ignore
        memin,memout

let ofStreams (memin,memout:#Stream) =
    memout.Seek(0L, SeekOrigin.Begin) |> ignore
    Messages.ShellMsg.Parser.ParseDelimitedFrom memout 

let private toDict (s:seq<_*_>) = System.Linq.Enumerable.ToDictionary(s, fst, snd)

[<Test>]
let ``reads handshake``() = 
    let ctx = Messages.Context(ComponentId = "SimpleSpout",TaskId = 5)
    ctx.TaskComponents.Add(toDict [1,"AddOneBolt"
                                   2,"AddOneBolt"
                                   3,"ResultBolt"
                                   4,"ResultBolt"
                                   5,"SimpleSpout"
                                   6,"__acker"])
    let handshake = Messages.Handshake(PidDir="C:\\Users\\eugene\\storm-local\\workers\\9ee413b6-c7d2-4896-ae4d-d150da988822\\pids", Context = ctx)
    handshake.Config.Add(toDict ["storm.id",VL(StringValue ="FstSample-2-1456522507")
                                 "dev.zookeeper.path",VL(StringValue="/tmp/dev-storm-zookeeper")
                                 "topology.tick.tuple.freq.secs",VL(NumberValue=30.)
                                 "topology.classpath",VL(NullValue=WellKnownTypes.NullValue.NULL_VALUE)])

    let (in',_) = 
        Messages.StormMsg(Handshake = handshake)
        |> toStreams (mkStreams())
        |> ProtoIO.startWith 
        <| ignore

    let expected = InCommand<Schema>.Handshake(
                    Conf ["storm.id",box "FstSample-2-1456522507"
                          "dev.zookeeper.path", box "/tmp/dev-storm-zookeeper"
                          "topology.tick.tuple.freq.secs", box 30.
                          "topology.classpath", null],
                    "C:\\Users\\eugene\\storm-local\\workers\\9ee413b6-c7d2-4896-ae4d-d150da988822\\pids",
                    {TaskId=5;ComponentId="SimpleSpout";Components=Map [1,"AddOneBolt"
                                                                        2,"AddOneBolt"
                                                                        3,"ResultBolt"
                                                                        4, "ResultBolt"
                                                                        5,"SimpleSpout"
                                                                        6,"__acker"]})
    async {
        return! in'()
    } |> Async.RunSynchronously =! expected


[<Test>]
let ``reads next``() = 
    let (in',_) = 
        Messages.StormMsg(NextCmd=Messages.NextCommand())
        |> toStreams (mkStreams())
        |> ProtoIO.startWith
        <| ignore
    
    async {
        return! in'()
    } |> Async.RunSynchronously =! InCommand<Schema>.Next


[<Test>]
let ``reads ack``() = 
    let (in',_) = 
        Messages.StormMsg(AckCmd=Messages.AckCommand(Id="zzz"))
        |> toStreams (mkStreams())
        |> ProtoIO.startWith
        <| ignore

    async {
        return! in'()
    } |> Async.RunSynchronously =! InCommand<Schema>.Ack "zzz"


[<Test>]
let ``reads nack``() = 
    let (in',_) = 
        Messages.StormMsg(NackCmd=Messages.NackCommand(Id="zzz"))
        |> toStreams (mkStreams())
        |> ProtoIO.startWith
        <| ignore
    
    async {
        return! in'()
    } |> Async.RunSynchronously =! InCommand<Schema>.Nack "zzz"


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
        <| ignore

    async {
        return! in'()
    } |> Async.RunSynchronously =! InCommand<Schema>.Tuple(Original {x=62},"2651792242051038370","AddOneBolt","Original",1)


[<Test>]
let ``writes tuple``() = 
    let streams = mkStreams()
    let (_,out) = ProtoIO.startWith streams ignore
    
    out(Emit(Original {x=62},Some "2651792242051038370",["123"],"Original",None))
    Threading.Thread.Sleep(10)
    let emit = (ofStreams streams).Emit
    (emit.Id, emit.Anchors |> List.ofSeq, emit.NeedTaskIds, emit.Stream) =! ("2651792242051038370",["123"], false, "Original")
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
        <| ignore
    
    let even = Even({x=62},{str="a"})
    
    out'(Emit(even,Some "2651792242051038370",[],"Even",None))
    async {
        return! in'()
    } 
    |> Async.RunSynchronously =! InCommand<Schema>.Tuple(even,"2651792242051038370","AddOneBolt","Even",1)
    Threading.Thread.Sleep 100
    let emit = (ofStreams streams).Emit
    (emit.Id, emit.Stream) =! ("2651792242051038370", "Even")
    emit.Tuple.Count =! 2
    emit.Tuple.[0].Int32Val =! 62
    emit.Tuple.[1].StrVal =! "a"


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
        <|  ignore
    
    let t = MaybeString(Some "zzz")
    
    out'(Emit(t,Some "2651792242051038370",[],"MaybeString",None))
    async {
        return! in'()
    } 
    |> Async.RunSynchronously =! InCommand<Schema>.Tuple(t,"2651792242051038370","AddOneBolt","MaybeString",1)
    Threading.Thread.Sleep 100
    let emit = (ofStreams streams).Emit
    (emit.Id, emit.Stream) =! ("2651792242051038370","MaybeString")
    emit.Tuple.Count =! 1
    emit.Tuple.[0].StrVal =! "zzz"


[<Test>]
[<Category("performance")>]
let ``rw throughput``() =
    let count = 10000 
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
        |> Seq.replicate count
        |> toStreams streams
        |> ProtoIO.startWith 
        <| ignore
    
    let even = Even({x=62},{str="a"})
    let sw = System.Diagnostics.Stopwatch.StartNew()
    async {
        for i in {1..count} do
            Emit(even,Some "2651792242051038370",[],"Even",None) |> out'
            let! msg = in'()
            msg |> ignore
    } |> Async.RunSynchronously
    System.Diagnostics.Debug.WriteLine( sprintf "Ellapsed: %dms, %d/ms" sw.ElapsedMilliseconds (10000L/sw.ElapsedMilliseconds))

