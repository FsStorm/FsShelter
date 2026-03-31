/// Phase 1: Acker correctness tests
/// Modeled on Apache Storm's AckerTest / TupleTreeTest
module FsShelter.AckerTests

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

type Tracker =
    { emitted : int64 ref
      acked : int64 ref
      nacked : int64 ref
      ackIds : ResizeArray<TupleId>
      nackIds : ResizeArray<TupleId> }
    static member Create () =
        { emitted = ref 0L; acked = ref 0L; nacked = ref 0L
          ackIds = ResizeArray(); nackIds = ResizeArray() }

let mkTrackedTopology (tracker: Tracker) maxPending timeout =
    let numbers (t: Tracker) =
        Interlocked.Increment &t.emitted.contents |> ignore
        Some(Named(string !t.emitted), Original { x = 1 })

    let t = topology "acker-test" {
        let s1 = numbers
                 |> Spout.runReliable
                     (fun _ _ -> tracker)
                     (fun t -> (fun tid -> Interlocked.Increment &t.acked.contents |> ignore; lock t.ackIds (fun () -> t.ackIds.Add tid)),
                               (fun tid -> Interlocked.Increment &t.nacked.contents |> ignore; lock t.nackIds (fun () -> t.nackIds.Add tid)))
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
// Storm AckerTest: testAckerAcksSingleTuple
// ---------------------------------------------------------------------------
[<Test>]
let ``Acker tracks and acks a single tuple`` () =
    let tracker = Tracker.Create()
    let t = mkTrackedTopology tracker 1 30
    let stop = Hosting.runWith (fun _ _ -> ignore) t

    // wait for at least 1 ack
    let deadline = DateTime.UtcNow.AddSeconds 10.
    while !tracker.acked < 1L && DateTime.UtcNow < deadline do
        Thread.Sleep 50

    stop()
    Thread.Sleep 500

    test <@ !tracker.acked >= 1L @>
    test <@ !tracker.nacked = 0L @>

// ---------------------------------------------------------------------------
// Storm AckerTest: testAckerHandlesTupleTree
// Multi-hop: spout → bolt1 (anchored emit) → bolt2 (ack)
// Verify full tree completes — acker XORs to zero
// ---------------------------------------------------------------------------
[<Test>]
let ``Acker tracks multi-hop tuple tree`` () =
    let tracker = Tracker.Create()
    let t = mkTrackedTopology tracker 10 30
    let stop = Hosting.runWith (fun _ _ -> ignore) t

    // wait for several acks to confirm multi-hop tree completion
    let deadline = DateTime.UtcNow.AddSeconds 10.
    while !tracker.acked < 5L && DateTime.UtcNow < deadline do
        Thread.Sleep 50

    stop()
    Thread.Sleep 500

    test <@ !tracker.acked >= 5L @>
    test <@ !tracker.nacked = 0L @>
    // every emitted tuple that was acked means the full tree completed
    test <@ !tracker.emitted >= !tracker.acked @>

// ---------------------------------------------------------------------------
// Storm AckerTest: testAckerFailsOnBoltFailure
// Bolt throws → acker receives Fail → spout gets Nack
// ---------------------------------------------------------------------------
[<Test>]
let ``Acker nacks on bolt failure`` () =
    let tracker = Tracker.Create()
    let failCount = ref 0L

    let failNumbers (t: Tracker) =
        Interlocked.Increment &t.emitted.contents |> ignore
        Some(Named(string !t.emitted), Original { x = 1 })

    let failingBolt (input: Schema, emit: Schema -> unit) =
        Interlocked.Increment failCount |> ignore
        failwith "simulated bolt failure"

    let t = topology "acker-fail-test" {
        let s1 = failNumbers
                 |> Spout.runReliable
                     (fun _ _ -> tracker)
                     (fun t -> (fun _ -> Interlocked.Increment &t.acked.contents |> ignore),
                               (fun _ -> Interlocked.Increment &t.nacked.contents |> ignore))
                     ignore
        let b1 = failingBolt
                 |> Bolt.run (fun _ _ t emit -> (t, emit))
        yield s1 ==> b1 |> Shuffle.on Original
    }
    let topo = t |> withConf [ TOPOLOGY_MAX_SPOUT_PENDING 2
                               TOPOLOGY_ACKER_EXECUTORS 1
                               TOPOLOGY_MESSAGE_TIMEOUT_SECS 2
                               TOPOLOGY_DEBUG false ]

    let errors = ResizeArray<exn>()
    let stop = Hosting.runWith (fun _ _ -> ignore) topo

    // wait for nacks (bolt failures cause either immediate Fail or timeout)
    let deadline = DateTime.UtcNow.AddSeconds 10.
    while !tracker.nacked < 1L && DateTime.UtcNow < deadline do
        Thread.Sleep 100

    stop()
    Thread.Sleep 500

    test <@ !tracker.nacked >= 1L @>

// ---------------------------------------------------------------------------
// Storm AckerTest: testAckerTimesOutTuples
// Emit tuple, bolt never acks, wait > timeout → spout gets Nack
// NOTE: The system timer ticks every 30s, so expiry check requires ~30s wait
// ---------------------------------------------------------------------------
[<Test>]
[<Category("interactive")>]
let ``Acker expires timed-out tuples`` () =
    let tracker = Tracker.Create()
    let emittedOnce = ref false

    let oneNumber (t: Tracker) =
        if not !emittedOnce then
            emittedOnce := true
            Interlocked.Increment &t.emitted.contents |> ignore
            Some(Named(string !t.emitted), Original { x = 42 })
        else
            None

    // bolt that hangs forever (never acks by sleeping)
    let hangingBolt (input: Schema, _: Schema -> unit) =
        Thread.Sleep(Timeout.Infinite)

    let t = topology "acker-timeout-test" {
        let s1 = oneNumber
                 |> Spout.runReliable
                     (fun _ _ -> tracker)
                     (fun t -> (fun _ -> Interlocked.Increment &t.acked.contents |> ignore),
                               (fun _ -> Interlocked.Increment &t.nacked.contents |> ignore))
                     ignore
        let b1 = hangingBolt
                 |> Bolt.run (fun _ _ t emit -> (t, emit))
        yield s1 ==> b1 |> Shuffle.on Original
    }
    // very short timeout to trigger expiry quickly
    let topo = t |> withConf [ TOPOLOGY_MAX_SPOUT_PENDING 2
                               TOPOLOGY_ACKER_EXECUTORS 1
                               TOPOLOGY_MESSAGE_TIMEOUT_SECS 1
                               TOPOLOGY_DEBUG false ]
    let stop = Hosting.runWith (fun _ _ -> ignore) topo

    // system timer ticks every 30s by default, but acker tick is on same timer
    // We need to wait for timeout + tick interval. Timeout is 1s, tick is 30s.
    // With self-hosted mode, the acker gets Tick from the system timer at 30s intervals.
    // For a 1s timeout, we may need to wait up to 30s for the tick to fire expiry check.
    let deadline = DateTime.UtcNow.AddSeconds 35.
    while !tracker.nacked < 1L && DateTime.UtcNow < deadline do
        Thread.Sleep 200

    stop()
    Thread.Sleep 500

    test <@ !tracker.emitted >= 1L @>
    test <@ !tracker.nacked >= 1L @>

// ---------------------------------------------------------------------------
// Storm AckerTest: testAckerBackpressure
// Exceed highWater*2 → acker nacks overflow tuples
// Uses a very small buffer to trigger capacity limit
// NOTE: Requires system timer tick (30s) to trigger nack via timeout
// ---------------------------------------------------------------------------
[<Test>]
[<Category("interactive")>]
let ``Acker capacity overflow nacks tuples`` () =
    let tracker = Tracker.Create()

    let fastNumbers (t: Tracker) =
        Interlocked.Increment &t.emitted.contents |> ignore
        Some(Named(string !t.emitted), Original { x = 1 })

    // bot that never acks - will cause acker inFlight to grow
    let blackHole (input: Schema, _: Schema -> unit) = ()

    let t = topology "acker-overflow-test" {
        let s1 = fastNumbers
                 |> Spout.runReliable
                     (fun _ _ -> tracker)
                     (fun t -> (fun _ -> Interlocked.Increment &t.acked.contents |> ignore),
                               (fun _ -> Interlocked.Increment &t.nacked.contents |> ignore))
                     ignore
        let b1 = blackHole
                 |> Bolt.run (fun _ _ t emit -> (t, emit))
        yield s1 ==> b1 |> Shuffle.on Original
    }
    // Very small ring buffer = small highWater, large maxPending to flood the acker
    let topo = t |> withConf [ TOPOLOGY_MAX_SPOUT_PENDING 512
                               TOPOLOGY_ACKER_EXECUTORS 1
                               TOPOLOGY_EXECUTOR_RECEIVE_BUFFER_SIZE 8
                               TOPOLOGY_MESSAGE_TIMEOUT_SECS 1
                               TOPOLOGY_DEBUG false ]
    let stop = Hosting.runWith (fun _ _ -> ignore) topo

    // let the acker fill up and either expire or overflow
    let deadline = DateTime.UtcNow.AddSeconds 35.
    while !tracker.nacked < 1L && DateTime.UtcNow < deadline do
        Thread.Sleep 200

    stop()
    Thread.Sleep 500

    test <@ !tracker.nacked >= 1L @>
