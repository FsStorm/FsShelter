/// Phase 2: Spout liveness & backpressure tests
/// Modeled on Apache Storm's SpoutOutputCollectorTest
module FsShelter.SpoutTests

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

type SpoutTracker =
    { emitted : int64 ref
      acked : int64 ref
      nacked : int64 ref
      nackIds : ResizeArray<TupleId> }
    static member Create () =
        { emitted = ref 0L; acked = ref 0L; nacked = ref 0L
          nackIds = ResizeArray() }

// ---------------------------------------------------------------------------
// Storm SpoutOutputCollectorTest: testMaxSpoutPending
// With MAX_SPOUT_PENDING=N, verify pending never significantly exceeds N
// ---------------------------------------------------------------------------
[<Test>]
let ``Spout respects maxPending backpressure`` () =
    let tracker = SpoutTracker.Create()
    let maxPending = 2

    let numbers (t: SpoutTracker) =
        Interlocked.Increment &t.emitted.contents |> ignore
        Some(Named(string !t.emitted), Original { x = 1 })

    let slowBolt (input: Schema, emit: Schema -> unit) =
        Thread.Sleep 100
        emit input

    let sinkBolt (input: Schema, _: Schema -> unit) = ()

    let t = topology "spout-pending-test" {
        let s1 = numbers
                 |> Spout.runReliable
                     (fun _ _ -> tracker)
                     (fun t -> (fun _ -> Interlocked.Increment &t.acked.contents |> ignore),
                               (fun _ -> Interlocked.Increment &t.nacked.contents |> ignore))
                     ignore
        let b1 = slowBolt |> Bolt.run (fun _ _ t emit -> (t, emit))
        let b2 = sinkBolt |> Bolt.run (fun _ _ t emit -> (t, emit))
        yield s1 ==> b1 |> Shuffle.on Original
        yield b1 ==> b2 |> Shuffle.on Original
    }
    let topo = t |> withConf [ TOPOLOGY_MAX_SPOUT_PENDING maxPending
                               TOPOLOGY_ACKER_EXECUTORS 1
                               TOPOLOGY_MESSAGE_TIMEOUT_SECS 1
                               TOPOLOGY_DEBUG false ]
    let stop = Hosting.runWith (fun _ _ -> ignore) topo

    Thread.Sleep 3000

    // sample in-flight: should be bounded by maxPending (+ small margin for non-atomic check)
    let inFlight = !tracker.emitted - !tracker.acked
    stop()
    Thread.Sleep 500

    // with maxPending=2, in-flight should be bounded — allow margin for timing
    test <@ !tracker.acked > 0L @>
    test <@ inFlight <= int64 (maxPending + 5) @>

// ---------------------------------------------------------------------------
// Storm SpoutOutputCollectorTest: testSpoutReplayOnFail
// Nack a tuple → verify spout nack callback fires
// ---------------------------------------------------------------------------
[<Test>]
let ``Spout nack callback fires on failure`` () =
    let tracker = SpoutTracker.Create()

    let numbers (t: SpoutTracker) =
        Interlocked.Increment &t.emitted.contents |> ignore
        Some(Named(string !t.emitted), Original { x = 1 })

    let failingBolt (input: Schema, _: Schema -> unit) =
        failwith "bolt failure for nack test"

    let t = topology "spout-nack-test" {
        let s1 = numbers
                 |> Spout.runReliable
                     (fun _ _ -> tracker)
                     (fun t -> (fun _ -> Interlocked.Increment &t.acked.contents |> ignore),
                               (fun tid -> Interlocked.Increment &t.nacked.contents |> ignore
                                           lock t.nackIds (fun () -> t.nackIds.Add tid)))
                     ignore
        let b1 = failingBolt |> Bolt.run (fun _ _ t emit -> (t, emit))
        yield s1 ==> b1 |> Shuffle.on Original
    }
    let topo = t |> withConf [ TOPOLOGY_MAX_SPOUT_PENDING 2
                               TOPOLOGY_ACKER_EXECUTORS 1
                               TOPOLOGY_MESSAGE_TIMEOUT_SECS 2
                               TOPOLOGY_DEBUG false ]
    let stop = Hosting.runWith (fun _ _ -> ignore) topo

    let deadline = DateTime.UtcNow.AddSeconds 10.
    while !tracker.nacked < 1L && DateTime.UtcNow < deadline do
        Thread.Sleep 100

    stop()
    Thread.Sleep 500

    test <@ !tracker.nacked >= 1L @>
    test <@ tracker.nackIds.Count >= 1 @>

// ---------------------------------------------------------------------------
// Storm SpoutOutputCollectorTest: testSpoutActivateDeactivate
// Verify spout stops and resumes correctly through activate/deactivate cycle
// ---------------------------------------------------------------------------
[<Test>]
let ``Spout handles activate-deactivate cycle via topology restart`` () =
    let tracker = SpoutTracker.Create()

    let numbers (t: SpoutTracker) =
        Interlocked.Increment &t.emitted.contents |> ignore
        Some(Named(string !t.emitted), Original { x = 1 })

    let sinkBolt (input: Schema, _: Schema -> unit) = ()

    let t = topology "spout-activate-test" {
        let s1 = numbers
                 |> Spout.runReliable
                     (fun _ _ -> tracker)
                     (fun t -> (fun _ -> Interlocked.Increment &t.acked.contents |> ignore), ignore)
                     ignore
        let b1 = sinkBolt |> Bolt.run (fun _ _ t emit -> (t, emit))
        yield s1 ==> b1 |> Shuffle.on Original
    }
    let topo = t |> withConf [ TOPOLOGY_MAX_SPOUT_PENDING 10
                               TOPOLOGY_ACKER_EXECUTORS 1
                               TOPOLOGY_MESSAGE_TIMEOUT_SECS 1
                               TOPOLOGY_DEBUG false ]

    // start, let it run, stop, check state progressed, start again
    let stop1 = Hosting.runWith (fun _ _ -> ignore) topo
    Thread.Sleep 2000
    let countAfterFirst = !tracker.emitted
    stop1()
    Thread.Sleep 500

    test <@ countAfterFirst > 0L @>

    // second run resumes
    let stop2 = Hosting.runWith (fun _ _ -> ignore) topo
    Thread.Sleep 2000
    let countAfterSecond = !tracker.emitted
    stop2()
    Thread.Sleep 500

    test <@ countAfterSecond > countAfterFirst @>

// ---------------------------------------------------------------------------
// Storm SpoutOutputCollectorTest: testSpoutIdleHandling
// Spout returns None → no crash, no unbounded resource growth
// ---------------------------------------------------------------------------
[<Test>]
let ``Idle spout does not leak resources`` () =
    let emitAttempts = ref 0L

    let noneSpout (_: unit) =
        Interlocked.Increment emitAttempts |> ignore
        None : (TupleId * Schema) option

    let sinkBolt (input: Schema, _: Schema -> unit) = ()

    let t = topology "spout-idle-test" {
        let s1 = noneSpout
                 |> Spout.runReliable (fun _ _ -> ()) (fun _ -> ignore, ignore) ignore
        let b1 = sinkBolt |> Bolt.run (fun _ _ t emit -> (t, emit))
        yield s1 ==> b1 |> Shuffle.on Original
    }
    let topo = t |> withConf [ TOPOLOGY_MAX_SPOUT_PENDING 10
                               TOPOLOGY_ACKER_EXECUTORS 1
                               TOPOLOGY_MESSAGE_TIMEOUT_SECS 1
                               TOPOLOGY_DEBUG false ]

    GC.Collect()
    let memBefore = GC.GetTotalMemory(true)

    let stop = Hosting.runWith (fun _ _ -> ignore) topo
    Thread.Sleep 5000
    stop()
    Thread.Sleep 500

    GC.Collect()
    let memAfter = GC.GetTotalMemory(true)

    // spout should have been called many times but produced nothing
    test <@ !emitAttempts > 0L @>
    // memory should not grow significantly (< 10MB for 5 seconds of idle)
    let growth = (memAfter - memBefore) / 1024L / 1024L
    test <@ growth < 10L @>
