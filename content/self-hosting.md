## Running Topologies

We call running a FsShelter topology entirely inside a .NET process "self-hosting".
The feature simulates Storm cluster and can be used for testing or production deployments where scale-out capability of Storm is not required.
The 2.0 release of FsShelter reimplements this feature using the same technology Storm does - Disruptor queues, greatly improving the performance and further reducing overhead.

Limitations:

* only FsShelter components can be self-hosted, no shell or java;

* only does in-proc message passing - can't scale out.

Benefits:

* Self-hosted F# components perform on par with native JVM components under Storm, while taking as little as 30MB and ~20 threads.

* no Storm, zookeeper or even JRE are required, while delivering the same processing guarantees including tuple timeouts.

Start small then scale out to run on a cluster, with no changes to your code! Use the same building blocks that run in the cloud... anywhere!

For details refer to the Guaranteed sample, which demonstrates how to run the topology either way.

See also:

* [Architecture](architecture.html) — Internal runtime structure: tasks, executors, channels, topology construction, and lifecycle.

* [Message flow](message-flow.html) — End-to-end processing scenarios with diagrams: reliable/unreliable delivery, backpressure, timeouts, failures, and restarts.

* [Acker algorithm](acker-algorithm.html) — XOR-tree tuple tracking: how anchoring, acking, and nacking work to provide guaranteed message processing.

* [Routing](routing.html) — Tuple routing between components: grouping strategies (Shuffle, Fields, All, Direct) and stream-based fan-out.

## Entry points

### `Hosting.run`

Simplest entry point. No logging, with auto-restart (delegates to `runWith`). Returns a shutdown function:

```fsharp
let run topology = runWith (fun _ _ -> ignore) topology

```

### `Hosting.runWith` (auto-restart with backoff)

Production entry point. Accepts a logging factory and auto-restarts on unhandled exceptions with exponential backoff and a maximum restart count:

```fsharp
let runWith (startLog: int -> Log) (topology: Topology<'t>) =
    let maxRestarts = 5
    let mutable restartCount = 0
    let rec restart (ex: exn) =
        if restartCount >= maxRestarts then
            // give up after maxRestarts
        else
            restartCount <- restartCount + 1
            let backoffMs = min (1000 * (1 <<< restartCount)) 30000
            Thread.Sleep backoffMs
            stopAndShutdown(); start()
    and start () =
        let r = topology |> RuntimeTopology.ofTopology startLog restart
        restartCount <- 0  // reset on successful start
        // activate...
    and stopAndShutdown () = ...
    start()
    stopAndShutdown  // returns the shutdown function

```

The `restart` function is passed as the `onException` handler to every Disruptor channel. When any component throws, the topology is torn down and rebuilt with increasing delay between attempts.

sequenceDiagram
    participant Caller
    participant runWith
    participant RT as RuntimeTopology
    participant Component

    Caller->>runWith: topology
    runWith->>RT: ofTopology(startLog, restart)
    RT-->>runWith: rtt
    runWith->>RT: activate(rtt)
    Note over runWith: Running (restartCount = 0)

    Component->>runWith: Unhandled exception
    Note over runWith: restartCount++ (1/5)
    Note over runWith: Backoff: 2000ms
    runWith->>RT: stop(timeout, rtt)
    runWith->>RT: shutdown(rtt)
    runWith->>RT: ofTopology(startLog, restart)
    RT-->>runWith: new rtt
    runWith->>RT: activate(new rtt)
    Note over runWith: Running (restartCount = 0)

    Component->>runWith: Another exception
    Note over runWith: restartCount++ (1/5)
    Note over runWith: Backoff: 2000ms
    runWith->>RT: stop + shutdown + restart

    Note over runWith: ... up to 5 restarts max ...

    Caller->>runWith: invoke shutdown fn
    runWith->>RT: stop + shutdown

Key details:

* `Monitor.TryEnter` prevents concurrent restart attempts — if a restart is already in progress, additional exceptions are logged as warnings and ignored

* Exponential backoff: `min(1000 * 2^attempt, 30000)` milliseconds between attempts

* `restartCount` resets to 0 after each successful start

* After `maxRestarts` (5) consecutive failures, the topology stays down

* `runNoRestart` uses `Environment.Exit(1)` instead, delegating restart to the process supervisor

## Diagnostics

Self-hosted topologies emit diagnostics via standard .NET APIs:

### OpenTelemetry integration

The `FsShelter.Hosting` namespace exposes an `ActivitySource` and a `Meter` for distributed tracing and metrics collection.

**Tracing** (`ActivitySource: "FsShelter.Hosting"`):

Activity | Kind | Tags | Description
--- | --- | --- | ---
`channel.process` | Internal | — | Wraps each Disruptor event; propagates parent context via `Envelope.ParentContext`
`spout.emit` | Producer | `component.id`, `task.id` | Wraps spout dispatch (Next, Ack/Nack handling)
`bolt.process` | Consumer | `component.id`, `task.id` | Wraps bolt dispatch (Tuple processing)


**Metrics** (`Meter: "FsShelter.Hosting"`):

Instrument | Type | Unit | Description
--- | --- | --- | ---
`fsshelter.tuples.emitted` | Counter<int64> | — | Tuples emitted by spouts
`fsshelter.tuples.acked` | Counter<int64> | — | Tuples acked (XOR tree completed)
`fsshelter.tuples.nacked` | Counter<int64> | — | Tuples nacked (explicit failure)
`fsshelter.tuples.expired` | Counter<int64> | — | Tuples expired by acker bucket rotation
`fsshelter.task.processing_time` | Histogram<float> | ms | Per-message processing time (tagged by `component.id`, `task.id`)
`fsshelter.spout.pending` | UpDownCounter<int> | — | Current number of unacked spout tuples


### Debug logging

* `TOPOLOGY_DEBUG` flag can be set on individual components to selectively trace the traffic.

* Setting the debug flag globally will also trace the system and acker components.

* Setting the flag will also trace the time spent in the body of your function.

## Configuration

### Topology-level options

Option | Default | Effect
--- | --- | ---
`TOPOLOGY_MAX_SPOUT_PENDING` | 128 | Maximum unacked tuples per spout task before backpressure kicks in
`TOPOLOGY_ACKER_TASKS` | 4 | Number of acker task instances (logical units with independent state)
`TOPOLOGY_ACKER_EXECUTORS` | 2 | Number of acker executor threads (tasks are distributed round-robin)
`TOPOLOGY_EXECUTOR_RECEIVE_BUFFER_SIZE` | 256 | Base Disruptor ring buffer size (scaled by tasks per executor)
`TOPOLOGY_MESSAGE_TIMEOUT_SECS` | 30 | Timeout for tuple completion; controls acker bucket rotation interval and drain window during shutdown
`TOPOLOGY_SLEEP_SPOUT_WAIT_STRATEGY_TIME_MS` | 100 | Spout executor timeout: how often the spout wakes to poll for new tuples when idle
`TOPOLOGY_DEBUG` | false | Enable trace-level logging with timing
`TOPOLOGY_TICK_TUPLE_FREQ_SECS` | (none) | Per-bolt tick tuple interval in seconds


### Component-level DSL

Function | Applies to | Default | Effect
--- | --- | --- | ---
`withParallelism n` | Spout, Bolt | 1 | Number of tasks (independent processing units with their own state)
`withExecutors n` | Spout, Bolt | = Parallelism | Number of executor threads; tasks are distributed round-robin across executors
`withConf [...]` | Spout, Bolt, Topology | — | Configuration overrides (component-level merged with topology-level)
`withActivation tuple` | Bolt | None | Send this tuple to the bolt on activation
`withDeactivation tuple` | Bolt | None | Send this tuple to the bolt on deactivation


Example:

```fsharp
// 4 bolt tasks served by 2 executor threads (2 tasks per thread)
let counter = countWords
              |> Bolt.run (fun _ _ t emit -> (t, emit))
              |> withParallelism 4
              |> withExecutors 2

```

### Parameter relationships

The configuration parameters interact in important ways. Understanding these relationships helps with tuning.

**Parallelism vs Executors:**

* `Parallelism` sets the number of **tasks** — independent logical units with their own state, pending counters, and handlers.

* `Executors` sets the number of **threads** serving those tasks. When `Executors < Parallelism`, multiple tasks share a Disruptor thread and their messages are dispatched by `TaskId`.

* The runtime enforces `Executors = min(Executors, Parallelism)` — you can't have more threads than tasks.

* Default is 1:1 (each task gets its own thread).

**Ring buffer sizing:**

The ring buffer per executor is: `TOPOLOGY_EXECUTOR_RECEIVE_BUFFER_SIZE * tasks_per_executor`.

When sharing executors (e.g., 4 tasks on 2 executors), each executor's buffer is `256 * 2 = 512` slots. This automatic scaling prevents starvation when multiple tasks share a thread.

The ring buffer must also be large enough to hold `TOPOLOGY_MAX_SPOUT_PENDING` in-flight tuples. If `maxPending` exceeds the ring buffer capacity, spout publishes can block and deadlock the drain on shutdown.

**Spout wait strategy:**

`TOPOLOGY_SLEEP_SPOUT_WAIT_STRATEGY_TIME_MS` controls how often an idle spout wakes to call `next`. Lower values mean faster response to available work but higher CPU utilisation when idle. Values below ~10ms cause measurable overhead from frequent wake-ups with no throughput benefit.

**Message timeout and shutdown:**

`TOPOLOGY_MESSAGE_TIMEOUT_SECS` serves three roles:

0 **Acker bucket rotation** — tuples older than this are expired

1 **Shutdown drain window** — the system sleeps this long between stopping spouts and stopping bolts

2 **Restart backoff ceiling** — indirectly affects how long restarts take

Lower values speed up shutdown and failure detection, but risk false-expiring tuples that are merely slow to process.

**Acker capacity:**

`TOPOLOGY_ACKER_TASKS` controls the number of independent acker state machines.
`TOPOLOGY_ACKER_EXECUTORS` controls the threads serving them.
Acker tasks are stateless relative to each other (each tracks a disjoint set of tuples via hash partitioning), so more tasks reduce lock contention. More executors reduce thread contention when acker throughput is the bottleneck.

### Tuning summary

Goal | Adjust
--- | ---
Increase throughput (CPU-bound bolts) | Increase bolt `withParallelism`
Reduce thread count | Set `withExecutors` lower than `withParallelism` (share threads)
Handle slow consumers | Increase `TOPOLOGY_MAX_SPOUT_PENDING` and `TOPOLOGY_EXECUTOR_RECEIVE_BUFFER_SIZE` proportionally
Faster failure detection | Lower `TOPOLOGY_MESSAGE_TIMEOUT_SECS` (but ensure it exceeds your slowest bolt processing time)
Reduce idle CPU | Increase `TOPOLOGY_SLEEP_SPOUT_WAIT_STRATEGY_TIME_MS` (default 100ms is usually fine)
Handle high acker load | Increase `TOPOLOGY_ACKER_TASKS` and/or `TOPOLOGY_ACKER_EXECUTORS`

