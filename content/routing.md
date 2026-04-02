## Routing

The routing layer determines how tuples emitted by a component reach the appropriate downstream bolt instances. It supports four grouping strategies and handles stream-based fan-out.

## Grouping strategies

When defining a topology, each stream connection specifies a grouping that controls how tuples are distributed across bolt instances:

```fsharp
type Grouping<'t> =
    | Shuffle                               // Hash-based distribution
    | Fields of ('t -> obj) * string list   // Key-based affinity
    | All                                   // Broadcast to all instances
    | Direct                                // Caller-specified target

```

### Shuffle

Distributes tuples across bolt instances by hashing the tuple ID:

```fsharp
| Shuffle when instances.Length = 1 ->
    fun _ _ -> instances :> _ seq          // Optimization: skip hash for single instance
| Shuffle ->
    let ix tupleId = abs(tupleId.GetHashCode() % instances.Length)
    fun tupleId _ ->
        instances.[ix tupleId] |> Seq.singleton

```

graph LR
    SP[Spout] -->|"hash mod 3 = 0"| B0[Bolt 0]
    SP -->|"hash mod 3 = 1"| B1[Bolt 1]
    SP -->|"hash mod 3 = 2"| B2[Bolt 2]

### Fields

Routes tuples to a specific instance based on a key extracted from the tuple. Guarantees that tuples with the same key always go to the same bolt instance:

```fsharp
| Fields (map, _) ->
    fun _ tuple ->
        let ix = (map tuple).GetHashCode() % instances.Length |> abs
        instances.[ix] |> Seq.singleton

```

DSL usage:

```fsharp
yield b1 --> b2 |> Group.by (function Odd(n, _) -> n.x | _ -> failwith "unexpected")

```

graph LR
    B1[Bolt 1] -->|"key=apple, hash mod 2 = 0"| B2a[Bolt 2a]
    B1 -->|"key=banana, hash mod 2 = 1"| B2b[Bolt 2b]

### All

Broadcasts every tuple to all instances of the downstream bolt:

```fsharp
| All ->
    fun _ _ -> instances :> _ seq

```

graph LR
    SP[Spout] --> B0[Bolt 0]
    SP --> B1[Bolt 1]
    SP --> B2[Bolt 2]

### Direct

Allows the emitter to specify the target task ID explicitly. Returns an empty sequence from the group function — routing is handled separately via a direct lookup:

```fsharp
| Direct ->
    fun _ _ -> Seq.empty

// Direct routing bypasses the distributor:
let direct =
    memoize (fun dstId ->
        let (_, (send, _)) = boltTasks |> Map.find dstId
        in Other >> send)

```

## Tuple router construction

`Routing.mkTupleRouter` builds the complete routing function for a task. It combines:

0 **Stream definitions** — which components subscribe to which streams

1 **Grouping functions** — how to select target instances

2 **ID generation** — `mkIds` for tuple tracking

graph TB
    subgraph mkTupleRouter
        EM["Emit(tuple, anchors, srcId, compId, stream, dstId)"]

        EM -->|"dstId = Some id"| DIRECT["Direct Send"]
        EM -->|"dstId = None"| DIST["Distributor Lookup"]

        DIST --> GROUP["Grouping Function"]
        GROUP --> INST["Target Instance(s)"]
        INST --> SEND["Send to Disruptor ring buffer"]

        DIRECT --> SEND
    end

### Distributor construction

For each `(streamId, componentId)` pair in the topology's stream definitions, a distributor function is created at startup:

```fsharp
let mkDistributors taskId map (KeyValue((streamId, dstId), streamDef)) =
    let instances = sinksOfComp dstId
    let group = mkGroup instances streamDef.Grouping
    map |> Map.add streamId (fun mkIds tuple ->
        let ids = mkIds ()
        ids |> Seq.iter (fun tupleId ->
            let msg = Tuple(tuple, tupleId, fst streamId, snd streamId, taskId) |> Other
            group tupleId tuple
            |> Seq.apply msg))

```

### Instance resolution

Bolt instances are grouped by component ID using memoization:

```fsharp
let sinksOfComp =
    let bolts = boltTasks |> Map.groupBy
        (fun (_, (compId, _)) -> compId)
        (fun (_, (_, (send, _))) -> send)
    memoize (fun compId -> bolts.[compId] |> Array.ofSeq)

```

## Stream-based fan-out

A single component can emit to multiple streams, each routed to different downstream bolts:

graph LR
    A[Bolt A] -->|"Odd stream Shuffle"| B1[Bolt B logOdd]
    A -->|"Even stream Shuffle"| B2[Bolt C logEven]

DSL definition:

```fsharp
yield b1 --> b2 |> Shuffle.on Odd    // Odd stream from b1 to b2
yield b1 --> b3 |> Shuffle.on Even   // Even stream from b1 to b3

```

Each stream has its own distributor entry in the routing map. When a bolt emits, the `getStream` function (derived from the DU case) determines which distributor handles it.

## Anchored vs unanchored routing

The routing function receives a `mkIds` parameter that differs based on context:

Context | mkIds Source | Behavior
--- | --- | ---
Spout emit | `TupleTree.track` | Generates `anchorId`, sends `Track` + `Anchor` to acker
Bolt emit (anchored) | `TupleTree.anchor` | Sends `Anchor` to existing tree, returns `"anchorId:tupleId"`
Bolt emit (unanchored) | Identity | Returns plain tuple IDs (no acker interaction)


The `==>` operator in the DSL creates anchored streams where emitted tuples are tracked in the anchor tree.
The `-->` operator creates unanchored streams where emitted tuples are not tracked:

```fsharp
yield s1 ==> b1 |> Shuffle.on Original   // Anchored: b1's emits are tracked
yield b1 ==> b2 |> Shuffle.on Odd        // Anchored: b2's emits are tracked
yield b1 --> b3 |> Shuffle.on Even       // Unanchored: b3's emits are not tracked

```

## Complete routing example

For the Guaranteed sample topology:

graph TB
    S1["numbers (1 instance)"]
    B1a["addOne (instance 0)"]
    B1b["addOne (instance 1)"]
    B2["logOdd (1 instance)"]
    B3["logEven (1 instance)"]

    S1 -->|"Original Shuffle"| B1a
    S1 -->|"Original Shuffle"| B1b
    B1a -->|"Odd Shuffle"| B2
    B1b -->|"Odd Shuffle"| B2
    B1a -->|"Even Shuffle"| B3
    B1b -->|"Even Shuffle"| B3

    A1["Acker 1"]
    A2["Acker 2"]

    S1 -. "Track" .-> A1
    S1 -. "Track" .-> A2
    B1a -. "Anchor + Ok" .-> A1
    B1b -. "Anchor + Ok" .-> A2
    B2 -. "Ok" .-> A1
    B3 -. "Ok" .-> A2

Routing decisions:

* Spout → addOne: `Shuffle` across 2 instances → `hash(tupleId) % 2`

* addOne → logOdd: `Shuffle` across 1 instance → always instance 0

* addOne → logEven: `Shuffle` across 1 instance → always instance 0

* Acker assignment: `anchorId % 2` determines which acker handles the tree
