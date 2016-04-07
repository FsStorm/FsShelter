(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use 
// it to define helpers that you do not want to show in the documentation.
#I "../../build"
#r "FsShelter.dll"

open System
open FsShelter
(**
Overview
-------
FsShelter is a library for implementation of [Apache Storm](https://storm.apache.org/) components and topologies in F#.
FsShelter is based on and a major rewrite of [FsStorm](https://github.com/FsStorm). It departs from FsStrom in significant ways and therefore has been split into itsown project.

Overall, the librabry provides "batteries included" experience with wrappers for Nimbus API as well as support for packaging and exporting:

- bundle and submit a topology for execution w/o needing JDK or Storm CLI
- include Storm-side serializer along
- kill a running topology
- generate a topology graph as part of your build

The topology and the components could be implemented in a single EXE project and are executed by Storm via its [multilang](https://storm.apache.org/documentation/Multilang-protocol.html) protocol as separate processes - one for each task/instance.
Corresponding [ProtoShell](https://github.com/prolucid/protoshell) and [ThriftShell](https://github.com/prolucid/thriftshell) libraries facilitate Protobuf and Thrift serialization, which improve throughput of FsShelter topologies as compared to standard JSON.
See samples to learn how to bundle the assemblies and a serializer for upload to Storm.

Bring your own, if you need it:

- command line parser
- logging
- custom serializer


FsShelter topology schema
-----------------------
While Storm tuples are dynamically typed and to a large extend the types are transparent to Storm itself, they are not types-less. 
Mistakes and inconsistencies between declared outputs and tuple consumers could easily lead to errors detectable at run-time only and may be frustrating to test, detect and fix.
FsShelter introduces concept of topology schema, defined as F# discriminated union:
*)

type BasicSchema = 
    | Original of int
    | Incremented of int

(**
where every DU case becomes a distinct stream in the topology. The fields of each DU case will become tuple fields in Storm streams.

It is often handy to define a type that's shared across streams and FsShelter supports defining cases with records:
*)

type Number = { X:int; Desc:string }

type RecordSchema = 
    | Original of int
    | Described of Number
    | Translated of Number

(**
It is also common to join/zip tuples from multiple streams and FsShelter supports defining cases with records adjoined:
*)

type RecordsSchema = 
    | Original of Number
    | Doubled of Number*Number

(**
Other than safety of working with statically-verified schema the reason we care about structure of the tuple is because we reference them in Storm grouping definitions.
FsShelter "flattens" the first immediate "layer" of the DU case so that all the fields, weither they come from the embedded record or the DU case itself, are available for grouping expressions.


FsShelter components
-----------------------
Some of the flexibility of Storm has been hidden to provide simple developer experience for authoring event-driven solutions.
For exmple, FsShelter components are implemeted as simple functions:
*)

// numbers spout - produces messages
let numbers source = async { return Some(Original(source())) }

(**
The async body of a spout is expected to return an option if there's a tuple to emit or None if there's nothing to emit at this time.

Bolts can get a tuple on any number of streams, and so we pattern match:
*)

// add 1 bolt - consumes and emits messages to Incremented stream
let addOne (input, emit) = 
    async { 
        match input with
        | BasicSchema.Original(x) -> Incremented(x + 1)
        | _ -> failwithf "unexpected input: %A" input
        |> emit
    }

(**
The bolt can also emit at any time, and we can hold on to the passed emit function (with caveates).
Also, there can be as many arguments for the component functions as needed, the specifics will be determined when the components are put together in a topology.
*)

// terminating bolt - consumes messages
let logResult (info, input) = 
    async { 
        match input with
        | BasicSchema.Incremented(x) -> info (sprintf "%A" x)
        | _ -> failwithf "unexpected input: %A" input
    }

(**


Using F# DSL to define the topology
--------------------

Storm topology is a graph of spouts and bolts connected via streams. FsShelter provides an embedded DSL for defining the topologies, which allows for mix and match of native Java, external shell and FsShell components:
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
        let s1 = numbers |> runUnreliably (fun log cfg -> source) // ignoring available Storm logging and cfg and passing our source function
        
        let b1 = 
            addOne
            |> runBolt (fun log cfg tuple emit -> (tuple, emit)) // pass incoming tuple and emit function
            |> withParallelism 2 // override default parallelism of 1
        
        let b2 = 
            logResult
            |> runBolt (fun log cfg tuple emit -> ((log LogLevel.Info), tuple)) // example of passing Info-level Storm logger into the bolt
            |> withParallelism 2
        
        yield s1 --> b1 |> shuffle.on BasicSchema.Original // emit from s1 to b1 on Original stream
        yield b1 --> b2 |> shuffle.on Incremented // emit from b1 to b2 on Incremented stream
    }

(**
Storm will start (a copy of) the same EXE for every component instance in the topology and will assign each instance a task it supposed to execute.

The topology can be packaged with all its dependecies and submitted using embedded Nimbus client, see the examples for details.


Exporting the topology graph in DOT format (GraphViz) using F# scripts
-----------------------
Once the number of components grows beyond trivial it is often handy to be able to visualize them and FsShelter includes a simple way to export the topology into a graph:
*)

sampleTopology |> DotGraph.writeToConsole

(**
See the samples included for further details.

Samples & documentation
-----------------------

 * [WordCount](wordcount.html) contains a "unreliable" spout example - emitted tuples do not require ack, could be lost in case of failure.

 * [Guaranteed](guaranteed.html) contains a "reliable" spout example - emitted tuples have unique ID and require ack.

 * [API Reference](reference/index.html) contains automatically generated documentation for public types, modules
   and functions in the library. 
 
Getting FsShelter
----------------

<div class="row">
  <div class="span1"></div>
  <div class="span6">
    <div class="well well-small" id="nuget">
      The FsShelter library can be installed from <a href="https://nuget.org/packages/FsShelter">NuGet</a> or <a href="https://www.myget.org/F/FsShelter/">MyGet</a>:
      <pre>PM> Install-Package FsShelter</pre>
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
<a href="http://prolucid.ca/" target="_blank"><img src="http://prolucid.ca/wp-content/uploads/2014/06/Logo-.jpg" alt="Prolucid" style="height:30px" border="0" /></a>


  [content]: https://github.com/Prolucid/FsShelter/tree/master/docs/content
  [gh]: https://github.com/Prolucid/FsShelter
  [issues]: https://github.com/Prolucid/FsShelter/issues
  [readme]: https://github.com/Prolucid/FsShelter/blob/master/README.md
  [license]: https://github.com/Prolucid/FsShelter/blob/master/LICENSE.md
*)
