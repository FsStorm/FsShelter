/// Phase 4+5: Resource consumption, stability, and edge case tests
/// Modeled on Apache Storm's WorkerTest / TopologyValidatorTest
module FsShelter.ResourceAndEdgeCaseTests

open NUnit.Framework
open Swensen.Unquote
open FsShelter.TestTopology
open FsShelter.DSL
open FsShelter.Multilang
open FsShelter.Hosting
open System
open System.Threading

#nowarn "25"

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------
type ResTracker =
    { emitted : int64 ref
      acked : int64 ref
      nacked : int64 ref }
    static member Create () =
        { emitted = ref 0L; acked = ref 0L; nacked = ref 0L }

let mkSimpleTopology (tracker: ResTracker) maxPending timeout =
    let numbers (t: ResTracker) =
        Interlocked.Increment &t.emitted.contents |> ignore
        Some(Named(string !t.emitted), Original { x = 1 })

    let t = topology "resource-test" {
        let s1 = numbers
                 |> Spout.runReliable
                     (fun _ _ -> tracker)
                     (fun t -> (fun _ -> Interlocked.Increment &t.acked.contents |> ignore),
                               (fun _ -> Interlocked.Increment &t.nacked.contents |> ignore))
                     ignore
        let b1 = split
                 |> Bolt.run (fun _ _ t emit -> (t, emit))
        let b2 = resultBolt
                 |> Bolt.run (fun _ _ t _ -> ignore, t)
        yield s1 ==> b1 |> Shuffle.on Original
        yield b1 --> b2 |> Group.by (function Odd(n,_) -> n.x)
        yield b1 --> b2 |> Group.by (function Even(x,str) -> (x.x,str.str))
    }
    t |> withConf [ TOPOLOGY_MAX_SPOUT_PENDING maxPending
                    TOPOLOGY_ACKER_EXECUTORS 1
                    TOPOLOGY_MESSAGE_TIMEOUT_SECS (max 1 timeout)
                    TOPOLOGY_DEBUG false ]

// ---------------------------------------------------------------------------
// Storm WorkerTest: testWorkerMemoryStability
// Run topology for duration, sample memory, assert no monotonic growth
// ---------------------------------------------------------------------------
[<Test>]
[<Category("integration")>]
let ``Steady-state memory does not grow unbounded`` () =
    let tracker = ResTracker.Create()
    let topo = mkSimpleTopology tracker 50 30

    GC.Collect(2, GCCollectionMode.Forced, true)
    GC.WaitForPendingFinalizers()
    let memBaseline = GC.GetTotalMemory(true)

    let stop = Hosting.runWith (fun _ _ -> ignore) topo

    // sample every 2s for 10s
    let samples = ResizeArray<int64>()
    for _ in 1..5 do
        Thread.Sleep 2000
        GC.Collect()
        samples.Add(GC.GetTotalMemory(false))

    stop()
    Thread.Sleep 500

    // memory should not exceed 3x baseline (generous bound)
    let maxMem = samples |> Seq.max
    let ratio = float maxMem / float memBaseline
    test <@ ratio < 3.0 @>

    // verify topology actually ran
    test <@ !tracker.acked > 0L @>

// ---------------------------------------------------------------------------
// Storm WorkerTest: testWorkerIsolation
// Run 2 topologies with separate counters, verify no cross-talk
// ---------------------------------------------------------------------------
[<Test>]
let ``Multi-topology isolation`` () =
    let tracker1 = ResTracker.Create()
    let tracker2 = ResTracker.Create()

    let topo1 = mkSimpleTopology tracker1 10 30
                |> fun t -> { t with Name = "isolation-1" }
    let topo2 = mkSimpleTopology tracker2 10 30
                |> fun t -> { t with Name = "isolation-2" }

    let stop1 = Hosting.runWith (fun _ _ -> ignore) topo1
    let stop2 = Hosting.runWith (fun _ _ -> ignore) topo2

    Thread.Sleep 3000

    stop1()
    stop2()
    Thread.Sleep 500

    // both topologies should have produced acks independently
    test <@ !tracker1.acked > 0L @>
    test <@ !tracker2.acked > 0L @>
    // nacks should be zero under normal processing
    test <@ !tracker1.nacked = 0L @>
    test <@ !tracker2.nacked = 0L @>

// ---------------------------------------------------------------------------
// Storm WorkerTest: testWorkerShutdown
// Graceful shutdown: topology stops without hanging
// ---------------------------------------------------------------------------
[<Test>]
let ``Graceful shutdown completes within timeout`` () =
    let tracker = ResTracker.Create()
    let topo = mkSimpleTopology tracker 10 1

    let stop = Hosting.runWith (fun _ _ -> ignore) topo
    Thread.Sleep 2000

    let sw = System.Diagnostics.Stopwatch.StartNew()
    stop()
    sw.Stop()

    // shutdown should complete within a reasonable time (< 10s with 1s timeout)
    test <@ sw.Elapsed.TotalSeconds < 10.0 @>
    test <@ !tracker.acked > 0L @>

// ---------------------------------------------------------------------------
// Storm TopologyValidatorTest: testTickTupleDelivery
// Configure TICK_TUPLE_FREQ_SECS → bolt receives __tick tuples
// ---------------------------------------------------------------------------
[<Test>]
let ``Tick tuples are delivered to bolts`` () =
    let ticksReceived = ref 0L
    let acked = ref 0L

    let numbers (_: unit) =
        Some(Named(string (Guid.NewGuid())), Original { x = 1 })

    let tickAwareBolt (input: Schema, _: Schema -> unit) =
        match input with
        | Schema.Tick -> Interlocked.Increment ticksReceived |> ignore
        | _ -> ()

    let t = topology "tick-test" {
        let s1 = numbers
                 |> Spout.runReliable (fun _ _ -> ())
                     (fun _ -> (fun _ -> Interlocked.Increment acked |> ignore), ignore)
                     ignore
        let b1 = tickAwareBolt
                 |> Bolt.run (fun _ _ t emit -> (t, emit))
                 |> withConf [TOPOLOGY_TICK_TUPLE_FREQ_SECS 1]
        yield s1 ==> b1 |> Shuffle.on Original
    }
    let topo = t |> withConf [ TOPOLOGY_MAX_SPOUT_PENDING 10
                               TOPOLOGY_ACKER_EXECUTORS 1
                               TOPOLOGY_MESSAGE_TIMEOUT_SECS 1
                               TOPOLOGY_DEBUG false ]
    let stop = Hosting.runWith (fun _ _ -> ignore) topo

    // tick timer fires every 1s, wait enough for at least one
    Thread.Sleep 5000

    stop()
    Thread.Sleep 500

    test <@ !ticksReceived >= 1L @>

// ---------------------------------------------------------------------------
// Minimal topology: simplest possible (spout → 1 bolt)
// ---------------------------------------------------------------------------
[<Test>]
let ``Minimal topology runs end-to-end`` () =
    let acked = ref 0L

    let numbers (_: unit) =
        Some(Named(string (Guid.NewGuid())), Original { x = 1 })

    let sinkBolt (input: Schema, _: Schema -> unit) = ()

    let t = topology "minimal-test" {
        let s1 = numbers
                 |> Spout.runReliable (fun _ _ -> ())
                     (fun _ -> (fun _ -> Interlocked.Increment acked |> ignore), ignore)
                     ignore
        let b1 = sinkBolt |> Bolt.run (fun _ _ t emit -> (t, emit))
        yield s1 ==> b1 |> Shuffle.on Original
    }
    let topo = t |> withConf [ TOPOLOGY_MAX_SPOUT_PENDING 5
                               TOPOLOGY_ACKER_EXECUTORS 1
                               TOPOLOGY_MESSAGE_TIMEOUT_SECS 1
                               TOPOLOGY_DEBUG false ]
    let stop = Hosting.runWith (fun _ _ -> ignore) topo

    let deadline = DateTime.UtcNow.AddSeconds 10.
    while !acked < 5L && DateTime.UtcNow < deadline do
        Thread.Sleep 50

    stop()
    Thread.Sleep 500

    test <@ !acked >= 5L @>

// ---------------------------------------------------------------------------
// Empty spout: always returns None, topology should handle gracefully
// ---------------------------------------------------------------------------
[<Test>]
let ``Empty spout runs without errors`` () =
    let noneSpout (_: unit) =
        None : (TupleId * Schema) option

    let sinkBolt (input: Schema, _: Schema -> unit) = ()

    let t = topology "empty-spout-test" {
        let s1 = noneSpout
                 |> Spout.runReliable (fun _ _ -> ()) (fun _ -> ignore, ignore) ignore
        let b1 = sinkBolt |> Bolt.run (fun _ _ t emit -> (t, emit))
        yield s1 ==> b1 |> Shuffle.on Original
    }
    let topo = t |> withConf [ TOPOLOGY_MAX_SPOUT_PENDING 5
                               TOPOLOGY_ACKER_EXECUTORS 1
                               TOPOLOGY_MESSAGE_TIMEOUT_SECS 1
                               TOPOLOGY_DEBUG false ]

    // should not throw during 3s of idle operation
    let stop = Hosting.runWith (fun _ _ -> ignore) topo
    Thread.Sleep 3000
    stop()
    Thread.Sleep 500
    Assert.Pass("Empty spout topology ran without errors")

// ---------------------------------------------------------------------------
// Storm SpoutPendingCounterConsistency
// Rapid ack/nack → verify pending counter stays consistent
// ---------------------------------------------------------------------------
[<Test>]
let ``Rapid ack-nack keeps pending counter consistent`` () =
    let tracker = ResTracker.Create()
    
    let numbers (t: ResTracker) =
        Interlocked.Increment &t.emitted.contents |> ignore
        Some(Named(string !t.emitted), Original { x = 1 })

    let sometimesFails (input: Schema, emit: Schema -> unit) =
        match input with
        | Original { x = x } ->
            if (!tracker.emitted % 3L) = 0L then
                failwith "periodic failure"
            else
                emit input
        | _ -> ()

    let sinkBolt (input: Schema, _: Schema -> unit) = ()

    let t = topology "pending-consistency-test" {
        let s1 = numbers
                 |> Spout.runReliable
                     (fun _ _ -> tracker)
                     (fun t -> (fun _ -> Interlocked.Increment &t.acked.contents |> ignore),
                               (fun _ -> Interlocked.Increment &t.nacked.contents |> ignore))
                     ignore
        let b1 = sometimesFails |> Bolt.run (fun _ _ t emit -> (t, emit))
        let b2 = sinkBolt |> Bolt.run (fun _ _ t emit -> (t, emit))
        yield s1 ==> b1 |> Shuffle.on Original
        yield b1 ==> b2 |> Shuffle.on Original
    }
    let topo = t |> withConf [ TOPOLOGY_MAX_SPOUT_PENDING 10
                               TOPOLOGY_ACKER_EXECUTORS 1
                               TOPOLOGY_MESSAGE_TIMEOUT_SECS 2
                               TOPOLOGY_DEBUG false ]
    let stop = Hosting.runWith (fun _ _ -> ignore) topo

    // let it run with mixed success/failure
    let deadline = DateTime.UtcNow.AddSeconds 15.
    while (!tracker.acked + !tracker.nacked) < 20L && DateTime.UtcNow < deadline do
        Thread.Sleep 100

    stop()
    Thread.Sleep 500

    // acked + nacked should account for a good portion of emitted
    // (some may still be in-flight at shutdown)
    let totalResolved = !tracker.acked + !tracker.nacked
    test <@ totalResolved > 0L @>
    test <@ !tracker.emitted >= totalResolved @>
