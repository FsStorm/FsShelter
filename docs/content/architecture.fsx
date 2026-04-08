(**
Architecture
-------
This page describes the internal structure of the FsShelter self-hosting runtime — how topologies are compiled into tasks, how messages flow through Disruptor ring buffers, and how the lifecycle is managed. 

For a higher-level introduction, see [Core Concepts](concepts.html). For configuration and entry points, see [Running Topologies](self-hosting.html).


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
3. **Sleep(timeout)** — allow in-flight tuples to drain through the bolt DAG (`SUPERVISOR_WORKER_SHUTDOWN_SLEEP_SECS`, defaults to `TOPOLOGY_MESSAGE_TIMEOUT_SECS`)
4. **Bolts** — stop processing (deactivate)
5. **Ackers** — stop tracking (last, so late acks can still be processed)

Then `RuntimeTopology.shutdown` halts all Disruptor instances. When multiple tasks share an executor, the shared Disruptor is halted only once (tracked via a `HashSet<Shutdown>`).


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


See also
--------------------

- [Message flow](message-flow.html) — End-to-end processing scenarios with sequence diagrams
- [Acker algorithm](acker-algorithm.html) — XOR-tree tuple tracking for guaranteed delivery
- [Routing](routing.html) — Grouping strategies and stream-based fan-out

*)
