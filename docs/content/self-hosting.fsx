(**
Self-hosting
-------
We call running a FsShelter topology entirely inside a .NET process "self-hosting".
The feature simulates Storm cluster and can be used for testing or production deployments where scale-out capability of Storm is not required.
The 2.0 release of FsShelter reimplements this feature using the same technology Storm does - Disruptor queues, greatly improving the performance and further reducing overhead.

Limitations:

- only FsShelter components can be self-hosted, no shell or java;
- only does in-proc message passing - can't scale out.

Benefits:

- Self-hosted F# components perform on par with native JVM components under Storm, while taking as little as 30MB and ~20 threads.
- no Storm, zookeeper or even JRE are required, while delivering the same processing guarantees including tuple timeouts.

Start small then scale out to run on a cluster, with no changes to your code! Use the same building blocks that run in the cloud... anywhere! 

For details refer to the Guaranteed sample, which demonstrates how to run the topology either way.

See also:

- [Message flow](message-flow.html) — End-to-end processing scenarios with diagrams: reliable/unreliable delivery, backpressure, timeouts, failures, and restarts.
- [Acker algorithm](acker-algorithm.html) — XOR-tree tuple tracking: how anchoring, acking, and nacking work to provide guaranteed message processing.
- [Routing](routing.html) — Tuple routing between components: grouping strategies (Shuffle, Fields, All, Direct) and stream-based fan-out.


RuntimeTopology structure
--------------------
When a topology is hosted, it is compiled into a `RuntimeTopology<'t>` record:

    type RuntimeTopology<'t> =
        { systemTask : TaskId * Channel<TaskMsg<'t, unit>>
          ackerTasks : Map<TaskId, Channel<TaskMsg<'t, AckerMsg>>>
          spoutTasks : Map<TaskId, ComponentId * Channel<TaskMsg<'t, InCommand<'t>>>>
          boltTasks  : Map<TaskId, ComponentId * Channel<TaskMsg<'t, InCommand<'t>>>> }

Each task has a unique `TaskId` (sequential integer) and a `Channel<'msg>` which is a `(Send<'msg> * Shutdown)` pair — a publish function and a halt function for the underlying Disruptor.

### Tasks and executors

A **task** is a logical processing unit with independent state. An **executor** is a Disruptor thread that serves one or more tasks. Multiple tasks sharing an executor have their messages dispatched by `TaskId` embedded in each `Envelope`.

| Task | Count | Executors | Channel Type | Purpose |
|------|-------|-----------|-------------|--------|
| **System** | 1 | 1 | `TaskMsg<'t, unit>` | Manages timer callbacks for tick tuples |
| **Acker** | `TOPOLOGY_ACKER_TASKS` (default 4) | `TOPOLOGY_ACKER_EXECUTORS` (default 2) | `TaskMsg<'t, AckerMsg>` | XOR-tree tracking for guaranteed delivery |
| **Spout** | Per-component `Parallelism` | `Executors` (default = `Parallelism`) | `TaskMsg<'t, InCommand<'t>>` | Message generation with backpressure |
| **Bolt** | Per-component `Parallelism` | `Executors` (default = `Parallelism`) | `TaskMsg<'t, InCommand<'t>>` | Message processing and downstream emission |

Tasks are distributed across executors round-robin via `groupIntoExecutors`. Each task within an executor maintains its own independent handler, dispatcher, and state (e.g. pending counter for spouts, acker buckets for ackers).

### Message wrapper

All task messages are wrapped in `TaskMsg<'t, 'msg>` (a struct DU to avoid allocation on the hot path):

    [<Struct>]
    type TaskMsg<'t, 'msg> =
        | Start of rtt:RuntimeTopology<'t>   // Receive topology reference
        | Stop                                // Graceful shutdown
        | Tick                                // Timer-driven heartbeat
        | Other of msg:'msg                   // Domain-specific message


Channel infrastructure
--------------------
Each executor runs on a Disruptor ring buffer. Messages carry a `TaskId` in their `Envelope`, and the consumer dispatches to the correct per-task handler via an array lookup table.

### `Channel.startExecutor` (ackers, bolts)
Multiple tasks share a single Disruptor ring buffer. Each published message includes a `TaskId` via `publishWithTaskId`. The consumer looks up the handler by `envelope.TaskId` and invokes it.

### `Channel.startExecutorWithTimeout` (spouts)
Same as `startExecutor` but with `TimeoutBlockingWaitStrategy` (100ms timeout). When no message arrives within the timeout window, a combined `onTimeout` function fires, calling each spout task's `onTimeout` to issue additional `Next` requests.

    // Executor channel — multiple tasks sharing one ring buffer
    let startExecutor ringSize onException (tasks: (TaskId * handler) array) =
        // Builds taskIndex dictionary for O(1) dispatch
        // Returns: (send: TaskId -> Send<'msg>), halt

    // Executor channel with timeout — for spouts
    let startExecutorWithTimeout ringSize onException (tasks: (TaskId * handler) array) onTimeout =
        // Same dispatch + timeout fires onTimeout()
        // Returns: (send: TaskId -> Send<'msg>), halt

The system task uses the basic `Channel.start` (single handler, no executor dispatch).

The ring buffer size per executor scales with the number of tasks it serves: `TOPOLOGY_EXECUTOR_RECEIVE_BUFFER_SIZE * tasksPerExecutor` (base default 256).

Spouts and bolts send directly to downstream Disruptor ring buffers via typed `send` functions — no intermediate buffering or boxing.


Topology construction
--------------------
`RuntimeTopology.ofTopology` builds the runtime from a topology definition:

```mermaid
sequenceDiagram
    participant Caller
    participant ofTopology
    participant Channel
    participant System
    participant Ackers
    participant Bolts
    participant Spouts

    Caller->>ofTopology: topology definition
    Note over ofTopology: Assign sequential TaskIds

    ofTopology->>Channel: start(mkSystem handler)
    Channel-->>System: Disruptor ring buffer

    loop For each acker executor (1..ACKER_EXECUTORS)
        ofTopology->>Channel: startExecutor(mkAcker handlers)
        Channel-->>Ackers: Disruptor ring buffer
    end

    loop For each bolt executor (per parallelism/executors)
        ofTopology->>Channel: startExecutor(mkBolt handlers)
        Channel-->>Bolts: Disruptor ring buffer
    end

    loop For each spout executor (per parallelism/executors)
        ofTopology->>Channel: startExecutorWithTimeout(mkSpout handlers)
        Channel-->>Spouts: Disruptor ring buffer (with timeout)
    end

    ofTopology-->>Caller: RuntimeTopology record
```


Task lifecycle
--------------------
After construction, the topology goes through activation, processing, and shutdown phases:

```mermaid
stateDiagram-v2
    [*] --> Created: ofTopology
    Created --> Activated: RuntimeTopology.activate
    state Activated {
        [*] --> AckersStarted: Start msg to ackers
        AckersStarted --> BoltsStarted: Start msg to bolts
        BoltsStarted --> SpoutsStarted: Start msg to spouts
        SpoutsStarted --> TimersStarted: Start msg to system
    }
    Activated --> Processing: All components active
    Processing --> Stopping: RuntimeTopology.stop
    state Stopping {
        [*] --> SystemStopped: Stop to system
        SystemStopped --> SpoutsStopped: Stop to spouts
        SpoutsStopped --> Draining: Sleep(timeout) for in-flight tuples
        Draining --> BoltsStopped: Stop to bolts
        BoltsStopped --> AckersStopped: Stop to ackers
    }
    Stopping --> Shutdown: RuntimeTopology.shutdown
    Shutdown --> [*]
```

### Activation order

`RuntimeTopology.activate` sends `Start rtt` to each task, giving it a reference to the full runtime so it can route messages to other tasks:

1. **Ackers first** — must be ready before spouts emit tracked tuples
2. **Bolts second** — must be ready before spouts emit routed tuples
3. **Spouts third** — begin generating messages
4. **System last** — starts timer callbacks that drive tick tuples

### Shutdown order

`RuntimeTopology.stop` sends `Stop` with a drain window between spouts and bolts:

1. **System** — stop timers
2. **Spouts** — stop generating messages (deactivate)
3. **Sleep(timeout)** — allow in-flight tuples to drain through the bolt DAG
4. **Bolts** — stop processing (deactivate)
5. **Ackers** — stop tracking (last, so late acks can still be processed)

Then `RuntimeTopology.shutdown` halts all Disruptor instances. When multiple tasks share an executor, the shared Disruptor is halted only once (tracked via a `HashSet<Shutdown>`).


Entry points
--------------------

### `Hosting.run`

Simplest entry point. No logging, with auto-restart (delegates to `runWith`). Returns a shutdown function:

    let run topology = runWith (fun _ _ -> ignore) topology


### `Hosting.runWith` (auto-restart with backoff)

Production entry point. Accepts a logging factory and auto-restarts on unhandled exceptions with exponential backoff and a maximum restart count:

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

The `restart` function is passed as the `onException` handler to every Disruptor channel. When any component throws, the topology is torn down and rebuilt with increasing delay between attempts.

```mermaid
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
```

Key details:

- `Monitor.TryEnter` prevents concurrent restart attempts — if a restart is already in progress, additional exceptions are logged as warnings and ignored
- Exponential backoff: `min(1000 * 2^attempt, 30000)` milliseconds between attempts
- `restartCount` resets to 0 after each successful start
- After `maxRestarts` (5) consecutive failures, the topology stays down
- `runNoRestart` uses `Environment.Exit(1)` instead, delegating restart to the process supervisor


Component wrappers
--------------------

### Spout wrapper (`mkSpout`)

Wraps a `Runnable<'t>` into a `(handler, onTimeout)` pair:

- `handler` processes `TaskMsg` values:
  - On `Start rtt`: creates output function (routing + acker tracking), calls `Activate`, issues initial `Next` requests up to `maxPending`
  - On `Other(Ack _)` / `Other(Nack _)`: decrements `pending`, issues `Next`, forwards to dispatcher
  - On `Stop`: sends `Deactivate`, clears dispatcher
- `onTimeout` is called when the Disruptor 100ms timeout fires: issues another `Next` if under the pending limit

### Bolt wrapper (`mkBolt`)

Wraps a `Runnable<'t>` into a handler function:

- On `Start rtt`: creates output function (routing + anchoring + ack/nack), calls `Activate`
- On `Other(Tuple ...)`: forwards to dispatcher for processing
- On `Stop`: sends `Deactivate`, clears dispatcher

### System task (`mkSystem`)

Manages `System.Threading.Timer` instances:

- **Spout/Acker tick**: every 30 seconds, sends `Tick` to all spouts and ackers
- **Bolt tick tuples**: for bolts with `TOPOLOGY_TICK_TUPLE_FREQ_SECS` configured, sends a `__tick` tuple at the configured interval
- On `Stop`: disposes all timers


Component interaction overview
--------------------

```mermaid
graph LR
    subgraph Timers
        T30["30s Timer"]
        TT["Tick Timer"]
    end

    subgraph Spouts
        S1["Spout 1"]
        S2["Spout N"]
    end

    subgraph Bolts
        B1["Bolt 1"]
        B2["Bolt M"]
    end

    subgraph Ackers
        A1["Acker 1"]
        A2["Acker K"]
    end

    T30 -- "Tick" --> S1 & S2
    T30 -- "Tick" --> A1 & A2
    TT -- "__tick tuple" --> B1 & B2

    S1 -- "Tuple" --> B1
    S2 -- "Tuple" --> B2
    B1 -- "Tuple" --> B2

    S1 -. "Track" .-> A1
    S2 -. "Track" .-> A2
    B1 -. "Ok / Fail" .-> A1
    B2 -. "Ok / Fail" .-> A2
    A1 -. "Ack / Nack" .-> S1
    A2 -. "Ack / Nack" .-> S2
```

**Solid arrows** = data tuples. **Dashed arrows** = acker protocol messages.


Diagnostics
--------
Self-hosted topologies emit diagnostics via standard .NET APIs:

### OpenTelemetry integration

The `FsShelter.Hosting` namespace exposes an `ActivitySource` and a `Meter` for distributed tracing and metrics collection.

**Tracing** (`ActivitySource: "FsShelter.Hosting"`):

| Activity | Kind | Tags | Description |
|----------|------|------|-------------|
| `channel.process` | Internal | — | Wraps each Disruptor event; propagates parent context via `Envelope.ParentContext` |
| `spout.emit` | Producer | `component.id`, `task.id` | Wraps spout dispatch (Next, Ack/Nack handling) |
| `bolt.process` | Consumer | `component.id`, `task.id` | Wraps bolt dispatch (Tuple processing) |

**Metrics** (`Meter: "FsShelter.Hosting"`):

| Instrument | Type | Unit | Description |
|-----------|------|------|-------------|
| `fsshelter.tuples.emitted` | Counter<int64> | — | Tuples emitted by spouts |
| `fsshelter.tuples.acked` | Counter<int64> | — | Tuples acked (XOR tree completed) |
| `fsshelter.tuples.nacked` | Counter<int64> | — | Tuples nacked (explicit failure) |
| `fsshelter.tuples.expired` | Counter<int64> | — | Tuples expired by acker bucket rotation |
| `fsshelter.task.processing_time` | Histogram<float> | ms | Per-message processing time (tagged by `component.id`, `task.id`) |
| `fsshelter.spout.pending` | UpDownCounter<int> | — | Current number of unacked spout tuples |

### Debug logging

- `TOPOLOGY_DEBUG` flag can be set on individual components to selectively trace the traffic.
- Setting the debug flag globally will also trace the system and acker components.
- Setting the flag will also trace the time spent in the body of your function.


Configuration
--------

### Topology-level options

| Option | Default | Effect |
|--------|---------|--------|
| `TOPOLOGY_MAX_SPOUT_PENDING` | 128 | Maximum unacked tuples per spout task before backpressure kicks in |
| `TOPOLOGY_ACKER_TASKS` | 4 | Number of acker task instances (logical units with independent state) |
| `TOPOLOGY_ACKER_EXECUTORS` | 2 | Number of acker executor threads (tasks are distributed round-robin) |
| `TOPOLOGY_EXECUTOR_RECEIVE_BUFFER_SIZE` | 256 | Base Disruptor ring buffer size (scaled by tasks per executor) |
| `TOPOLOGY_MESSAGE_TIMEOUT_SECS` | 30 | Timeout for tuple completion; controls acker bucket rotation interval and drain window during shutdown |
| `TOPOLOGY_SLEEP_SPOUT_WAIT_STRATEGY_TIME_MS` | 100 | Spout executor timeout: how often the spout wakes to poll for new tuples when idle |
| `TOPOLOGY_DEBUG` | false | Enable trace-level logging with timing |
| `TOPOLOGY_TICK_TUPLE_FREQ_SECS` | (none) | Per-bolt tick tuple interval in seconds |

### Component-level DSL

| Function | Applies to | Default | Effect |
|----------|-----------|---------|--------|
| `withParallelism n` | Spout, Bolt | 1 | Number of tasks (independent processing units with their own state) |
| `withExecutors n` | Spout, Bolt | = Parallelism | Number of executor threads; tasks are distributed round-robin across executors |
| `withConf [...]` | Spout, Bolt, Topology | — | Configuration overrides (component-level merged with topology-level) |
| `withActivation tuple` | Bolt | None | Send this tuple to the bolt on activation |
| `withDeactivation tuple` | Bolt | None | Send this tuple to the bolt on deactivation |

Example:

    // 4 bolt tasks served by 2 executor threads (2 tasks per thread)
    let counter = countWords
                  |> Bolt.run (fun _ _ t emit -> (t, emit))
                  |> withParallelism 4
                  |> withExecutors 2

### Parameter relationships

The configuration parameters interact in important ways. Understanding these relationships helps with tuning.

**Parallelism vs Executors:**

- `Parallelism` sets the number of **tasks** — independent logical units with their own state, pending counters, and handlers.
- `Executors` sets the number of **threads** serving those tasks. When `Executors < Parallelism`, multiple tasks share a Disruptor thread and their messages are dispatched by `TaskId`.
- The runtime enforces `Executors = min(Executors, Parallelism)` — you can't have more threads than tasks.
- Default is 1:1 (each task gets its own thread).

**Ring buffer sizing:**

The ring buffer per executor is: `TOPOLOGY_EXECUTOR_RECEIVE_BUFFER_SIZE * tasks_per_executor`.

When sharing executors (e.g., 4 tasks on 2 executors), each executor's buffer is `256 * 2 = 512` slots. This automatic scaling prevents starvation when multiple tasks share a thread.

The ring buffer must also be large enough to hold `TOPOLOGY_MAX_SPOUT_PENDING` in-flight tuples. If `maxPending` exceeds the ring buffer capacity, spout publishes can block and deadlock the drain on shutdown.

**Spout wait strategy:**

`TOPOLOGY_SLEEP_SPOUT_WAIT_STRATEGY_TIME_MS` controls how often an idle spout wakes to call `next`. Lower values mean faster response to available work but higher CPU utilisation when idle. Values below ~10ms cause measurable overhead from frequent wake-ups with no throughput benefit.

**Message timeout and shutdown:**

`TOPOLOGY_MESSAGE_TIMEOUT_SECS` serves three roles:

1. **Acker bucket rotation** — tuples older than this are expired
2. **Shutdown drain window** — the system sleeps this long between stopping spouts and stopping bolts
3. **Restart backoff ceiling** — indirectly affects how long restarts take

Lower values speed up shutdown and failure detection, but risk false-expiring tuples that are merely slow to process.

**Acker capacity:**

`TOPOLOGY_ACKER_TASKS` controls the number of independent acker state machines.
`TOPOLOGY_ACKER_EXECUTORS` controls the threads serving them.
Acker tasks are stateless relative to each other (each tracks a disjoint set of tuples via hash partitioning), so more tasks reduce lock contention. More executors reduce thread contention when acker throughput is the bottleneck.

### Tuning summary

| Goal | Adjust |
|------|--------|
| Increase throughput (CPU-bound bolts) | Increase bolt `withParallelism` |
| Reduce thread count | Set `withExecutors` lower than `withParallelism` (share threads) |
| Handle slow consumers | Increase `TOPOLOGY_MAX_SPOUT_PENDING` and `TOPOLOGY_EXECUTOR_RECEIVE_BUFFER_SIZE` proportionally |
| Faster failure detection | Lower `TOPOLOGY_MESSAGE_TIMEOUT_SECS` (but ensure it exceeds your slowest bolt processing time) |
| Reduce idle CPU | Increase `TOPOLOGY_SLEEP_SPOUT_WAIT_STRATEGY_TIME_MS` (default 100ms is usually fine) |
| Handle high acker load | Increase `TOPOLOGY_ACKER_TASKS` and/or `TOPOLOGY_ACKER_EXECUTORS` |

*)
