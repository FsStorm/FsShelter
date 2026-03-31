(**
Acker algorithm
-------
The acker implements Storm's XOR-tree algorithm for tracking tuple completion across a DAG of bolts. It provides guaranteed message processing: every tuple emitted by a spout will eventually be either fully acknowledged (all downstream bolts completed) or nacked (timeout or failure).

Core idea
--------------------
Each tuple tree is identified by an `anchorId` (random `int64`). The acker maintains a single XOR accumulator per anchor. As tuples flow through bolts, each tuple ID is XOR'd into the accumulator twice — once when anchored (created) and once when completed (Ok). Since `x ⊕ x = 0`, when all tuples complete, the accumulator returns to zero.


Data structures
--------------------

### AckerMsg

Messages sent to acker tasks:

    type AckerMsg =
        | Track of taskId:TaskId * tupleId:TupleId * anchorId:int64
        | Anchor of anchoredId:(int64 * int64)    // (anchorId, tupleId)
        | Ok of okId:(int64 * int64)              // (anchorId, tupleId)
        | Fail of failId:(int64 * int64)          // (anchorId, tupleId)

### TreeState

Internal acker state per anchor:

    type TreeState =
        | Pending of TupleId * TaskId * int64 * int64
        //           srcId     srcTask  xorVal  (unused, 0L)
        | Complete of srcId:TupleId * src:TaskId
        | Done

The fourth field of `Pending` is unused (always `0L`) — timeout expiry is handled by bucket rotation rather than per-entry timestamps.

### AnchoredTupleId

Tuple IDs flowing through bolts are encoded as `"anchorId:tupleId"` strings:

    let (|AnchoredTuple|_|) (tupleId:string) =
        match tupleId.Split ':' with
        | [|anchor;tid|] -> Some (int64 anchor, int64 tid)
        | _ -> None


State machine
--------------------

```mermaid
stateDiagram-v2
    [*] --> Pending: Track(taskId, srcTupleId, anchorId)

    Pending --> Pending: Anchor(anchorId, tupleId) v = v XOR tupleId
    Pending --> Pending: Ok(anchorId, tupleId) v != 0

    Pending --> Complete: Ok(anchorId, tupleId) v = 0
    Pending --> Nacked: Fail(anchorId, _)
    Pending --> Nacked: Tick (bucket rotation expires)

    Complete --> [*]: Ack sent to spout, entry removed
    Nacked --> [*]: Nack sent to spout, entry removed
```


TupleTree module
--------------------
The `TupleTree` module provides three functions that produce the acker protocol messages at the spout and bolt layers.

### Acker assignment

Each anchor is assigned to an acker instance by hashing:

    let inline ackerOfAnchor (ackers: _ array) (anchorId: int64) =
        let i = abs (anchorId % (int64 ackers.Length))
        ackers.[int i]

This ensures all messages for a given anchor always go to the same acker instance.

### `TupleTree.track` (spout side)

Called when a spout emits a tuple. Generates a fresh `anchorId` and sends `Track` to the acker:

    let track nextId ackers taskId _ sourceTupleId =
        let anchorId = nextId()
        match sourceTupleId with
        | Some sid -> Track(taskId, sid, anchorId) |> toAcker
        | _ -> ()
        fun () ->
            let tupleId = nextId()
            Anchor(anchorId, tupleId) |> toAcker
            [sprintf "%d:%d" anchorId tupleId]

Returns a function `mkIds: unit -> string list` that, when called by the router, generates a new `tupleId`, sends `Anchor` to the acker, and returns `["anchorId:tupleId"]` for downstream bolts.

```mermaid
sequenceDiagram
    participant SP as Spout
    participant TT as TupleTree.track
    participant ACK as Acker

    SP->>TT: track(taskId, sourceTupleId)
    TT->>TT: anchorId = nextId()
    TT->>ACK: Track(taskId, sourceTupleId, anchorId)
    Note over ACK: buckets[current][anchorId] = Pending(srcId, taskId, 0, 0)
    TT-->>SP: mkIds function

    Note over SP: Router calls mkIds() when emitting
    SP->>TT: mkIds()
    TT->>TT: tupleId = nextId()
    TT->>ACK: Anchor(anchorId, tupleId)
    TT-->>SP: ["anchorId:tupleId"]
```

### `TupleTree.anchor` (bolt side)

Called when a bolt receives a tuple and prepares to emit downstream. Parses the incoming anchored IDs, and returns a function that generates new tuple IDs anchored to the same tree:

    let anchor nextId ackers anchors _ =
        let anchors =
            anchors
            |> List.choose ((|AnchoredTuple|_|) >> Option.map (fun (a, _) -> a, toAcker ackers a))
        fun () ->
            let tupleId = nextId()
            anchors |> List.iter (fun ((aid, enqueue), _) -> Anchor(aid, tupleId) |> enqueue)
            match anchors with
            | [] -> [string tupleId]
            | _ -> anchors |> List.map (fun ((aid, _), _) -> sprintf "%d:%d" aid tupleId)

```mermaid
sequenceDiagram
    participant B as Bolt
    participant TT as TupleTree.anchor
    participant ACK as Acker

    Note over B: Receives Tuple(t, "123:456", ...)
    B->>TT: anchor(["123:456"])
    TT->>TT: Parse anchors

    Note over B: Bolt emits downstream, router calls mkIds()
    B->>TT: mkIds()
    TT->>TT: tupleId = nextId()
    TT->>ACK: Anchor(123, tupleId)
    TT-->>B: ["123:tupleId"]
```

### `TupleTree.mkAck` (bolt completion)

Creates ack/nack functions that bolts use to signal tuple completion:

    let mkAck toResult ackers =
        function
        | AnchoredTuple (a, id) ->
            toResult (a, id) |> toAcker ackers a
        | _ -> ()

Called with `Ok` for successful processing, `Fail` for exceptions.

```mermaid
sequenceDiagram
    participant B as Bolt
    participant ACK as Acker

    Note over B: Processing succeeded
    B->>ACK: Ok(anchorId, tupleId)
    Note over ACK: v = v XOR tupleId

    Note over B: Processing failed
    B->>ACK: Fail(anchorId, tupleId)
    Note over ACK: Immediate nack to spout
```


XOR accumulation example
--------------------
Consider a simple DAG: `Spout → Bolt A → Bolt B`:

```mermaid
graph LR
    S[Spout] -->|"anchorId:100"| A[Bolt A]
    A -->|"anchorId:200"| B[Bolt B]
```

| Step | Event | XOR Value | State |
|------|-------|-----------|-------|
| 1 | `Track(spoutTask, srcId, anchorId)` | `v = 0` | Pending |
| 2 | `Anchor(anchorId, 100)` — spout route | `v = 100` | Pending |
| 3 | `Anchor(anchorId, 200)` — Bolt A emit | `v = 100 ⊕ 200` | Pending |
| 4 | `Ok(anchorId, 100)` — Bolt A done | `v = 200` | Pending |
| 5 | `Ok(anchorId, 200)` — Bolt B done | `v = 0` | **Complete** → Ack |

### Fan-out example

`Spout → Bolt A → (Bolt B, Bolt C)`:

| Step | Event | XOR Value |
|------|-------|-----------|
| 1 | Track | `v = 0` |
| 2 | Anchor(aid, 100) — spout→A | `v = 100` |
| 3 | Anchor(aid, 200) — A→B | `v = 100 ⊕ 200` |
| 4 | Anchor(aid, 300) — A→C | `v = 100 ⊕ 200 ⊕ 300` |
| 5 | Ok(aid, 100) — A done | `v = 200 ⊕ 300` |
| 6 | Ok(aid, 200) — B done | `v = 300` |
| 7 | Ok(aid, 300) — C done | `v = 0` → **Complete** |


Acker capacity and bucket rotation
--------------------
The acker uses a rotating array of `numBuckets = 3` dictionaries instead of a single `inFlight` dictionary. New entries go into the current bucket. On each `Tick` (every 30 seconds), the oldest bucket is expired — all remaining `Pending` entries are nacked and the bucket is cleared. This provides coarse-grained timeout without per-entry timestamps.

    let numBuckets = 3
    let buckets = Array.init numBuckets (fun _ -> Dictionary<int64, TreeState>(...))
    let mutable currentBucket = 0

### Lookup across buckets

`tryFind` scans all buckets to locate an anchor, returning `ValueSome(bucketIndex, state)`. `xor` uses the bucket index to update the entry in place:

    let tryFind anchor =
        for i = 0 to numBuckets - 1 do
            match buckets.[i].TryGetValue anchor with ...

    let xor tupleId anchor =
        match tryFind anchor with
        | ValueSome (i, Pending(sourceId, taskId, v, ts)) ->
            let v = v ^^^ tupleId
            ValueSome (i, if v = 0L then Complete(...) else Pending(...))

### Bucket rotation (timeout expiry)

The system timer sends `Tick` to ackers every 30 seconds. On tick:

1. Identify the oldest bucket: `(currentBucket + 1) % numBuckets`
2. For each `Pending` entry in that bucket: send `Nack` to the source spout
3. Clear the bucket
4. Advance `currentBucket` to the now-empty bucket

With 3 buckets and a 30-second tick, entries survive between 30 and 90 seconds before expiry.

```mermaid
graph TD
    TICK[Tick received] --> ROTATE["rotate(): oldest = (current+1) % 3"]
    ROTATE --> SCAN[Scan oldest bucket]
    SCAN --> PENDING{Entry is Pending?}
    PENDING -->|Yes| NACK[Nack srcTupleId to spout]
    PENDING -->|No| SKIP[Skip entry]
    NACK --> CLEAR
    SKIP --> CLEAR
    CLEAR[Clear oldest bucket] --> ADVANCE["currentBucket = oldest"]
```

### High-water mark

A capacity guard prevents unbounded memory growth. When `totalCount()` (sum of all buckets) exceeds `highWater * 2`, new tuples are nacked immediately:

    let totalCount () = buckets |> Array.sumBy (fun b -> b.Count)

    if totalCount() > highWater * 2 then
        Nack sid |> sendToSpout taskId

ID generation
--------
Each task has its own `Random` instance for generating tuple and anchor IDs:

    let seed = ref (int DateTime.Now.Ticks)
    let mkIdGenerator() =
        let rnd = Random(Threading.Interlocked.Increment &seed.contents)
        let rec nextId () =
            let v = rnd.NextInt64()
            if v = 0L then nextId()  // 0 would break XOR tracking
            else v
        nextId

Zero values are excluded because `x ⊕ 0 = x` — a zero tuple ID would be invisible to the XOR accumulator.

*)
