module FsShelter.ThriftIOTests

open NUnit.Framework
open Swensen.Unquote
open FsShelter.TestTopology
open FsShelter.Multilang
open System
open System.IO
open Thrift.Protocol
open Thrift.Transport
open Prolucid.ThriftShell
open CommonTests

type V = Messages.Variant

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

let reverseIn (stdin:#Stream) comp task :unit->Async<InCommand<'t>> =
    stdin.Seek(0L, SeekOrigin.Begin) |> ignore

    let streamRW = TupleSchema.mapSchema<'t>() |> Map.ofArray
    let findConstructor stream = 
        streamRW |> Map.find stream |> fst
        
    let toCommand (msg:Messages.ShellMsg) =
        let constr = findConstructor msg.Emit.Stream
        InCommand.Tuple ((ThriftIO.ofFields constr msg.Emit.Tuple)(), msg.Emit.Id, comp, msg.Emit.Stream, task)

    let proto = new TBinaryProtocol(new TStreamTransport(stdin, null))
    fun () -> async {
                let! msg = async {
                    let msg = Messages.ShellMsg() 
                    msg.Read proto
                    return msg
                }
                return toCommand msg
              }

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
                Config= (toDict ["FsShelter.id",V(StrVal ="Simple-2-1456522507")
                                 "dev.zookeeper.path",V(StrVal="/tmp/dev-storm-zookeeper")
                                 "topology.tick.tuple.freq.secs",V(Int32Val=30)
                                 "topology.classpath",V(None=Messages.NoneStruct())])))
        |> toStreams (mkStreams())
        |> ThriftIO.startWith 
        <|| (syncOut,ignore)

    let expected = InCommand<Schema>.Handshake(
                    Conf ["FsShelter.id",box "Simple-2-1456522507"
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
        <|| (syncOut,ignore)
    
    async {
        return! in'()
    } |> Async.RunSynchronously =! InCommand<Schema>.Next


[<Test>]
let ``reads ack``() = 
    let (in',_) = 
        Messages.StormMsg(AckCmd=Messages.AckCommand(Id="zzz"))
        |> toStreams (mkStreams())
        |> ThriftIO.startWith
        <|| (syncOut,ignore)

    async {
        return! in'()
    } |> Async.RunSynchronously =! InCommand<Schema>.Ack "zzz"


[<Test>]
let ``reads nack``() = 
    let (in',_) = 
        Messages.StormMsg(NackCmd=Messages.NackCommand(Id="zzz"))
        |> toStreams (mkStreams())
        |> ThriftIO.startWith
        <|| (syncOut,ignore)
    
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
        <|| (syncOut,ignore)

    async {
        return! in'()
    } |> Async.RunSynchronously =! InCommand<Schema>.Tuple(Original {x=62},"2651792242051038370","AddOneBolt","Original",1)


[<Test>]
let ``writes tuple``() = 
    let streams = mkStreams()
    let (_,out) = ThriftIO.startWith streams syncOut ignore
    
    out(Emit(Original {x=62},Some "2651792242051038370",["123"],"Original",None,None))
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
        <|| (syncOut,ignore)
    
    let even = Even({x=62},{str="a"})
    
    out'(Emit(even,Some "2651792242051038370",[],"Even",None,None))
    async {
        return! in'()
    } 
    |> Async.RunSynchronously =! InCommand<Schema>.Tuple(even,"2651792242051038370","AddOneBolt","Even",1)
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
        <|| (syncOut,ignore)
    
    let t = MaybeString(Some "zzz")
    
    out'(Emit(t,Some "2651792242051038370",[],"MaybeString",None,None))
    async {
        return! in'()
    } 
    |> Async.RunSynchronously =! InCommand<Schema>.Tuple(t,"2651792242051038370","AddOneBolt","MaybeString",1)
    let emit = (ofStreams streams).Emit
    (emit.Id, emit.Stream) =! ("2651792242051038370","MaybeString")
    emit.Tuple.Count =! 1
    emit.Tuple.[0].StrVal =! "zzz"


[<Test>]
[<Category("performance")>]
let ``roundtrip throughput``() =
    let count = 10000 
    use mem = new MemoryStream()
    let (_,out') = ThriftIO.startWith (mem,mem) syncOut ignore

    let sw = System.Diagnostics.Stopwatch.StartNew()
    async {
        for i in {1..count} do
            Emit(justFields,Some "2651792242051038370",[],"JustFields",None,None) |> out'
        let in':unit->Async<InCommand<Schema>> = reverseIn mem "AddOneBolt" 1 
        for i in {1..count} do
            do! in'() |> Async.Ignore
    } |> Async.RunSynchronously
    System.Diagnostics.Debug.WriteLine( sprintf "[Thrift] Ellapsed: %dms, %f/s" sw.ElapsedMilliseconds ((float count)/sw.Elapsed.TotalSeconds))
