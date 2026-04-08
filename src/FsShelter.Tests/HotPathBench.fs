module FsShelter.HotPathBench

open FsShelter.Hosting
open FsShelter.Hosting.Types
open FsShelter.Hosting.Channel
open FsShelter.Hosting.TupleTree
open FsShelter.Hosting.Routing
open FsShelter.Topology
open FsShelter.TestTopology
open FsShelter.Multilang
open NUnit.Framework
open System
open System.Diagnostics
open System.Threading

let inline measure label iterations (f: unit -> unit) =
    // warmup
    for _ in 1..1000 do f()
    GC.Collect(2, GCCollectionMode.Forced, true)
    GC.WaitForPendingFinalizers()
    let gc0Before = GC.CollectionCount 0
    let sw = Stopwatch.StartNew()
    for _ in 1..iterations do f()
    sw.Stop()
    let gc0After = GC.CollectionCount 0
    let nsPerOp = sw.Elapsed.TotalMilliseconds * 1_000_000.0 / float iterations
    printfn "  %-40s %8.0f ns/op   %d GC0 in %dk ops" label nsPerOp (gc0After - gc0Before) (iterations / 1000)

[<Test>]
[<Category("perf")>]
let ``Hot path component benchmarks`` () =
    let iterations = 1_000_000

    printfn "\n=== Disruptor publish ==="

    // 2. Disruptor RingBuffer publish - fire into a channel
    let mutable received = 0
    let (send, halt) = Channel.start 1024 ignore (fun (_:int) -> received <- received + 1)
    Thread.Sleep 50 // let disruptor thread start
    measure "Disruptor publish (int)" iterations (fun () ->
        send 42)
    halt()
    Thread.Sleep 50

    // With TaskMsg DU wrapping
    let mutable received2 = 0
    let (send2, halt2) = Channel.start 1024 ignore (fun (msg:TaskMsg<Schema,int>) ->
        match msg with Other x -> received2 <- received2 + x | _ -> ())
    Thread.Sleep 50
    measure "Disruptor publish (DU wrap)" iterations (fun () ->
        send2 (Other 1))
    halt2()
    Thread.Sleep 50

    printfn "\n=== Executor dispatch ==="

    // Executor with 4 tasks on one Disruptor — measures TaskId lookup overhead
    let mutable execReceived = 0
    let execTasks = [| for tid in 0..3 -> (tid, fun (_:int) (_:bool) -> execReceived <- execReceived + 1) |]
    let (execSend, execHalt) = Channel.startExecutor 1024 ignore execTasks
    Thread.Sleep 50
    let execSend2 = execSend 2
    measure "Executor publish (4 tasks)" iterations (fun () ->
        execSend2 42)
    execHalt()
    Thread.Sleep 50

    printfn "\n=== TupleTree operations ==="

    // Set up a mock acker channel to absorb Track/Anchor/Ok messages
    let mutable ackerReceived = 0
    let (ackerSend, ackerHalt) = Channel.start 4096 ignore (fun (_:TaskMsg<Schema, AckerMsg>) -> ackerReceived <- ackerReceived + 1)
    Thread.Sleep 50
    let ackers = [| (0, (ackerSend, ackerHalt)); (1, (ackerSend, ackerHalt)) |]

    // TupleTree.track: ID generation + Track to acker + returns closure
    let nextId = TupleTree.mkIdGenerator()
    measure "TupleTree.track" iterations (fun () ->
        let mkIds = TupleTree.track nextId ackers 0 () (Some (TupleId.ofString "src"))
        mkIds() |> ignore)

    // TupleTree.anchor: re-anchor from parent tuple
    let parentAnchors = [Anchored(12345L, 67890L)]
    measure "TupleTree.anchor" iterations (fun () ->
        let mkIds = TupleTree.anchor nextId ackers parentAnchors ()
        mkIds() |> ignore)

    // TupleTree.mkAck: ack routing to acker
    let ack = TupleTree.mkAck AckerMsg.Ok ackers
    let anchoredTid = Anchored(12345L, 67890L)
    measure "TupleTree.mkAck" iterations (fun () ->
        ack anchoredTid)

    ackerHalt()
    Thread.Sleep 50

    printfn "\n=== Routing ==="

    // Set up mock bolt channels to absorb routed tuples
    let mutable routed = 0
    let mkBoltChannel tid =
        let (s, h) = Channel.start 4096 ignore (fun (_:TaskMsg<Schema, InCommand<Schema>>) -> routed <- routed + 1)
        (tid, ("b1", (s, h)))
    let boltSends = [| for tid in 10..13 -> mkBoltChannel tid |]
    Thread.Sleep 50
    let boltTasks = boltSends |> Map.ofArray
    let streams = 
        Map.ofList [ (("s1", "Original"), "b1"), 
                     { Src = "s1"; Dst = "b1"; Grouping = Shuffle; Anchoring = true; Schema = ["x"] } ]
    let mkIds _ _ () = [Unanchored(nextId())]
    let router = mkTupleRouter mkIds streams boltTasks 0
    let testTuple = ([], None, Original { x = 42 }, "s1", "Original", (None: int option))
    measure "Routing.shuffle (4 instances)" iterations (fun () ->
        router testTuple)

    // Fields grouping
    let fieldsStreams = 
        Map.ofList [ (("s1", "Original"), "b1"), 
                     { Src = "s1"; Dst = "b1"; Grouping = Fields((fun (t:Schema) -> box t), ["x"]); Anchoring = true; Schema = ["x"] } ]
    let fieldsRouter = mkTupleRouter mkIds fieldsStreams boltTasks 0
    measure "Routing.fields (4 instances)" iterations (fun () ->
        fieldsRouter testTuple)

    for (_, (_, (_, h))) in boltSends do h()
    Thread.Sleep 50
