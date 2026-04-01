module FsShelter.PerfBench

open FsShelter.TestTopology
open FsShelter.DSL
open FsShelter.Multilang
open System
open System.Threading
open System.Diagnostics
open System.Diagnostics.Metrics
open System.Collections.Generic

module TelemetryCollector =
    let mutable private counters = Dictionary<string, int64>()
    let mutable private histogramCounts = Dictionary<string, int64>()
    let mutable private histogramSums = Dictionary<string, float>()
    let mutable private listener : MeterListener option = None

    let start () =
        counters <- Dictionary<string, int64>()
        histogramCounts <- Dictionary<string, int64>()
        histogramSums <- Dictionary<string, float>()
        let ml = new MeterListener()
        ml.InstrumentPublished <- Action<Instrument, MeterListener>(fun instrument l ->
            if instrument.Meter.Name = "FsShelter.Hosting" then
                l.EnableMeasurementEvents(instrument))
        ml.SetMeasurementEventCallback<int64>(fun instrument value _ _ ->
            lock counters (fun () ->
                let key = instrument.Name
                match counters.TryGetValue key with
                | true, v -> counters.[key] <- v + value
                | _ -> counters.[key] <- value))
        ml.SetMeasurementEventCallback<int>(fun instrument value _ _ ->
            lock counters (fun () ->
                let key = instrument.Name
                match counters.TryGetValue key with
                | true, v -> counters.[key] <- v + int64 value
                | _ -> counters.[key] <- int64 value))
        ml.SetMeasurementEventCallback<float>(fun instrument value _ _ ->
            lock histogramCounts (fun () ->
                let key = instrument.Name
                match histogramCounts.TryGetValue key with
                | true, c -> histogramCounts.[key] <- c + 1L
                             histogramSums.[key] <- histogramSums.[key] + value
                | _ -> histogramCounts.[key] <- 1L
                       histogramSums.[key] <- value))
        ml.Start()
        listener <- Some ml

    let stop () =
        listener |> Option.iter (fun l -> l.RecordObservableInstruments(); l.Dispose())
        listener <- None

    let print () =
        printfn "--- Telemetry ---"
        for (KeyValue(k, v)) in counters do
            printfn "  %-40s %d" k v
        for (KeyValue(k, c)) in histogramCounts do
            let avg = histogramSums.[k] / float c
            printfn "  %-40s count=%d avg=%.3f" k c avg
        printfn ""

module Bench =
    type BenchWorld = 
        { rnd : Random
          count : int64 ref
          acked : int64 ref }

    let benchNumbers (world : BenchWorld) =
        Interlocked.Increment &world.count.contents |> ignore
        Some(Named(string world.count.Value), Original { x = world.rnd.Next(0, 100) }) 

    let run durationMs maxPending spoutWaitMs boltParallelism boltExecutors =
        let world = { rnd = Random(42); count = ref 0L; acked = ref 0L }

        let t = topology "perf-bench" {
            let s1 = benchNumbers
                     |> Spout.runReliable 
                         (fun _ _ -> world)
                         (fun w -> (fun _ -> Interlocked.Increment &w.acked.contents |> ignore), ignore)
                         ignore                                    
            let b1 = split
                     |> Bolt.run (fun _ _ t emit -> (t,emit))
                     |> withParallelism boltParallelism
                     |> withExecutors boltExecutors
            let b2 = resultBolt
                     |> Bolt.run (fun _ _ t _ -> ignore, t)
            yield s1 ==> b1 |> Shuffle.on Original
            yield b1 --> b2 |> Group.by (function Odd(n,_) -> (n.x) | _ -> failwith "unexpected") 
            yield b1 --> b2 |> Group.by (function Even(x,str) -> (x.x,str.str) | _ -> failwith "unexpected")
        }

        TelemetryCollector.start()

        let proc = Process.GetCurrentProcess()
        let gcBefore = GC.CollectionCount 0, GC.CollectionCount 1, GC.CollectionCount 2
        let memBefore = GC.GetTotalMemory(true)
        let cpuBefore = proc.TotalProcessorTime.TotalMilliseconds

        let nextPow2 n = let mutable v = n - 1 in v <- v ||| (v >>> 1); v <- v ||| (v >>> 2); v <- v ||| (v >>> 4); v <- v ||| (v >>> 8); v <- v ||| (v >>> 16); v + 1
        let ringSize = nextPow2 (max 256 (maxPending * 2))
        let stop = 
            t 
            |> withConf [ TOPOLOGY_MAX_SPOUT_PENDING maxPending
                          TOPOLOGY_ACKER_EXECUTORS 2
                          TOPOLOGY_MESSAGE_TIMEOUT_SECS 10
                          TOPOLOGY_EXECUTOR_RECEIVE_BUFFER_SIZE ringSize
                          TOPOLOGY_SLEEP_SPOUT_WAIT_STRATEGY_TIME_MS spoutWaitMs ]
            |> Hosting.runWith (fun _ _ -> ignore)

        let sw = Stopwatch.StartNew()
        Thread.Sleep(durationMs: int)
        sw.Stop()

        let emitted = world.count.Value
        let acked = world.acked.Value
        let cpuAfter = proc.TotalProcessorTime.TotalMilliseconds
        let memAfter = GC.GetTotalMemory(false)
        let gc0, gc1, gc2 = GC.CollectionCount 0 - (let (a,_,_) = gcBefore in a),
                            GC.CollectionCount 1 - (let (_,a,_) = gcBefore in a),
                            GC.CollectionCount 2 - (let (_,_,a) = gcBefore in a)

        stop()
        Thread.Sleep(500) // allow shutdown
        TelemetryCollector.stop()

        let elapsed = sw.Elapsed.TotalSeconds
        {| Emitted = emitted
           Acked = acked
           InFlight = emitted - acked
           ElapsedSec = elapsed
           Rate = float acked / elapsed
           CpuMs = cpuAfter - cpuBefore
           MemDeltaKB = (memAfter - memBefore) / 1024L
           GC0 = gc0; GC1 = gc1; GC2 = gc2 |}

    let printResult label (r: {| Emitted: int64; Acked: int64; InFlight: int64; ElapsedSec: float; Rate: float; CpuMs: float; MemDeltaKB: int64; GC0: int; GC1: int; GC2: int |}) =
        printfn "=== %s ===" label
        printfn "  Emitted:    %d" r.Emitted
        printfn "  Acked:      %d" r.Acked
        printfn "  In-flight:  %d" r.InFlight
        printfn "  Elapsed:    %.2fs" r.ElapsedSec
        printfn "  Throughput: %.0f msg/s" r.Rate
        printfn "  CPU time:   %.0f ms" r.CpuMs
        printfn "  CPU util:   %.0f%%" (r.CpuMs / (r.ElapsedSec * 1000.0) * 100.0)
        printfn "  Mem delta:  %d KB" r.MemDeltaKB
        printfn "  GC 0/1/2:   %d/%d/%d" r.GC0 r.GC1 r.GC2
        printfn ""

module BackpressureBench =
    type BPWorld = 
        { rnd : Random
          count : int64 ref
          acked : int64 ref
          nacked : int64 ref }

    let bpNumbers (world : BPWorld) =
        Interlocked.Increment &world.count.contents |> ignore
        Some(Named(string world.count.Value), Original { x = world.rnd.Next(0, 100) }) 

    /// A split bolt that introduces a fixed delay to simulate slow processing
    let slowSplit (delayMs: int) (input, emit) =
        Thread.SpinWait(delayMs * 1000) // spin-wait to simulate CPU work without yielding thread
        match input with
        | Original { x = x } -> 
            match x % 2 with
            | 0 -> Even ({x=x}, {str="even"})
            | _ -> Odd ({x=x}, "odd")
        | _ -> failwithf "unexpected input: %A" input
        |> emit

    /// A split bolt that sleeps a random time to simulate variable-latency processing (e.g. external I/O)
    let jittySplit (rnd: Random) (minMs: int) (maxMs: int) (input, emit) =
        Thread.Sleep(rnd.Next(minMs, maxMs))
        match input with
        | Original { x = x } -> 
            match x % 2 with
            | 0 -> Even ({x=x}, {str="even"})
            | _ -> Odd ({x=x}, "odd")
        | _ -> failwithf "unexpected input: %A" input
        |> emit

    let run durationMs maxPending boltDelayMs boltParallelism =
        let world = { rnd = Random(42); count = ref 0L; acked = ref 0L; nacked = ref 0L }

        let t = topology "bp-bench" {
            let s1 = bpNumbers
                     |> Spout.runReliable 
                         (fun _ _ -> world)
                         (fun w -> (fun _ -> Interlocked.Increment &w.acked.contents |> ignore), 
                                   (fun _ -> Interlocked.Increment &w.nacked.contents |> ignore))
                         ignore                                    
            let b1 = slowSplit boltDelayMs
                     |> Bolt.run (fun _ _ t emit -> (t,emit))
                     |> withParallelism boltParallelism
            let b2 = resultBolt
                     |> Bolt.run (fun _ _ t _ -> ignore, t)
            yield s1 ==> b1 |> Shuffle.on Original
            yield b1 --> b2 |> Group.by (function Odd(n,_) -> (n.x) | _ -> failwith "unexpected") 
            yield b1 --> b2 |> Group.by (function Even(x,str) -> (x.x,str.str) | _ -> failwith "unexpected")
        }

        TelemetryCollector.start()

        let proc = Process.GetCurrentProcess()
        let gcBefore = GC.CollectionCount 0, GC.CollectionCount 1, GC.CollectionCount 2
        let memBefore = GC.GetTotalMemory(true)
        let cpuBefore = proc.TotalProcessorTime.TotalMilliseconds

        let stop = 
            t 
            |> withConf [ TOPOLOGY_MAX_SPOUT_PENDING maxPending
                          TOPOLOGY_ACKER_EXECUTORS 2
                          TOPOLOGY_MESSAGE_TIMEOUT_SECS 30 ]
            |> Hosting.runWith (fun _ _ -> ignore)

        let sw = Stopwatch.StartNew()
        Thread.Sleep(durationMs: int)
        sw.Stop()

        let emitted = world.count.Value
        let acked = world.acked.Value
        let nacked = world.nacked.Value
        let cpuAfter = proc.TotalProcessorTime.TotalMilliseconds
        let memAfter = GC.GetTotalMemory(false)
        let gc0, gc1, gc2 = GC.CollectionCount 0 - (let (a,_,_) = gcBefore in a),
                            GC.CollectionCount 1 - (let (_,a,_) = gcBefore in a),
                            GC.CollectionCount 2 - (let (_,_,a) = gcBefore in a)

        stop()
        Thread.Sleep(500)
        TelemetryCollector.stop()

        let elapsed = sw.Elapsed.TotalSeconds
        {| Emitted = emitted
           Acked = acked
           Nacked = nacked
           InFlight = emitted - acked - nacked
           ElapsedSec = elapsed
           Rate = float acked / elapsed
           MaxPending = maxPending
           CpuMs = cpuAfter - cpuBefore
           MemDeltaKB = (memAfter - memBefore) / 1024L
           GC0 = gc0; GC1 = gc1; GC2 = gc2 |}

    let printResult label (r: {| Emitted: int64; Acked: int64; Nacked: int64; InFlight: int64; ElapsedSec: float; Rate: float; MaxPending: int; CpuMs: float; MemDeltaKB: int64; GC0: int; GC1: int; GC2: int |}) =
        printfn "=== %s ===" label
        printfn "  MaxPending: %d" r.MaxPending
        printfn "  Emitted:    %d" r.Emitted
        printfn "  Acked:      %d" r.Acked
        printfn "  Nacked:     %d" r.Nacked
        printfn "  In-flight:  %d" r.InFlight
        printfn "  Elapsed:    %.2fs" r.ElapsedSec
        printfn "  Throughput: %.0f msg/s" r.Rate
        printfn "  CPU time:   %.0f ms" r.CpuMs
        printfn "  CPU util:   %.0f%%" (r.CpuMs / (r.ElapsedSec * 1000.0) * 100.0)
        printfn "  Mem delta:  %d KB" r.MemDeltaKB
        printfn "  GC 0/1/2:   %d/%d/%d" r.GC0 r.GC1 r.GC2
        printfn ""

[<NUnit.Framework.Test>]
[<NUnit.Framework.Category("perf")>]
[<NUnit.Framework.TestCase(100, 128, 2, 2)>]  // default config (baseline)
[<NUnit.Framework.TestCase(10, 16, 2, 2)>]    // tight backpressure
[<NUnit.Framework.TestCase(10, 1280, 2, 2)>]  // deep buffer — acker/GC stress
[<NUnit.Framework.TestCase(10, 128, 4, 4)>]   // high fan-out contention
[<NUnit.Framework.TestCase(10, 128, 4, 1)>]   // 4 tasks on 1 executor (shared thread)
let ``Throughput benchmark`` (spoutWaitMs: int) (maxPending: int) (boltParallelism: int) (boltExecutors: int) =
    printfn "Running benchmark (5s, wait=%d, pending=%d, par=%d, exec=%d)..." spoutWaitMs maxPending boltParallelism boltExecutors
    let result = Bench.run 5000 maxPending spoutWaitMs boltParallelism boltExecutors
    Bench.printResult (sprintf "Throughput (wait=%d, pending=%d, par=%d, exec=%d)" spoutWaitMs maxPending boltParallelism boltExecutors) result
    TelemetryCollector.print()

[<NUnit.Framework.Test>]
[<NUnit.Framework.Category("perf")>]
let ``Backpressure benchmark - slow bolts`` () =
    printfn "Running backpressure benchmark (slow bolts vs fast spout)..."
    printfn ""

    // Baseline: fast bolts, high maxPending
    let baseline = Bench.run 3000 128 100 2 2
    Bench.printResult "Baseline (fast bolts, maxPending=128)" baseline

    // Slow bolts with tight maxPending — should throttle spout
    let tight = BackpressureBench.run 3000 16 50 2
    BackpressureBench.printResult "Slow bolts (delay=50, maxPending=16, par=2)" tight

    // Slow bolts with higher maxPending — allows more buffering
    let loose = BackpressureBench.run 3000 128 50 2
    BackpressureBench.printResult "Slow bolts (delay=50, maxPending=128, par=2)" loose

    // Slow bolts with more parallelism to compensate
    let scaled = BackpressureBench.run 3000 64 50 4
    BackpressureBench.printResult "Slow bolts (delay=50, maxPending=64, par=4)" scaled

    // Verify backpressure: tight maxPending should have lower throughput
    printfn "--- Backpressure analysis ---"
    printfn "  Baseline throughput:     %.0f msg/s" baseline.Rate
    printfn "  Tight BP throughput:     %.0f msg/s" tight.Rate
    printfn "  Loose BP throughput:     %.0f msg/s" loose.Rate
    printfn "  Scaled BP throughput:    %.0f msg/s" scaled.Rate
    printfn "  Tight/Baseline ratio:    %.1f%%" (tight.Rate / baseline.Rate * 100.0)
    printfn "  Scaled/Tight ratio:      %.1f%%" (scaled.Rate / tight.Rate * 100.0)
    printfn ""

    // Structural assertions
    NUnit.Framework.Assert.That(tight.Rate, NUnit.Framework.Is.LessThan(baseline.Rate), 
        "Slow bolts with tight maxPending should have lower throughput than baseline")
    NUnit.Framework.Assert.That(tight.InFlight, NUnit.Framework.Is.LessThanOrEqualTo(int64 tight.MaxPending), 
        "In-flight tuples should not exceed maxPending")
    NUnit.Framework.Assert.That(tight.Acked, NUnit.Framework.Is.GreaterThan(0L), 
        "Some tuples should still be acked even under backpressure")

    TelemetryCollector.print()

[<NUnit.Framework.Test>]
[<NUnit.Framework.Category("perf")>]
let ``Backpressure benchmark - random latency bolts`` () =
    printfn "Running random-latency backpressure benchmark..."
    printfn ""

    let runJitty durationMs maxPending minMs maxMs boltParallelism =
        let world : BackpressureBench.BPWorld = 
            { rnd = Random(42); count = ref 0L; acked = ref 0L; nacked = ref 0L }
        // each bolt task gets its own Random to avoid contention
        let mkRnd = let seed = ref 0 in fun () -> Random(Interlocked.Increment seed)

        let t = topology "jitty-bench" {
            let s1 = BackpressureBench.bpNumbers
                     |> Spout.runReliable 
                         (fun _ _ -> world)
                         (fun w -> (fun _ -> Interlocked.Increment &w.acked.contents |> ignore), 
                                   (fun _ -> Interlocked.Increment &w.nacked.contents |> ignore))
                         ignore                                    
            let b1 = BackpressureBench.jittySplit (mkRnd()) minMs maxMs
                     |> Bolt.run (fun _ _ t emit -> (t,emit))
                     |> withParallelism boltParallelism
            let b2 = resultBolt
                     |> Bolt.run (fun _ _ t _ -> ignore, t)
            yield s1 ==> b1 |> Shuffle.on Original
            yield b1 --> b2 |> Group.by (function Odd(n,_) -> (n.x) | _ -> failwith "unexpected") 
            yield b1 --> b2 |> Group.by (function Even(x,str) -> (x.x,str.str) | _ -> failwith "unexpected")
        }

        TelemetryCollector.start()

        let proc = Process.GetCurrentProcess()
        let gcBefore = GC.CollectionCount 0, GC.CollectionCount 1, GC.CollectionCount 2
        let memBefore = GC.GetTotalMemory(true)
        let cpuBefore = proc.TotalProcessorTime.TotalMilliseconds

        let stop = 
            t 
            |> withConf [ TOPOLOGY_MAX_SPOUT_PENDING maxPending
                          TOPOLOGY_ACKER_EXECUTORS 2
                          TOPOLOGY_MESSAGE_TIMEOUT_SECS 30 ]
            |> Hosting.runWith (fun _ _ -> ignore)

        let sw = Stopwatch.StartNew()
        Thread.Sleep(durationMs: int)
        sw.Stop()

        let emitted = world.count.Value
        let acked = world.acked.Value
        let nacked = world.nacked.Value
        let cpuAfter = proc.TotalProcessorTime.TotalMilliseconds
        let memAfter = GC.GetTotalMemory(false)
        let gc0, gc1, gc2 = GC.CollectionCount 0 - (let (a,_,_) = gcBefore in a),
                            GC.CollectionCount 1 - (let (_,a,_) = gcBefore in a),
                            GC.CollectionCount 2 - (let (_,_,a) = gcBefore in a)

        stop()
        Thread.Sleep(500)
        TelemetryCollector.stop()

        let elapsed = sw.Elapsed.TotalSeconds
        {| Emitted = emitted
           Acked = acked
           Nacked = nacked
           InFlight = emitted - acked - nacked
           ElapsedSec = elapsed
           Rate = float acked / elapsed
           MaxPending = maxPending
           CpuMs = cpuAfter - cpuBefore
           MemDeltaKB = (memAfter - memBefore) / 1024L
           GC0 = gc0; GC1 = gc1; GC2 = gc2 |}

    // Random latency 10-200ms per bolt — simulates real-world I/O jitter
    let jitty = runJitty 5000 64 10 200 2
    BackpressureBench.printResult "Random latency (10-200ms, maxPending=64, par=2)" jitty

    // Same jitter with more parallelism
    let jittyScaled = runJitty 5000 64 10 200 4
    BackpressureBench.printResult "Random latency (10-200ms, maxPending=64, par=4)" jittyScaled

    printfn "--- Random latency analysis ---"
    printfn "  Jitty (par=2) throughput:  %.0f msg/s" jitty.Rate
    printfn "  Jitty (par=4) throughput:  %.0f msg/s" jittyScaled.Rate
    printfn "  Scaling factor:            %.1fx" (jittyScaled.Rate / jitty.Rate)
    printfn ""

    NUnit.Framework.Assert.That(jitty.InFlight, NUnit.Framework.Is.LessThanOrEqualTo(int64 jitty.MaxPending), 
        "In-flight tuples should not exceed maxPending under jitter")
    NUnit.Framework.Assert.That(jitty.Acked, NUnit.Framework.Is.GreaterThan(0L), 
        "Tuples should be acked even with random-latency bolts")
    NUnit.Framework.Assert.That(jitty.Nacked, NUnit.Framework.Is.EqualTo(0L), 
        "No nacks expected — timeout is generous relative to max bolt delay")

    TelemetryCollector.print()

[<NUnit.Framework.Test>]
[<NUnit.Framework.Category("perf")>]
let ``Spout latency benchmark - random sleep in spout`` () =
    printfn "Running spout-latency benchmark (random sleep in spout next)..."
    printfn ""

    let runSlowSpout durationMs maxPending minMs maxMs =
        let rnd = Random(42)
        let world : BackpressureBench.BPWorld =
            { rnd = rnd; count = ref 0L; acked = ref 0L; nacked = ref 0L }

        let slowNumbers (w : BackpressureBench.BPWorld) =
            Thread.Sleep(w.rnd.Next(minMs, maxMs))
            Interlocked.Increment &w.count.contents |> ignore
            Some(Named(string w.count.Value), Original { x = w.rnd.Next(0, 100) })

        let t = topology "slow-spout-bench" {
            let s1 = slowNumbers
                     |> Spout.runReliable
                         (fun _ _ -> world)
                         (fun w -> (fun _ -> Interlocked.Increment &w.acked.contents |> ignore),
                                   (fun _ -> Interlocked.Increment &w.nacked.contents |> ignore))
                         ignore
            let b1 = split
                     |> Bolt.run (fun _ _ t emit -> (t,emit))
                     |> withParallelism 2
            let b2 = resultBolt
                     |> Bolt.run (fun _ _ t _ -> ignore, t)
            yield s1 ==> b1 |> Shuffle.on Original
            yield b1 --> b2 |> Group.by (function Odd(n,_) -> (n.x) | _ -> failwith "unexpected")
            yield b1 --> b2 |> Group.by (function Even(x,str) -> (x.x,str.str) | _ -> failwith "unexpected")
        }

        TelemetryCollector.start()

        let proc = Process.GetCurrentProcess()
        let gcBefore = GC.CollectionCount 0, GC.CollectionCount 1, GC.CollectionCount 2
        let memBefore = GC.GetTotalMemory(true)
        let cpuBefore = proc.TotalProcessorTime.TotalMilliseconds

        let stop =
            t
            |> withConf [ TOPOLOGY_MAX_SPOUT_PENDING maxPending
                          TOPOLOGY_ACKER_EXECUTORS 2
                          TOPOLOGY_MESSAGE_TIMEOUT_SECS 30 ]
            |> Hosting.runWith (fun _ _ -> ignore)

        let sw = Stopwatch.StartNew()
        Thread.Sleep(durationMs: int)
        sw.Stop()

        let emitted = world.count.Value
        let acked = world.acked.Value
        let nacked = world.nacked.Value
        let cpuAfter = proc.TotalProcessorTime.TotalMilliseconds
        let memAfter = GC.GetTotalMemory(false)
        let gc0, gc1, gc2 = GC.CollectionCount 0 - (let (a,_,_) = gcBefore in a),
                            GC.CollectionCount 1 - (let (_,a,_) = gcBefore in a),
                            GC.CollectionCount 2 - (let (_,_,a) = gcBefore in a)

        stop()
        Thread.Sleep(500)
        TelemetryCollector.stop()

        let elapsed = sw.Elapsed.TotalSeconds
        {| Emitted = emitted
           Acked = acked
           Nacked = nacked
           InFlight = emitted - acked - nacked
           ElapsedSec = elapsed
           Rate = float acked / elapsed
           MaxPending = maxPending
           CpuMs = cpuAfter - cpuBefore
           MemDeltaKB = (memAfter - memBefore) / 1024L
           GC0 = gc0; GC1 = gc1; GC2 = gc2 |}

    // Spout sleeps 1-5ms per emit — simulates DB/API polling latency
    let fast = runSlowSpout 5000 64 1 5
    BackpressureBench.printResult "Slow spout (1-5ms, maxPending=64)" fast

    // Spout sleeps 10-50ms — heavier I/O
    let slow = runSlowSpout 5000 64 10 50
    BackpressureBench.printResult "Slow spout (10-50ms, maxPending=64)" slow

    printfn "--- Spout latency analysis ---"
    printfn "  Fast spout (1-5ms):   %.0f msg/s" fast.Rate
    printfn "  Slow spout (10-50ms): %.0f msg/s" slow.Rate
    printfn ""

    NUnit.Framework.Assert.That(fast.Acked, NUnit.Framework.Is.GreaterThan(0L),
        "Tuples should be acked with slow spout")
    NUnit.Framework.Assert.That(slow.Acked, NUnit.Framework.Is.GreaterThan(0L),
        "Tuples should be acked even with very slow spout")

    TelemetryCollector.print()
