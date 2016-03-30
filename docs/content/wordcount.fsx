(*** hide ***)
#I "../../build"
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
    | WordCount of string*int64

(**
Defining unreliable spouts
--------------------

FsShelter spouts can be implemented as "reliable" or "unreliable". 
Either implementation is a single function, returning async Option, where None indicates there's nothing to emit from this spout at this moment:

*)

// sentences spout - feeds messages into the topology
let sentences source = async { return source() |> Sentence |> Some }

(**
Defining bolts
--------------------

A couple of examples of a FsShelter bolts that read a tuple and emit another one:

*)

// split bolt - consumes sentences and emits words
let splitIntoWords (input, emit) = 
    async { 
        match input with
        | Sentence s -> s.Split([|' '|],StringSplitOptions.RemoveEmptyEntries) 
                        |> Seq.map Word 
                        |> Seq.iter emit
        | _ -> failwithf "unexpected input: %A" input
    }

// count words bolt 
let countWords (input, increment, emit) = 
    async { 
        match input with
        | Word word -> 
            let! count = increment word
            WordCount (word,count) |> emit
        | _ -> failwithf "unexpected input: %A" input
    }

(**
And a terminating bolt that reads a tuple, but doesn't emit anything:

*)

// log word count - terminating bolt 
let logResult (log, input) = 
    async { 
        match input with
        | WordCount (word,count) -> log (sprintf "%s: %d" word count)
        | _ -> failwithf "unexpected input: %A" input
    }


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

let increment =
    let mkCountAgent () = 
        let cache = Collections.Generic.Dictionary()
        MailboxProcessor.Start(fun inbox -> 
            async { 
                while true do
                    let! (rc:AsyncReplyChannel<int64>,word) = inbox.Receive()
                    match cache.TryGetValue(word) with
                    | (true,count) -> cache.[word] <- count + 1L
                                      count + 1L
                    | _ -> cache.[word] <- 1L
                           1L
                    |> rc.Reply
            })
    let agentsNumber = 10
    let countAgents = seq {for i in 1..agentsNumber -> mkCountAgent()} |> Seq.toArray
    fun word -> 
        let agent = countAgents.[abs (word.GetHashCode() % agentsNumber)]
        agent.PostAndAsyncReply(fun rc -> rc,word)

(**


Using F# DSL to define a topology
--------------------

A typical (event-streaming) Storm topology is a graph of spouts and bolts connected via streams that can be defined via `topology` computation expression:
*)

open FsShelter.DSL
#nowarn "25" // for stream grouping expressions

//define the storm topology
let sampleTopology = 
    topology "WordCount" { 
        let sentencesSpout = 
            sentences |> runSpout (fun log cfg -> source)        // make arguments: ignoring Storm logging and cfg, passing our source function
        
        let splitBolt = 
            splitIntoWords
            |> runBolt (fun log cfg tuple emit -> (tuple, emit)) // make arguments: pass incoming tuple and emit function
            |> withParallelism 2
        
        let countBolt = 
            countWords
            |> runBolt (fun log cfg tuple emit -> (tuple, increment, emit))
            |> withParallelism 2
        
        let logBolt = 
            logResult
            |> runBolt (fun log cfg ->                           // make arguments: pass PID-log and incoming tuple 
                            let mylog = Common.Logging.asyncLog (Diagnostics.Process.GetCurrentProcess().Id.ToString()+"_count")
                            fun tuple emit -> (mylog, tuple))
            |> withParallelism 2
        
        yield sentencesSpout --> splitBolt |> shuffle.on Sentence               // emit from sentencesSpout to splitBolt on Sentence stream, shuffle among target task instances
        yield splitBolt --> countBolt |> group.by (function Word w -> w)        // emit from splitBolt into countBolt on Word stream, group by word (into the same task instance)
        yield countBolt --> logBolt |> group.by (function WordCount (w,c) -> w) // emit from countBolt into logBolt on WordCount stream, group by word value
    }

(**
Here we define the graph by declaring the components and connecting them with arrows. 
The lambda arguments for the "run" methods privde the opportunity to carry out construction of the arguments that will be passed into the component functions, where:

* `log` is the Storm log factory
* `cfg` is the runtime configuration passed in from Storm 
* `tuple` is the instance of the schema DU coming in
* `emit` is the function to emit another tuple

`log` and `cfg` are fixed once (curried) and as demonstrated in the logBolt's mkArgs lambda, one time-initialization can be carried out by inserting arbitrary code before `tuple` and `emit` arguments.
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
* out of the box Storm only supports JSON multilang, for faster IO consider [ProtoShell](https://github.com/prolucid/protoshell) or [ThriftShell](https://github.com/prolucid/thriftshell). The serilizer JAR can be bundled along with the submitted topology or deployed in Storm's classpath beforehand.


Exporting the topology graph
--------------

FsShelter includes a completely customizable GraphViz (dot) export functionality, here's what the word count topology looks like with default renderers:

![SVG](svg/WordCount.svg "WordCount (SVG)")

The dotted lines represent "unanchored" streams and the number inside the `[]` shows the parallelism hint.
This was achived by a simple export to console:
*)

sampleTopology |> DotGraph.writeToConsole

(**
Followed by further conversion into a desired format, piping the markup into GrapViz:

```bash
mono samples/WordCount/bin/Release/WordCount.exe graph | dot -Tsvg -o build/WordCount.svg
```
*)


