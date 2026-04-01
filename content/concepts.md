## Core Concepts

FsShelter is a library for building stream-processing pipelines in F#. A pipeline is a directed graph where data flows from sources through processors, each transforming or routing messages along the way.

You don't need to know anything about Apache Storm to use FsShelter — this page covers the core ideas from scratch.

## What is stream processing?

Imagine a data pipeline: events arrive continuously (sensor readings, log entries, user clicks), get transformed, filtered, aggregated, then stored or forwarded. Instead of collecting events into a batch and processing them later, stream processing handles each event as it arrives — giving you low latency and the ability to react in real time.

A stream-processing topology is a directed acyclic graph (DAG) of processing steps. FsShelter lets you define these graphs with F# types and functions, then run them in-process with high-performance message passing.

## Key terminology

Term | What it means
--- | ---
**Spout** | A data source. Produces messages and feeds them into the pipeline. Example: reading from a message queue, generating random data, tailing a log file.
**Bolt** | A processor. Receives messages, transforms them, and optionally emits new messages downstream. Example: splitting sentences into words, counting occurrences, filtering by threshold.
**Tuple** | A single message flowing through the pipeline. In FsShelter, tuples are instances of your F# discriminated union — fully typed.
**Stream** | A named, typed channel connecting components. Each case of your schema DU defines a distinct stream.
**Topology** | The complete graph: spouts, bolts, and the streams connecting them.
**Schema** | An F# discriminated union that defines all the message types in your topology. It provides static type safety across the entire pipeline.


## Topology schema

While most stream-processing systems use dynamically typed messages, FsShelter uses F# discriminated unions to statically type every stream. Mistakes and inconsistencies between producers and consumers are caught at compile time, not at runtime.

Every DU case becomes a distinct stream. The fields of each case become the tuple's payload:

```fsharp
type BasicSchema = 
    | Original of int
    | Incremented of int
```

It is often handy to define a record type shared across streams:

```fsharp
type Number = { X:int; Desc:string }

type RecordSchema = 
    | Original of int
    | Described of Number
    | Translated of Number
```

Joining tuples from multiple streams is also supported:

```fsharp
type RecordsSchema = 
    | Original of Number
    | Doubled of Number * Number
```

Generic or nested schemas work too. You can define a base schema and extend it:

```fsharp
type SimpleSchema = 
    | Value of int
    | Transformed of int

type NestedSchema<'a> = 
    | Named of string
    | [<NestedStream>] Nested of 'a
```

A topology using `Topology<NestedSchema<SimpleSchema>>` combines both sets of streams.
For more about schema structure, grouping expressions, and serializer details, see [Schema reference](schema.html).

## Components

FsShelter components are plain F# functions — no base classes, no interfaces, no framework inheritance.

A **spout** returns an option: `Some tuple` to emit, `None` when there's nothing to emit right now:

```fsharp
// numbers spout - produces a message
let numbers source = Some(BasicSchema.Original(source()))
```

A **bolt** receives a tuple and optionally emits downstream. Pattern matching on the schema DU gives you exhaustive, type-safe dispatch:

```fsharp
// add one bolt - transforms and emits
let addOne (input, emit) = 
    match input with
    | BasicSchema.Original(x) -> BasicSchema.Incremented(x + 1)
    | _ -> failwithf "unexpected input: %A" input
    |> emit

// terminating bolt - consumes without emitting
let logResult (info, input) = 
    match input with
    | BasicSchema.Incremented(x) -> info (sprintf "%A" x)
    | _ -> failwithf "unexpected input: %A" input
```

The arguments to your component functions are wired up when you define the topology — you choose exactly what each function receives. There's no global state or magic injection.

## Topology DSL

FsShelter provides a computation expression for defining topologies. You declare components and connect them with arrows:

* `-->` connects two components on a stream (unanchored — fire and forget)

* `==>` connects with anchoring (for reliable delivery — see below)

* `Shuffle.on` distributes tuples across instances by hashing

* `Group.by` routes tuples with the same key to the same instance (affinity)

* `withParallelism n` runs `n` instances of a component

Here's a minimal topology:

```fsharp
let source = 
    let rnd = Random()
    fun () -> rnd.Next(0, 100)

open FsShelter.DSL
open FsShelter.Multilang

let sampleTopology = 
    topology "Sample" { 
        let s1 = 
            numbers
            |> Spout.runUnreliable (fun log cfg -> source) ignore
        let b1 = 
            addOne
            |> Bolt.run (fun log cfg tuple emit -> (tuple, emit))
            |> withParallelism 2
        let b2 = 
            logResult
            |> Bolt.run (fun log cfg tuple emit -> ((log LogLevel.Info), tuple))
            |> withParallelism 2
        
        yield s1 --> b1 |> Shuffle.on BasicSchema.Original
        yield b1 --> b2 |> Shuffle.on BasicSchema.Incremented
    }
```

The lambda arguments for the `run` methods construct the arguments passed to your component functions:

* `log` is a logging factory

* `cfg` is the runtime configuration

* `tuple` is the incoming message (schema DU instance)

* `emit` is a function to emit a new tuple downstream

`log` and `cfg` are curried once at startup. The `tuple` and `emit` arguments arrive per-message.

## Reliable vs unreliable delivery

FsShelter supports two delivery modes:

**Unreliable (fire and forget):** Tuples are emitted and processed but not tracked. If a bolt fails, the message is lost. This is the simplest mode and has the lowest overhead. Use it when losing an occasional message is acceptable (metrics, logging, non-critical analytics).

**Reliable (guaranteed delivery):** Every tuple emitted by a spout is tracked through the entire DAG. When all downstream bolts finish processing, the spout receives an **ack**. If any bolt fails or processing takes too long, the spout receives a **nack** and can retry. This is implemented via an XOR-tree tracking algorithm (see [Acker algorithm](acker-algorithm.html) for details).

In the DSL, the arrow operator determines the mode:

* `==>` creates an anchored stream (starts or continues tracking)

* `-->` creates an unanchored stream (fire and forget)

See [Word Count](wordcount.html) for an unreliable example and [Guaranteed](guaranteed.html) for a reliable example.

## Running a topology

FsShelter topologies run entirely in-process — no external infrastructure required. The runtime uses Disruptor ring buffers for high-performance, low-latency message passing between components.

```fsharp
let shutdown = Hosting.run myTopology

```

This starts all components, wires up the message routing, and returns a function you call to shut down cleanly. For production use with auto-restart and logging, see [Running Topologies](self-hosting.html).

If you need to deploy to an Apache Storm cluster for horizontal scale-out, the `FsShelter.Multilang` package provides Storm integration. See [Word Count: Deploying to Storm](wordcount.html) for details.

## Visualizing topologies

FsShelter can export any topology as a GraphViz DOT graph:

```fsharp
sampleTopology |> DotGraph.writeToConsole
```

Pipe the output through `dot` to generate SVG or PNG:

dotnet run -- graph | dot -Tsvg -o topology.svg

## Next steps

* ***[Word Count](wordcount.html)*** — Complete tutorial: schema, components, topology, graph export

* ***[Guaranteed delivery](guaranteed.html)*** — Reliable spouts with ack/nack and anchoring

* ***[Schema reference](schema.html)*** — Grouping expressions, flattening, serializer details

* ***[Running Topologies](self-hosting.html)*** — Entry points, configuration, diagnostics

* ***[Routing](routing.html)*** — How tuples are distributed across component instances

* ***[Architecture](architecture.html)*** — Internal runtime structure: tasks, executors, channels

* ***[Message Flow](message-flow.html)*** — End-to-end processing scenarios with sequence diagrams

* ***[Acker Algorithm](acker-algorithm.html)*** — XOR-tree tracking for guaranteed delivery
