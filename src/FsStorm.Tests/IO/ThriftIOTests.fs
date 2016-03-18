module Storm.ThriftIOTests

open NUnit.Framework
open Swensen.Unquote
open Storm.TestTopology
open Storm.Multilang
open System
open System.IO
open Thrift.Protocol
open Thrift.Transport
open Prolucid.ThriftShell
open TupleSchema

type V = Messages.Variant

let mkStreams() =
    let memin = new MemoryStream()
    let memout = new MemoryStream()
    (memin,memout)

let toStreams (memin,memout) =
    let proto = new TBinaryProtocol(new TStreamTransport(memout,memin))
    fun (o:obj) -> 
        match o with
        | :? TBase as msg -> msg.Write proto
        | :? seq<TBase> as msgs -> for msg in msgs do msg.Write proto
        | _ -> failwith "Unexpected argument"
        proto.Transport.Flush()
        memin.Seek(0L, SeekOrigin.Begin) |> ignore
        memin,memout

let ofStreams (memin,memout:#Stream) =
    memout.Seek(0L, SeekOrigin.Begin) |> ignore
    let proto = new TBinaryProtocol(new TStreamTransport(memout,memin))
    let msg = Messages.ShellMsg() 
    msg.Read proto
    msg

let private toDict (s:seq<_*_>) = System.Linq.Enumerable.ToDictionary(s, fst, snd)

[<Test>]
let ``reads handshake``() = 
    let (in',_) = 
        Messages.StormMsg(
            Handshake = Messages.Handshake(
                PidDir="C:\\Users\\eugene\\storm-local\\workers\\9ee413b6-c7d2-4896-ae4d-d150da988822\\pids",
                Context=Messages.Context(
                    TaskComponents = (toDict [1,"AddOneBolt"
                                              2,"AddOneBolt"
                                              3,"ResultBolt"
                                              4,"ResultBolt"
                                              5,"SimpleSpout"
                                              6,"__acker"]),
                    ComponentId = "SimpleSpout",
                    TaskId = 5),
                Config= (toDict ["storm.id",V(StrVal ="FstSample-2-1456522507")
                                 "dev.zookeeper.path",V(StrVal="/tmp/dev-storm-zookeeper")
                                 "topology.tick.tuple.freq.secs",V(Int32Val=30)
                                 "topology.classpath",V(None=Messages.NoneStruct())])))
        |> toStreams (mkStreams())
        |> ThriftIO.startWith 
        <| ignore

    let expected = InCommand<Schema>.Handshake(
                    Conf ["storm.id",box "FstSample-2-1456522507"
                          "dev.zookeeper.path", box "/tmp/dev-storm-zookeeper"
                          "topology.tick.tuple.freq.secs", box 30
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
        |> ThriftIO.startWith
        <| ignore
    
    async {
        return! in'()
    } |> Async.RunSynchronously =! InCommand<Schema>.Next


[<Test>]
let ``reads ack``() = 
    let (in',_) = 
        Messages.StormMsg(AckCmd=Messages.AckCommand(Id="zzz"))
        |> toStreams (mkStreams())
        |> ThriftIO.startWith
        <| ignore

    async {
        return! in'()
    } |> Async.RunSynchronously =! InCommand<Schema>.Ack "zzz"


[<Test>]
let ``reads nack``() = 
    let (in',_) = 
        Messages.StormMsg(NackCmd=Messages.NackCommand(Id="zzz"))
        |> toStreams (mkStreams())
        |> ThriftIO.startWith
        <| ignore
    
    async {
        return! in'()
    } |> Async.RunSynchronously =! InCommand<Schema>.Nack "zzz"


[<Test>]
let ``reads tuple``() = 
    let (in',_) = 
        Messages.StormMsg(
            StreamIn=Messages.StreamIn(
                Id="2651792242051038370",
                Tuple=Collections.Generic.List([V(Int32Val=62)]),
                Stream="Original",
                Task=1,
                Comp="AddOneBolt"))
        |> toStreams (mkStreams())
        |> ThriftIO.startWith
        <| ignore

    async {
        return! in'()
    } |> Async.RunSynchronously =! InCommand<Schema>.Tuple(Original {x=62},"2651792242051038370","AddOneBolt","Original",1)


[<Test>]
let ``writes tuple``() = 
    let streams = mkStreams()
    let (_,out) = ThriftIO.startWith streams ignore
    
    out(Emit(Original {x=62},Some "2651792242051038370",["123"],"Original",None))
    Threading.Thread.Sleep(10)
    let emit = (ofStreams streams).Emit
    (emit.Id, emit.Anchors |> List.ofSeq, emit.NeedTaskIds, emit.Stream) =! ("2651792242051038370",["123"], false, "Original")
    emit.Tuple.Count =! 1
    emit.Tuple.[0].Int32Val =! 62

[<Test>]
let ``rw complex tuple``() = 
    let streams = mkStreams()
    let (in',out') = 
        Messages.StormMsg(
            StreamIn=Messages.StreamIn(
                Id="2651792242051038370",
                Tuple=Collections.Generic.List([V(Int32Val=62);V(StrVal="a")]),
                Stream="Even",
                Task=1,
                Comp="AddOneBolt"))
        |> toStreams streams
        |> ThriftIO.startWith 
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
    let streams = mkStreams()
    let (in',out') = 
        Messages.StormMsg(
            StreamIn=Messages.StreamIn(
                Id="2651792242051038370",
                Tuple=Collections.Generic.List([V(StrVal="zzz")]),
                Stream="MaybeString",
                Task=1,
                Comp="AddOneBolt"))
        |> toStreams streams
        |> ThriftIO.startWith 
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
    let streams = mkStreams()
    let (in',out') = 
        Messages.StormMsg(
            StreamIn=Messages.StreamIn(
                Id="2651792242051038370",
                Tuple=Collections.Generic.List([V(Int32Val=62); V(StrVal="a")]),
                Stream="Even",
                Task=1,
                Comp="AddOneBolt"))
        |> Seq.replicate count
        |> toStreams streams
        |> ThriftIO.startWith 
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

