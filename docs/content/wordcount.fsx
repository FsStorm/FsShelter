(*** hide ***)
#I "../../src/FsShelter/bin/Release/netstandard2.0"
#r "FsShelter.dll"

open System
open FsShelter

(**
Defining the schema
--------------------

FsShelter uses F# discriminated unions to statically type streams:

*)

// data schema for the topology, every case is a unqiue stream
type Schema = 
    | Sentence of string
    | Word of string
    | WordCount of string * int64

(**
Defining unreliable spouts
--------------------

FsShelter spouts can be implemented as "reliable" or "unreliable". 
Either implementation is a single function, returning an `async option`, where `None` indicates there's nothing to emit from this spout at this moment:

*)

// sentences spout - feeds messages into the topology
let sentences source = source() |> Sentence |> Some

(**
Defining bolts
--------------------

A couple of examples of FsShelter bolts that read a tuple and emit another one:

*)

// split bolt - consumes sentences and emits words
let splitIntoWords (input, emit) = 
    match input with
    | Sentence s -> s.Split([|' '|],StringSplitOptions.RemoveEmptyEntries) 
                    |> Seq.map Word 
                    |> Seq.iter emit
    | _ -> failwithf "unexpected input: %A" input

// count words bolt 
let countWords (input, increment, emit) = 
    match input with
    | Word word -> WordCount (word, increment word) |> emit
    | _ -> failwithf "unexpected input: %A" input

(**
And a terminating bolt that reads a tuple, but doesn't emit anything:

*)

// log word count - terminating bolt 
let logResult (log, input) = 
    match input with
    | WordCount (word,count) -> log (sprintf "%s: %d" word count)
    | _ -> failwithf "unexpected input: %A" input


(**
We will pass these implementations into the component functions when we put things together in a topology definition.

*)
let source = 
    let rnd = new System.Random()
    let sentences = [ "the cow jumped over the moon"
                      "an apple a day keeps the doctor away"
                      "four score and seven years ago"
                      "snow white and the seven dwarfs"
                      "i am at two with nature" ]

    fun () -> sentences.[ rnd.Next(0, sentences.Length) ]

// increment word count and return new value
let increment =
    let cache = Collections.Concurrent.ConcurrentDictionary<string,int64 ref>()
    fun word -> 
        let c = cache.GetOrAdd(word, ref 0L) 
        Threading.Interlocked.Increment &c.contents

(**


Using F# DSL to define a topology
--------------------

A typical (event-streaming) Storm topology is a graph of spouts and bolts connected via streams that can be defined via a `topology` computation expression:
*)

open FsShelter.DSL
#nowarn "25" // for stream grouping expressions

//define the storm topology
let sampleTopology = 
    topology "WordCount" { 
        let sentencesSpout = 
            sentences
            |> Spout.runUnreliable (fun log cfg -> source)  // make arguments: ignoring Storm logging and cfg, passing our source function
                                   ignore                   // no deactivation
            |> withParallelism 4
        
        let splitBolt = 
            splitIntoWords
            |> Bolt.run (fun log cfg tuple emit -> (tuple, emit)) // make arguments: pass incoming tuple and emit function
            |> withParallelism 4
        
        let countBolt = 
            countWords
            |> Bolt.run (fun log cfg tuple emit -> (tuple, increment, emit))
            |> withParallelism 4
        
        let logBolt = 
            logResult
            |> Bolt.run (fun log cfg ->                           // make arguments: pass PID-log and incoming tuple 
                            let mylog = Common.Logging.asyncLog (Diagnostics.Process.GetCurrentProcess().Id.ToString()+"_count")
                            fun tuple emit -> (mylog, tuple))
            |> withParallelism 2
        
        yield sentencesSpout --> splitBolt |> Shuffle.on Sentence               // emit from sentencesSpout to splitBolt on Sentence stream, shuffle among target task instances
        yield splitBolt --> countBolt |> Group.by (function Word w -> w)        // emit from splitBolt into countBolt on Word stream, group by word (into the same task instance)
        yield countBolt --> logBolt |> Group.by (function WordCount (w,_) -> w) // emit from countBolt into logBolt on WordCount stream, group by word value
    }

(**
Here we define the graph by declaring the components and connecting them with arrows. 
The lambda arguments for the "run" methods provide the opportunity to carry out construction of the arguments that will be passed into the component functions, where:

* `log` is the Storm log factory
* `cfg` is the runtime configuration passed in from Storm 
* `tuple` is the instance of the schema DU coming in
* `emit` is the function to emit another tuple

`log` and `cfg` are fixed once (curried) and as demonstrated in the `logBolt`'s `mkArgs` lambda, one time-initialization can be carried out by inserting arbitrary code before `tuple` and `emit` arguments.
This initialization will not be triggered unless the task execution is actually requested by Storm for this specific instance of the process.


Submitting the topology for execution
--------------
Storm accepts JARs for code distribution and FsShelter provides functions to package our assemblies and upload them to Nimbus.
Once the code is uploaded, Storm needs to be told how to run it and FsShelter has functions that convert the above representation into that of Nimbus API.
Storm then starts the supervising processes across the cluster and spins up a copy of our executable for each task instance in our topology. 
FsShelter's `Task` will perform the handshake that will determine which component a given process instance has been assigned to execute:
*)
sampleTopology
|> Task.ofTopology
|> Task.run ProtoIO.start // here we specify which serializer to use when talking to Storm

(**

Then the execution will be handed over to one of the corresponding "dispatchers", which will handle the subsequent interaction between Storm and the component function.

Keep in mind that:

* STDIN/STDOUT are reserved for communications with Storm and any IO the component is going to do has to go through some other channel (no console logging!).
* out of the box Storm only supports JSON multilang. For faster IO, consider [ProtoShell](https://github.com/prolucid/protoshell). The serilizer JAR can be bundled along with the submitted topology or deployed in Storm's classpath beforehand.


Exporting the topology graph
--------------

FsShelter includes a completely customizable GraphViz (dot) export functionality. Here's what the word count topology looks like with default renderers:

![SVG](svg/WordCount.svg "WordCount (SVG)")

The dotted lines represent "unanchored" streams and the number inside the `[]` shows the parallelism hint.
This was achived by a simple export to console:
*)

sampleTopology |> DotGraph.writeToConsole

(**
Followed by further conversion into a desired format, piping the markup into GraphViz:

```bash
dotnet samples/WordCount/bin/Release/netcoreapp2.1/WordCount.dll graph | dot -Tsvg -o build/WordCount.svg
```

It is also possible to generate graphs with colour-coded streams:
*)

sampleTopology |> DotGraph.writeColourizedToConsole

(**
![SVG](svg/WordCount_colourized.svg "Colourized WordCount (SVG)")

Alternatively, you can provide your own X11 colour scheme:
*)

let customColours = [| "purple"; "dodgerblue"; "springgreen"; "olivedrab"; "orange"; "orangered"; "maroon"; "black" |]
sampleTopology
|> DotGraph.exportToDot (DotGraph.writeHeader, DotGraph.writeFooter, DotGraph.writeSpout, DotGraph.writeBolt, DotGraph.writeColourfulStream <| DotGraph.getColour customColours) System.Console.Out

(**
![SVG](svg/WordCount_custom_colourized.svg "Custom Colourized WordCount (SVG)")
*)