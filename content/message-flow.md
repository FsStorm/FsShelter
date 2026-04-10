## Message flow

End-to-end message processing scenarios in the FsShelter self-hosting runtime. Each scenario includes a processing graph showing the exact flow of messages between components.

## Protocol types

Messages flowing between components use Storm's multilang protocol types:

```fsharp
// Inbound messages to spouts/bolts
type InCommand<'t> =
    | Activate | Deactivate
    | Next                                          // Spout: request next tuple
    | Ack of TupleId | Nack of TupleId              // Spout: acker feedback
    | Tuple of 't * TupleId * ComponentId * StreamId * TaskId  // Bolt: incoming tuple
    | Heartbeat

// Outbound messages from spouts/bolts
type OutCommand<'t> =
    | Emit of 't * TupleId option * TupleId list * StreamId * TaskId option * bool option
    | Ok of TupleId | Fail of TupleId               // Bolt: ack/nack tuple
    | Log of string * LogLevel | Error of string * exn
    | Sync

```

Within the hosting runtime, these are wrapped in `TaskMsg`:

```fsharp
type TaskMsg<'t, 'msg> = Start of RuntimeTopology<'t> | Stop | Tick | Other of 'msg

```

## Dispatch functions

The dispatch layer (`Dispatch.fs`) converts protocol messages into component-specific behavior:

Function | Used By | Ack Behavior
--- | --- | ---
`reliableSpout` | Spouts with tuple IDs | Emits with `Some tupleId`; handles `Ack`/`Nack` callbacks
`unreliableSpout` | Fire-and-forget spouts | Emits with `None`; ignores `Ack`/`Nack`
`autoAckBolt` | Standard bolts | Sends `Ok id` after successful processing; `Fail id` on exception
`autoNackBolt` | Terminating bolts | Always sends `Fail id` (used for sinks that don't continue the DAG)


## Scenario A: Unreliable spout pipeline

Fire-and-forget message flow with no tuple tracking. The simplest scenario.

sequenceDiagram
    participant SP as Spout
    participant RT as Router
    participant B1 as Bolt

    Note over SP: Activated, pending < maxPending
    SP->>SP: Next
    SP->>SP: unreliableSpout.next() → Some tuple
    SP->>RT: Emit(tuple, None, [], stream, None)
    RT->>B1: Tuple(tuple, id, comp, stream, taskId)
    B1->>B1: consume(args, tuple, emit)
    B1->>B1: Ok id (auto-ack, sent to output)
    Note over B1: Ok goes nowhere meaningful

Key points:

* No `Track` message to acker (no source tuple ID)

* `Ok`/`Fail` from bolts has no effect since tuples aren't anchored

* Spout's pending counter still increments on emit and times out naturally

* Backpressure still applies via `maxPending`

## Scenario B: Reliable tuple flow (guaranteed delivery)

Full round-trip with acker tracking. This is the core guaranteed delivery scenario.

sequenceDiagram
    participant SP as Spout
    participant TT as TupleTree
    participant ACK as Acker
    participant RT as Router
    participant B1 as Bolt 1
    participant B2 as Bolt 2

    Note over SP: Activated, issues Next

    SP->>SP: reliableSpout.next()
    SP->>TT: track(taskId, srcTupleId)
    TT->>TT: Generate anchorId (random int64)
    TT->>ACK: Track(spoutTaskId, srcTupleId, anchorId)
    Note over ACK: buckets[current][anchorId] = Pending(srcTupleId, taskId, 0, 0)

    TT-->>SP: mkIds function
    SP->>RT: Emit(tuple, srcTupleId, [], stream)
    RT->>TT: mkIds()
    TT->>ACK: Anchor(anchorId, tupleId1)
    Note over ACK: XOR: v = 0 ^^^ tupleId1
    RT->>B1: Tuple(tuple, "anchorId:tupleId1", ...)
    SP->>SP: pending++

    Note over B1: Processing tuple...
    B1->>TT: anchor(["anchorId:tupleId1"])
    B1->>B1: consume(args, tuple, emit)
    B1->>RT: Emit(result, None, ["anchorId:tupleId1"], stream2)
    RT->>TT: mkIds()
    TT->>ACK: Anchor(anchorId, tupleId2)
    RT->>B2: Tuple(result, "anchorId:tupleId2", ...)

    B1->>ACK: Ok(anchorId, tupleId1)
    Note over ACK: XOR: v = v ^^^ tupleId1

    Note over B2: Processing tuple...
    B2->>ACK: Ok(anchorId, tupleId2)
    Note over ACK: XOR: v = v ^^^ tupleId2 = 0

    ACK->>SP: Ack(srcTupleId)
    SP->>SP: pending--
    SP->>SP: reliableSpout.ack(srcTupleId)
    Note over SP: Issues Next (pending < maxPending)

**XOR accumulation:**

0 Track: `v = 0`

1 Anchor tupleId1: `v = 0 ⊕ tupleId1`

2 Anchor tupleId2: `v = 0 ⊕ tupleId1 ⊕ tupleId2`

3 Ok tupleId1: `v = 0 ⊕ tupleId2`

4 Ok tupleId2: `v = 0` → **Complete!**

For a detailed walkthrough of the XOR-tree algorithm, see [Acker algorithm](acker-algorithm.html).

## Scenario C: Spout backpressure

The spout self-throttles based on unacknowledged tuple count.

sequenceDiagram
    participant SP as Spout
    participant ACK as Acker
    participant B as Bolt

    Note over SP: maxPending = 3, pending = 0

    SP->>SP: Next, emit tuple 1
    Note over SP: pending = 1
    SP->>SP: Next, emit tuple 2
    Note over SP: pending = 2
    SP->>SP: Next, emit tuple 3
    Note over SP: pending = 3

    rect rgb(255, 230, 230)
        Note over SP: pending >= maxPending STOP issuing Next
        SP->>SP: (timeout, check pending, skip)
        SP->>SP: (timeout, check pending, skip)
    end

    B->>ACK: Ok(tupleId)
    ACK->>SP: Ack(srcTupleId)
    Note over SP: pending = 2

    rect rgb(230, 255, 230)
        Note over SP: pending < maxPending RESUME issuing Next
        SP->>SP: Next, emit tuple 4
        Note over SP: pending = 3
    end

Implementation detail from `mkSpout`:

```fsharp
let inline dispatchNext () =
    if pending < maxPending then
        dispatcher InCommand.Next

// On timeout (ValueNone from Disruptor):
| _ -> issueNext()

// On Ack/Nack:
| Other msg ->
    match msg with
    | Ack _ | Nack _ ->
        Interlocked.Decrement &pending |> ignore
        issueNext()
        dispatcher msg

```

The spout channel uses `TimeoutBlockingWaitStrategy(100ms)`. When the timeout fires with no message, the spout calls `issueNext()`, which checks the pending count before dispatching `Next`.

## Scenario D: Tuple timeout and nack

When a tuple isn't fully acked before its acker bucket rotates out, the acker expires it and nacks the source spout. The acker uses `numBuckets = 3` with a 30-second tick, so entries survive between 30 and 90 seconds.

sequenceDiagram
    participant SP as Spout
    participant ACK as Acker
    participant B as Bolt
    participant SYS as System Timer

    SP->>ACK: Track(spoutTaskId, srcTupleId, anchorId)
    Note over ACK: buckets[current][anchorId] = Pending(srcTupleId, taskId, 0, 0)

    SP->>B: Tuple(tuple, "anchorId:tupleId1", ...)
    B->>ACK: Anchor(anchorId, tupleId1)
    Note over ACK: XOR: v = tupleId1

    Note over B: Bolt is slow or stuck... Never sends Ok

    SYS->>ACK: Tick
    Note over ACK: rotate(): oldest = (current+1) % 3

    rect rgb(255, 230, 230)
        Note over ACK: Scan oldest bucket, anchorId is Pending
        ACK->>SP: Nack(srcTupleId)
        Note over ACK: Clear oldest bucket, advance currentBucket
    end

    SP->>SP: pending--
    SP->>SP: reliableSpout.nack(srcTupleId)
    Note over SP: Re-enqueue or discard (application-specific)

The system timer sends `Tick` to ackers every 30 seconds, triggering bucket rotation via `rotate()`.

## Scenario E: Bolt failure

When a bolt throws an exception during tuple processing, the auto-ack dispatch sends `Fail` instead of `Ok`:

sequenceDiagram
    participant SP as Spout
    participant ACK as Acker
    participant B as Bolt

    SP->>ACK: Track(spoutTaskId, srcTupleId, anchorId)
    SP->>B: Tuple(tuple, "anchorId:tupleId1", ...)
    B->>ACK: Anchor(anchorId, tupleId1)

    rect rgb(255, 230, 230)
        Note over B: consume() throws exception!
        B->>B: Error(message, ex) to log
        B->>ACK: Fail(anchorId, tupleId1)
    end

    Note over ACK: Fail path, immediate nack
    ACK->>ACK: Lookup inFlight[anchorId]
    ACK->>SP: Nack(srcTupleId)
    Note over ACK: Remove anchorId from inFlight

    SP->>SP: pending--
    SP->>SP: reliableSpout.nack(srcTupleId)
    Note over SP: Re-enqueue for retry

The `autoAckBolt` dispatch:

```fsharp
| Tuple(tuple, id, src, stream, task) ->
    let emit t = Emit(t, None, getAnchors (src,stream) id, getStream t, None, None) |> out
    try
        consume (args tuple emit)
        Ok id           // success → ack
    with ex ->
        Fail id |> out  // failure → nack
        Error(sprintf "autoAckBolt was unable to handle: ", ex)
    |> out

```

## Scenario F: Topology restart on unhandled exception

When `Hosting.runWith` is used, unhandled exceptions in any Disruptor channel trigger a full topology restart with exponential backoff:

sequenceDiagram
    participant EH as Exception Handler
    participant RW as runWith
    participant RT1 as RuntimeTopology (old)
    participant RT2 as RuntimeTopology (new)

    Note over RT1: Component throws unhandled exception
    RT1->>EH: exn
    EH->>RW: restart(exn)

    rect rgb(255, 245, 230)
        Note over RW: Monitor.TryEnter(sync)
        RW->>RW: Log error
        Note over RW: restartCount++ (1/5)
        Note over RW: Backoff: min(1000 * 2^1, 30000) = 2000ms
        RW->>RW: Thread.Sleep(2000)
        RW->>RT1: stop(timeout, rtt)
        RW->>RT1: shutdown(rtt)
        Note over RT1: All Disruptors halted

        RW->>RT2: ofTopology(startLog, restart)
        Note over RT2: Fresh channels created
        RW->>RT2: activate(rtt)
        Note over RW: restartCount = 0 (reset on success)
        Note over RT2: All components restarted
    end

    Note over RW: Monitor.Exit(sync)

Key details:

* `Monitor.TryEnter` prevents concurrent restart attempts — if a restart is already in progress, additional exceptions are logged as warnings and ignored

* Exponential backoff: `min(1000 * 2^attempt, 30000)` milliseconds between attempts

* `restartCount` resets to 0 after each successful start

* After `maxRestarts` (5) consecutive failures, the topology stays down

* The exception handler is passed to `Disruptor.SetDefaultExceptionHandler`, catching exceptions from `IEventHandler.OnEvent`

* If restart itself throws, the error is logged but the topology remains down

* `runNoRestart` uses `Environment.Exit(1)` instead, delegating restart to the process supervisor

## Message flow summary

graph TB
    subgraph Happy Path
        direction LR
        A1[Next] --> A2[Emit + Track]
        A2 --> A3[Route to Bolt]
        A3 --> A4[Consume + Anchor]
        A4 --> A5[Ok to Acker]
        A5 --> A6["XOR == 0, Ack"]
    end

    subgraph Failure Paths
        direction LR
        F1[Bolt Exception] --> F2[Fail to Acker]
        F2 --> F3[Nack to Spout]
        T1[Timeout Expired] --> T2[Nack to Spout]
        C1["Acker Capacity Exceeded"] --> C2[Nack to Spout]
    end

    subgraph Backpressure
        direction LR
        BP1["pending >= max"] --> BP2[Stop Next]
        BP2 --> BP3["Ack/Nack arrives"]
        BP3 --> BP4[Resume Next]
    end
