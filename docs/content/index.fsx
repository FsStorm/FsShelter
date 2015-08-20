(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use 
// it to define helpers that you do not want to show in the documentation.
#I "../../bin"
#r "FsJson.dll"
#r "Storm.dll"

(**
FsStorm
======================

Overview
-------
FsStorm is a library for implementation of [Apache Storm](https://storm.apache.org/) components, definition of topologies in F# DSL and submission via F# scripts for execution.
The topology and the components could be implemented in a single EXE project and are executed by Storm via its [multilang](https://storm.apache.org/documentation/Multilang-protocol.html) protocol as separate processes - one for each task/instance.
Accompanying FsJson library is used for dealing with Json structures passed in and out of Storm.

FsStorm components
-----------------------

FsStorm components are defined as functions that take at least one (last) argument: configuration passed in from Storm. In practice, you'll want to pass all your dependencies in, and that means at least one other: a runner, passed in from your topology. 
Additionally you can pass as many arguments from the topology as needed.
Think of the component function as "main" for your program. Storm will start (a copy of) the same EXE for all components in the topology, and will instruct each instance with the task it supposed to execute.
The "main" function will be called by FsStorm once per instance of every component and its purpose is to construct either "next" function for spouts or "consume" function for bolts and pass it to a runner.
FsStorm implements several runners that either talk to Storm or allow you to unit-test your components by recording outputs or playing back the inputs.


FsStorm tuples
-----------------------

Storm components communicate by passing tuples to each other over streams. The tuples are emmited into streams and have schema defined by the spout topology Output element.
Storm multilang is wrapped and accessible via included FsJson.
In addition to raw json access, FsStorm defines several helpers: tuple, namedStream, anchor, etc. that help to abstract the specifics of underlying multilang.


Example of a spout
-----------------------

*)
open FsJson
open Storm

let rnd = new System.Random() // used for generating random messages

///spout - produces messages
///cfg: the configuration passed in by Storm
///runner: a spout runner function (passed in from topology)
let spout runner cfg = 
    //define the function that will produce the tuples
    //emit: a function that emits message to storm (passed in by the runner)
    let next emit = fun () -> async { tuple [ rnd.Next(0, 100) ] |> emit } //the "next" function
    //run the spout
    next |> runner

(**
Topology DSL in F#
-----------------------
*)

//define the storm topology
open StormDSL
open FsJson

//example of using FsStorm DSL for defining topologies
let topology = 
    { TopologyName = "FstSample"
      Spouts = 
          [ { Id = "SimpleSpout" // unique Id
              Outputs = [ Default [ "number" ] ] // default stream schema
              Spout = Local { Func = spout Storm.simpleSpoutRunner }
              // configuration Storm will use with each instance of the component 
              Config = JsonNull 
              Parallelism = 1 } ] // one instance
      Bolts = 
          [ { Id = "AddOneBolt"
              Outputs = [ Default [ "number" ] ]
              // default stream of SimpleSpout, no grouping/affinity 
              Inputs = [ DefaultStream "SimpleSpout", Shuffle ]
              Bolt = Local { Func = addOneBolt Storm.autoAckBoltRunner Logging.log Storm.emit}
              Config = JsonNull
              Parallelism = 2 } // two instances of the process/component
            { Id = "ResultBolt"
              Outputs = [] // no output
              Inputs = [ DefaultStream "AddOneBolt", Shuffle ]
              Bolt = Local { Func = resultBolt Storm.autoAckBoltRunner Logging.log }
              Config = JsonNull
              Parallelism = 2 } ] }

(**
Submitting the topology using F# scripts
-----------------------
*)

#I "../../Refs"
#I "../../packages/Thrift/lib/net35"
#load "StormSubmit.fsx"

let binDir = "build"

StormSubmit.runTopology binDir "localhost" StormSubmit.default_nimbus_port


(**
Samples & documentation
-----------------------

 * [FstSample](sample.html) contains a "unreliable" spout example - emitted tuples do not require ack, could be lost in case of failure.

 * [FstGuaranteedSample](guaranteed.html) contains a "reliable" spout example - emitted tuples have unique ID and require ack.

 * [API Reference](reference/index.html) contains automatically generated documentation for public types, modules
   and functions in the library. 
 
 * [WordCount](https://github.com/FsStorm/FsStorm.WordCount) contains a simple example showing a spout with two bolts.

Getting FsStorm
----------------

<div class="row">
  <div class="span1"></div>
  <div class="span6">
    <div class="well well-small" id="nuget">
      The FsStorm library can be <a href="https://nuget.org/packages/FsStorm">installed from NuGet</a>:
      <pre>PM> Install-Package FsStorm</pre>
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

The library is available under MIT license, which allows modification and 
redistribution for both commercial and non-commercial purposes. For more information see the 
[License file][license] in the GitHub repository. 

  [content]: https://github.com/FsStorm/FsStorm/tree/master/docs/content
  [gh]: https://github.com/FsStorm/FsStorm
  [issues]: https://github.com/FsStorm/FsStorm/issues
  [readme]: https://github.com/FsStorm/FsStorm/blob/master/README.md
  [license]: https://github.com/FsStorm/FsStorm/blob/master/LICENSE.md
*)
