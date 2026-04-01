(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use 
// it to define helpers that you do not want to show in the documentation.
#I "../../src/FsShelter/bin/Release/net10.0"
#r "FsShelter.dll"

open System
open FsShelter
(**
Overview
-------
FsShelter is a library that lets you implement [Apache Storm](https://storm.apache.org/) components and topologies in F#.
FsShelter is a major rewrite of [FsStorm](https://github.com/FsStorm). It departs from FsStorm in significant ways and therefore has been split into its own project.

The library is split into two packages:

- **FsShelter** — the core library for defining topologies and running them in-process (self-hosting). Depends only on Disruptor for high-performance message passing.
- **FsShelter.Multilang** — Storm cluster integration: task execution via the [multilang](https://storm.apache.org/documentation/Multilang-protocol.html) protocol, Nimbus client for topology submission, and JSON/Protobuf serializers. Depends on Protobuf, Thrift, Newtonsoft.Json, and FsPickler.

Use `FsShelter` alone for [self-hosted](self-hosting.html) topologies. Add `FsShelter.Multilang` when deploying to a Storm cluster.

With the Multilang package, the library provides a "batteries included" experience:

- Bundle and submit a topology for execution without needing JDK or Storm CLI
- Includes Storm-side serializer ([ProtoShell](https://github.com/FsStorm/protoshell))
- Kill a running topology
- Generate a topology graph as part of your build

Bring your own, if you need it:

- command line parser
- logging
- custom serializer



FsShelter topology schema
-----------------------
While Storm tuples are dynamically typed, and to a large extent the types are transparent to Storm itself, they are not type-less. 
Mistakes and inconsistencies between declared outputs and tuple consumers could easily lead to errors detectable at run-time only and may be frustrating to test, detect and fix.
FsShelter introduces the concept of a topology schema, defined as an F# discriminated union:
*)

type BasicSchema = 
    | Original of int
    | Incremented of int

(**
Every DU case becomes a distinct stream in the topology. The fields of each DU case will become tuple fields in Storm streams.

It is often handy to define a type that's shared across streams, and FsShelter supports defining cases with records:
*)

type Number = { X:int; Desc:string }

type RecordSchema = 
    | Original of int
    | Described of Number
    | Translated of Number

(**
It is also common to join/zip tuples from multiple streams, and FsShelter supports defining cases with records adjoined:
*)

type RecordsSchema = 
    | Original of Number
    | Doubled of Number * Number

(**
Other than the safety of working with a statically-verified schema, the reason we care about the structure of the tuples is because we reference them in Storm grouping definitions.
FsShelter "flattens" the first immediate "layer" of the DU case so that all the fields, weither they come from the embedded record or the DU case itself, are available for grouping expressions.

Generic or nested schemas are also supported. For example:
*)

type BasicSchema = 
    | Original of int
    | Incremented of int

type NestedSchema<'a> = 
    | Named of string
    | Nested of 'a
    
(**
where a topology can be defined with the signature: `Topology<NestedSchema<BasicSchema>>`.
This can be useful for implementing a base topology and extending it using a nested set of streams. Nested streams can be grouped on by adding the `NestedStreamAttribute` to the `Nested` case. Without this attribute, nested streams will be treated as blobs.
*)

type NestedSchema<'a> = 
    | Named of string
    | [<NestedStream>] Nested of 'a


(**
FsShelter components
-----------------------
Some of the flexibility of Storm has been hidden to provide a simple developer experience for authoring event-driven solutions.
For exmple, FsShelter components are implemeted as simple functions:
*)

// numbers spout - produces messages
let numbers source = return Some(Original(source()))

(**
The async body of a spout is expected to return an option if there's a tuple to emit or None if there's nothing to emit at this time.

Bolts can get a tuple on any number of streams, and so we pattern match:
*)

// add 1 bolt - consumes and emits messages to Incremented stream
let addOne (input, emit) = 
    match input with
    | BasicSchema.Original(x) -> Incremented(x + 1)
    | _ -> failwithf "unexpected input: %A" input
    |> emit

(**
The bolt can also emit at any time, and we can hold on to the passed-in `emit` function (with caveats).
Also, there can be as many arguments for the component functions as needed; the specifics will be determined when the components are put together in a topology.
*)

// terminating bolt - consumes messages
let logResult (info, input) = 
    match input with
    | BasicSchema.Incremented(x) -> info (sprintf "%A" x)
    | _ -> failwithf "unexpected input: %A" input

(**


Using F# DSL to define the topology
--------------------

Storm topology is a graph of spouts and bolts connected via streams. FsShelter provides an embedded DSL for defining the topologies, which allows for mixing and matching of native Java, external shell, and FsShelter components:
*)

// define our source dependency
let source = 
    let rnd = Random()
    fun () -> rnd.Next(0, 100)

open FsShelter.DSL
open FsShelter.Multilang

//define the Storm topology
let sampleTopology = 
    topology "Sample" { 
        let s1 = 
            numbers
            |> Spout.runUnreliable (fun log cfg -> source) // ignoring available Storm logging and cfg and passing our source function
                                   ignore                  // no deactivation
        let b1 = 
            addOne
            |> Bolt.run (fun log cfg tuple emit -> (tuple, emit)) // pass incoming tuple and emit function
            |> withParallelism 2 // override default parallelism of 1
        
        let b2 = 
            logResult
            |> Bolt.run (fun log cfg tuple emit -> ((log LogLevel.Info), tuple)) // example of passing Info-level Storm logger into the bolt
            |> withParallelism 2
        
        yield s1 --> b1 |> Shuffle.on BasicSchema.Original // emit from s1 to b1 on Original stream
        yield b1 --> b2 |> Shuffle.on Incremented // emit from b1 to b2 on Incremented stream
    }

(**
Storm will start (a copy of) the same EXE for every component instance in the topology and will assign each instance a task it supposed to execute.

The topology can be packaged with all its dependecies and submitted using the embedded Nimbus client; see the examples for details.


Exporting the topology graph in DOT format (GraphViz) using F# scripts
-----------------------
Once the number of components grows beyond a trivial number, it is often handy to be able to visualize them. FsShelter includes a simple way to export the topology into a graph:
*)

sampleTopology |> DotGraph.writeToConsole

(**
See the samples included for further details.

Samples & documentation
-----------------------

 * [WordCount](wordcount.html) contains an "unreliable" spout example - emitted tuples do not require ack, and could be lost in case of failure.

 * [Guaranteed](guaranteed.html) contains a "reliable" spout example - emitted tuples have a unique ID and require ack.

 * [Self-hosting](self-hosting.html) describes the in-process runtime that lets you run topologies without Storm.

 * [API Reference](reference/index.html) contains automatically generated documentation for public types, modules
   and functions in the library. 
 
Getting FsShelter
----------------

<div class="row">
  <div class="span1"></div>
  <div class="span6">
    <div class="well well-small" id="nuget">
      The core library (topology DSL, self-hosting runtime) can be installed from <a href="https://nuget.org/packages/FsShelter">NuGet</a>:
      <pre>PM> Install-Package FsShelter</pre>
    </div>
  </div>
  <div class="span1"></div>
</div>

<div class="row">
  <div class="span1"></div>
  <div class="span6">
    <div class="well well-small">
      For Storm cluster deployment, add the multilang package from <a href="https://nuget.org/packages/FsShelter.Multilang">NuGet</a>:
      <pre>PM> Install-Package FsShelter.Multilang</pre>
    </div>
  </div>
  <div class="span1"></div>
</div>

Contributing and copyright
--------------------------

The project is hosted on [GitHub][gh] where you can [report issues][issues], fork 
the project and submit pull requests. If you're adding a new public API, please also 
consider adding [samples][content] that can be turned into a documentation. You might
also want to read the [library design notes][readme] to understand how it works.

The library is available under Apache 2.0 license, which allows modification and 
redistribution for both commercial and non-commercial purposes. For more information see the 
[License file][license] in the GitHub repository. 

Commercial support
--------------------------

Commercial training and support are available from the project sponsor: 
<a href="http://FsStorm.ca/" target="_blank"><img src="http://FsStorm.ca/wp-content/uploads/2014/06/Logo-.jpg" alt="FsStorm" style="height:30px" border="0" /></a>


  [content]: https://github.com/FsStorm/FsShelter/tree/master/docs/content
  [gh]: https://github.com/FsStorm/FsShelter
  [issues]: https://github.com/FsStorm/FsShelter/issues
  [readme]: https://github.com/FsStorm/FsShelter/blob/master/README.md
  [license]: https://github.com/FsStorm/FsShelter/blob/master/LICENSE.md
*)
