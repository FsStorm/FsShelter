(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use 
// it to define helpers that you do not want to show in the documentation.
#I "../../bin"
#r "FsShelter.dll"
#r "FsJson.dll"
#r "FsLogging.dll"

open FsJson
open Storm

(**
Defining reliable spouts
========================
[Processing guarantees](https://storm.apache.org/documentation/Guaranteeing-message-processing.html) are the biggest selling point of Storm, please see the official docs for the details.
FsShelter implements reliability semantics with "housekeeper" functions: defaultHousekeeper can be used as is for transient sources or as an inspiration for reliability over external/persistent sources. 
The spout implementation is fairly similar to the "unreliable" version, with the addition of unique (int64) tuple ID:

*)
let rnd = new System.Random() // used for generating random messages

///cfg: the configution passed in by storm
///runner: a spout runner function (passed in from topology)
let spout runner (cfg:Configuration) = 
    let count = ref 0L
    //define the "next" function
    //emit: a function that emits message to storm with unique ID
    let next emit = 
         fun () -> async { 
                     tuple [ rnd.Next(0, 100) ] 
                     |> emit (Threading.Interlocked.Increment &count.contents) 
                   }
    //run the spout
    next |> runner
	
(**
Anchoring and named streams
========================

FsShelter has helper functions to emit to a named stream or to anchor a tuple:

*)
///cfg: the configuration passed in from Storm
///runner: passed in from topology
///emit: passed in from topology
let addOneBolt runner emit cfg = 
    //define the consumer function 
    let add (msg : Json) =
        async { 
            let x = msg?tuple.[0].ValI + 1
            tuple [ x ]
            // write to a named stream
            |> namedStream (match x % 2 with | 0 -> "even" | _ -> "odd")
            // anchor to ensure the entire tuple tree is processed before the spout is ack'ed
            |> anchor msg // anchor to the original message
            |> emit
        }
    //run the bolt
    add |> runner

(**
Example of parametrization and use of the tuple's origin (component that emitted it and the stream that it arrived on) inspection:

*)
///cfg: the configuration passed in by Storm
///runner: passed in from topology
///log: log write
let resultBolt runner log (cfg:Configuration) = 
    let desc = cfg.Json?conf?desc.Val // the value passed in with the submitted topology Config
    //define the function that will return the consumer 
    let logResult (msg : Json) = 
          async { 
            log desc (sprintf "origin: %A(%A), data: %A" msg?comp.Val msg?stream.Val msg?tuple.[0].ValI)
          }
    //run the bolt
    logResult |> runner

(**
Topology with named streams and config overrides
========================

*)

open StormDSL

let topology = 
    { TopologyName = "FstGuaranteed"
      Spouts = 
          [ { Id = "ReliableSpout"
              Outputs = [ Default [ "number" ] ]
              Spout = Local { Func = spout (Storm.reliableSpoutRunner Storm.defaultHousekeeper) }
              Config = jval [ "topology.max.spout.pending", jval 123 ] // override "backpressure"
              Parallelism = 1 } ]
      Bolts = 
          [ { Id = "AddOneBolt"
              Outputs = [ Named("even", [ "number" ]) // named stream "even"
                          Named("odd", [ "number" ]) ]
              Inputs = [ DefaultStream "ReliableSpout", Shuffle ]
              Bolt = Local { Func = addOneBolt Storm.autoAckBoltRunner Storm.emit }
              Config = JsonNull
              Parallelism = 2 }
            { Id = "EvenResultBolt"
              Outputs = []
              Inputs = [ Stream("AddOneBolt","even"), Shuffle ]
              Bolt = Local { Func = resultBolt Storm.autoAckBoltRunner }
              Config = jval ["desc", "even"] // pass custom config property to the component
              Parallelism = 1 }
            { Id = "OddResultBolt"
              Outputs = []
              Inputs = [ Stream("AddOneBolt","odd"), Shuffle ]
              // logs to custom (FsLogging) pid-based log file
              Bolt = Local { Func = resultBolt Storm.autoAckBoltRunner Logging.log }
              Config = jval ["desc", "odd"] // pass custom config property to the component
              Parallelism = 1 } ] }

