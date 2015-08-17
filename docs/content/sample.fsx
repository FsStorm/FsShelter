(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use 
// it to define helpers that you do not want to show in the documentation.
#I "../../bin"
#r "FsStorm.dll"
#r "FsJson.dll"
#r "FsLogging.dll"

open FsJson
open Storm

(**
Defining unreliable spouts
========================

FsStorm spouts can be implemented as "reliable" or "unreliable". 
Unreliable spouts implementation can be as simple as a single function:

*)
let rnd = new System.Random() // used for generating random messages

///cfg: the configution passed in by storm
///runner: a spout runner function (passed in from topology)
let spout runner cfg = 
    //define the function that will return the emitter function
    //emit: a function that emits message to storm
    let createEmitter emit = fun () -> async { tuple [ rnd.Next(0, 100) ] |> emit } //the "next" function
    //run the spout
    createEmitter |> runner

(**
Defining bolts
========================

Example of a FsStorm bolt that reads a tuple and emits another one:

*)
///cfg: the configuration passed in from Storm
///runner: passed in from topology
///log: passed in from topology
///emit: passed in from topology
let addOneBolt runner log emit cfg = 
    //define the function that will return the consumer function
    let createAdder = 
        //accept/consume tuple function
        fun (msg : Json) -> 
            async { 
                log "msg" (sprintf "%A" msg)
                // note the tuple fields are accessible as Jval array items
                tuple [ msg?tuple.[0].ValI + 1 ] |> emit
            }
    //run the bolt
    createAdder |> runner

(**
And a terminating bolt that reads a tuple, but doesn't emit anything:

*)
///cfg: the configution passed in by storm
///runner: passed in from topology
///log: passed in from topology
let resultBolt runner log cfg = 
    //define the function that will return the consumer function
    let createReader = 
        //accept messages function
        fun (msg : Json) -> async { log "x" (sprintf "%A" msg?tuple.[0].ValI) }
    //run the bolt
    createReader |> runner


(**
Using F# DSL to define the topology
========================

Topologies are defined using a strong-typed DSL:
*)

open StormDSL

let topology = 
    { TopologyName = "FstSample"
      Spouts = 
          [ { Id = "SimpleSpout"
              Outputs = [ Default [ "number" ] ]
              Spout = Local { Func = spout Storm.simpleSpoutRunner }
              Config = JsonNull
              Parallelism = 1 } ]
      Bolts = 
          [ { Id = "AddOneBolt"
              Outputs = [ Default [ "number" ] ]
              Inputs = [ DefaultStream "SimpleSpout", Shuffle ]
              Bolt = Local { Func = addOneBolt Storm.autoAckBoltRunner Logging.log Storm.emit}
              Config = JsonNull
              Parallelism = 2 }
            { Id = "ResultBolt"
              Outputs = []
              Inputs = [ DefaultStream "AddOneBolt", Shuffle ]
              Bolt = Local { Func = resultBolt Storm.autoAckBoltRunner Logging.log }
              Config = JsonNull
              Parallelism = 2 } ] }


(**
Unit-testing the components
========================

Several runners are implemented to facilitate unit-testing in StormTest module:
*)

open NUnit.Framework
open System
open StormTest

let conf = 
    { PidDir = ""
      TaskId = ""
      Json = jval "" }

[<Test>]
let ``spout emits``() = 
    let results = ref []
    let s = spout (simpleSpoutRunner (fun x -> results := x :: results.Value) [ next; next ]) conf
    Async.RunSynchronously s
    match results.Value with
    | x :: y :: [] -> ()
    | _ -> Assert.Fail "Must have emitted exactly two!"

[<Test>]
let ``bolt adds``() = 
    let results = ref []
    let b = 
        addOneBolt (autoAckBoltRunner [ tuple [ 123 ] ] Console.WriteLine) 
            (fun tag desc -> Console.WriteLine("{0}: {1}", tag, desc)) (fun t -> results := t :: results.Value) conf
    Async.RunSynchronously(b, 2000)
    match results.Value with
    | x :: [] -> Assert.AreEqual(124, x?tuple.[0].ValI)
    | _ -> Assert.Fail "Head, no tail, that's the deal!"

[<Test>]
let ``bolt consumes``() = 
    let results = ref []
    let b = 
        resultBolt (autoAckBoltRunner [ tuple [ 123 ] ] Console.WriteLine) 
            (fun tag desc -> results := (tag, desc) :: results.Value) conf
    Async.RunSynchronously(b, 2000)
    match results.Value with
    | x :: [] -> Assert.AreEqual(("x", sprintf "%A" 123), x)
    | _ -> Assert.Fail "Head, no tail, that's the deal!"
