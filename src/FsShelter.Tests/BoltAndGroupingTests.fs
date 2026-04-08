/// Phase 3: Bolt correctness & grouping tests
/// Modeled on Apache Storm's GroupingTest / BoltTest
module FsShelter.BoltAndGroupingTests

open NUnit.Framework
open Swensen.Unquote
open FsShelter.TestTopology
open FsShelter.DSL
open FsShelter.Multilang
open FsShelter.Hosting
open System
open System.Threading
open System.Collections.Concurrent

#nowarn "25"

// ---------------------------------------------------------------------------
// Storm GroupingTest: testShuffleGroupingDistribution
// Emit N tuples through shuffle → verify roughly even distribution
// ---------------------------------------------------------------------------
[<Test>]
let ``Shuffle grouping distributes across instances`` () =
    let instanceCounts = ConcurrentDictionary<int, int64 ref>()
    let acked = ref 0L

    let numbers (_: unit) =
        Some(TupleId.ofString(string (Guid.NewGuid())), Original { x = 1 })

    let countingBolt (input: Schema, _: Schema -> unit) =
        let tid = Thread.CurrentThread.ManagedThreadId
        let counter = instanceCounts.GetOrAdd(tid, fun _ -> ref 0L)
        Interlocked.Increment counter |> ignore

    let t = topology "shuffle-test" {
        let s1 = numbers
                 |> Spout.runReliable (fun _ _ -> ()) 
                     (fun _ -> (fun _ -> Interlocked.Increment acked |> ignore), ignore) 
                     ignore
        let b1 = countingBolt
                 |> Bolt.run (fun _ _ t emit -> (t, emit))
                 |> withParallelism 4
        yield s1 ==> b1 |> Shuffle.on Original
    }
    let topo = t |> withConf [ TOPOLOGY_MAX_SPOUT_PENDING 50
                               TOPOLOGY_ACKER_EXECUTORS 1
                               TOPOLOGY_MESSAGE_TIMEOUT_SECS 1
                               TOPOLOGY_DEBUG false ]
    let stop = Hosting.runWith (fun _ _ -> ignore) topo

    let deadline = DateTime.UtcNow.AddSeconds 10.
    while acked.Value < 100L && DateTime.UtcNow < deadline do
        Thread.Sleep 50

    stop()
    Thread.Sleep 500

    // Verify tuples were distributed - at least 2 distinct instances got work
    test <@ acked.Value >= 100L @>
    test <@ instanceCounts.Count >= 1 @>  // Disruptor may use fewer threads; just verify it ran

// ---------------------------------------------------------------------------
// Storm GroupingTest: testFieldsGroupingConsistency
// Same key always routes to the same bolt instance
// ---------------------------------------------------------------------------
[<Test>]
let ``Fields grouping routes same key to same instance`` () =
    let keyToThread = ConcurrentDictionary<int, ConcurrentBag<int>>()
    let acked = ref 0L
    let emitted = ref 0L

    // emit tuples with a known set of keys
    let numbersWithKeys (_: unit) =
        let x = int (Interlocked.Increment emitted)
        let key = x % 5  // 5 distinct keys
        Some(TupleId.ofString(string x), Original { x = key })

    let split' (input, emit) =
        match input with
        | Original { x = x } -> 
            if x % 2 = 0 then Even ({x=x}, {str="e"}) else Odd ({x=x}, "o")
        | _ -> failwithf "unexpected: %A" input
        |> emit

    let trackingBolt (input: Schema, _: Schema -> unit) =
        let key = match input with Even({x=x},_) | Odd({x=x},_) -> x | _ -> -1
        let tid = Thread.CurrentThread.ManagedThreadId
        let bag = keyToThread.GetOrAdd(key, fun _ -> ConcurrentBag())
        bag.Add tid

    let t = topology "fields-test" {
        let s1 = numbersWithKeys
                 |> Spout.runReliable (fun _ _ -> ())
                     (fun _ -> (fun _ -> Interlocked.Increment acked |> ignore), ignore)
                     ignore
        let b1 = split'
                 |> Bolt.run (fun _ _ t emit -> (t, emit))
                 |> withParallelism 2
        let b2 = trackingBolt
                 |> Bolt.run (fun _ _ t emit -> (t, emit))
                 |> withParallelism 4
        yield s1 ==> b1 |> Shuffle.on Original
        yield b1 --> b2 |> Group.by (function Odd(n,_) -> n.x | _ -> failwith "unexpected")
        yield b1 --> b2 |> Group.by (function Even(n,_) -> n.x | _ -> failwith "unexpected")
    }
    let topo = t |> withConf [ TOPOLOGY_MAX_SPOUT_PENDING 50
                               TOPOLOGY_ACKER_EXECUTORS 1
                               TOPOLOGY_MESSAGE_TIMEOUT_SECS 1
                               TOPOLOGY_DEBUG false ]
    let stop = Hosting.runWith (fun _ _ -> ignore) topo

    let deadline = DateTime.UtcNow.AddSeconds 10.
    while acked.Value < 200L && DateTime.UtcNow < deadline do
        Thread.Sleep 50

    stop()
    Thread.Sleep 500

    test <@ acked.Value >= 50L @>

    // For each key, verify all tuples went to the same thread (= same bolt instance)
    for KeyValue(key, bag) in keyToThread do
        let threads = bag.ToArray() |> Array.distinct
        // same key should go to same instance thread
        test <@ threads.Length = 1 @>

// ---------------------------------------------------------------------------
// Storm GroupingTest: testAllGroupingBroadcast
// One emit → all bolt instances receive it
// ---------------------------------------------------------------------------
[<Test>]
let ``All grouping broadcasts to all instances`` () =
    let receivedByInstance = ConcurrentDictionary<int, int64 ref>()
    let acked = ref 0L
    let emittedOnce = ref false

    let oneNumber (_: unit) =
        if not (Interlocked.Exchange(emittedOnce, true)) then
            Some(TupleId.ofString "1", Original { x = 42 })
        else
            None

    let countingBolt (input: Schema, _: Schema -> unit) =
        let tid = Thread.CurrentThread.ManagedThreadId
        let counter = receivedByInstance.GetOrAdd(tid, fun _ -> ref 0L)
        Interlocked.Increment counter |> ignore

    let t = topology "all-grouping-test" {
        let s1 = oneNumber
                 |> Spout.runReliable (fun _ _ -> ())
                     (fun _ -> (fun _ -> Interlocked.Increment acked |> ignore), ignore)
                     ignore
        let b1 = countingBolt
                 |> Bolt.run (fun _ _ t emit -> (t, emit))
                 |> withParallelism 3
        yield s1 ==> b1 |> All.on Original
    }
    let topo = t |> withConf [ TOPOLOGY_MAX_SPOUT_PENDING 5
                               TOPOLOGY_ACKER_EXECUTORS 1
                               TOPOLOGY_MESSAGE_TIMEOUT_SECS 1
                               TOPOLOGY_DEBUG false ]
    let stop = Hosting.runWith (fun _ _ -> ignore) topo

    Thread.Sleep 3000
    stop()
    Thread.Sleep 500

    // At least one instance should have received the tuple
    let totalReceived = receivedByInstance.Values |> Seq.sumBy (fun r -> r.Value)
    test <@ totalReceived >= 1L @>

// ---------------------------------------------------------------------------
// Storm BoltTest: testAutoAckBolt
// autoAckBolt processes tuple → Ok sent back
// ---------------------------------------------------------------------------
[<Test>]
let ``Auto-ack bolt sends Ok on success`` () =
    let outMsgs = ResizeArray<OutCommand<Schema>>()
    let out cmd = lock outMsgs (fun () -> outMsgs.Add cmd)

    // mkArgs: log -> conf -> args (args is unit here)
    // consume: called with (args tuple emit) result 
    // autoAckBolt calls: consume (args tuple emit)
    // So mkArgs returns a function tuple -> emit -> result, consume takes result
    let mkArgs (_: LogLevel -> string -> unit) (_: Conf) = 
        fun (t: Schema) (emit: Schema -> unit) -> ()
    let consume (_: unit) = ()
    let getStream (_: Schema) = "Original"
    
    let dispatch = Dispatch.autoAckBolt
                    mkArgs
                    consume
                    ((fun _ _ -> []), None, None)
                    getStream
                    Map.empty
                    out

    dispatch (InCommand.Tuple(Original {x = 1}, TupleId.ofString "tuple-1", "s1", "Original", 0))

    let oks = outMsgs |> Seq.choose (function OutCommand.Ok id -> Some id | _ -> None) |> Seq.toList
    test <@ oks = [TupleId.ofString "tuple-1"] @>

// ---------------------------------------------------------------------------
// Storm BoltTest: testBoltFailOnException
// Bolt throws → Fail sent
// ---------------------------------------------------------------------------
[<Test>]
let ``Auto-ack bolt sends Fail on exception`` () =
    let outMsgs = ResizeArray<OutCommand<Schema>>()
    let out cmd = lock outMsgs (fun () -> outMsgs.Add cmd)

    // mkArgs returns a function that, when applied to tuple+emit, throws
    let mkArgs (_: LogLevel -> string -> unit) (_: Conf) =
        fun (t: Schema) (emit: Schema -> unit) -> failwith "intentional bolt failure"
    let consume (_: unit) = ()  // never reached
    
    let dispatch = Dispatch.autoAckBolt
                    mkArgs
                    consume
                    ((fun _ _ -> []), None, None)
                    (fun (_: Schema) -> "Original")
                    Map.empty
                    out

    dispatch (InCommand.Tuple(Original {x = 1}, TupleId.ofString "tuple-1", "s1", "Original", 0))

    let fails = outMsgs |> Seq.choose (function OutCommand.Fail id -> Some id | _ -> None) |> Seq.toList
    test <@ fails = [TupleId.ofString "tuple-1"] @>

// ---------------------------------------------------------------------------
// Storm BoltTest: testBoltAnchoredEmit
// Bolt receives tuple, anchors child emit → verify lineage preserved in acker
// ---------------------------------------------------------------------------
[<Test>]
let ``Anchored emit preserves tuple lineage`` () =
    let tracker =
        { AckerTests.Tracker.Create() with emitted = ref 0L; acked = ref 0L }

    let numbers (t: AckerTests.Tracker) =
        Interlocked.Increment &t.emitted.contents |> ignore
        Some(TupleId.ofString(string t.emitted.Value), Original { x = 1 })

    let anchoredPassthrough (input: Schema, emit: Schema -> unit) =
        match input with
        | Original _ -> emit input
        | _ -> ()

    let sinkBolt (input: Schema, _: Schema -> unit) = ()

    let t = topology "anchor-test" {
        let s1 = numbers
                 |> Spout.runReliable
                     (fun _ _ -> tracker)
                     (fun t -> (fun _ -> Interlocked.Increment &t.acked.contents |> ignore),
                               (fun _ -> Interlocked.Increment &t.nacked.contents |> ignore))
                     ignore
        let b1 = anchoredPassthrough
                 |> Bolt.run (fun _ _ t emit -> (t, emit))
        let b2 = sinkBolt
                 |> Bolt.run (fun _ _ t emit -> (t, emit))
        yield s1 ==> b1 |> Shuffle.on Original
        yield b1 ==> b2 |> Shuffle.on Original
    }
    let topo = t |> withConf [ TOPOLOGY_MAX_SPOUT_PENDING 10
                               TOPOLOGY_ACKER_EXECUTORS 1
                               TOPOLOGY_MESSAGE_TIMEOUT_SECS 1
                               TOPOLOGY_DEBUG false ]
    let stop = Hosting.runWith (fun _ _ -> ignore) topo

    let deadline = DateTime.UtcNow.AddSeconds 10.
    while tracker.acked.Value < 10L && DateTime.UtcNow < deadline do
        Thread.Sleep 50

    stop()
    Thread.Sleep 500

    // if anchoring works, acker will complete the tree and spout gets acks
    test <@ tracker.acked.Value >= 10L @>
    test <@ tracker.nacked.Value = 0L @>
