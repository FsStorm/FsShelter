(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use 
// it to define helpers that you do not want to show in the documentation.
#I "../../build"
#r "FsShelter.dll"

open FsShelter
open System
open System.Collections.Generic

(**
Defining reliable spouts
--------------------
[Processing guarantees](https://storm.apache.org/documentation/Guaranteeing-message-processing.html) are the biggest selling point of Storm, please see the official docs for the details.
The reliable spout implementation for a source like peristent a queue (RabbitMQ, Kafka, etc) needs to obtain the event id from the source and forward Storm's acks and nacks back to the source.
The obtained Id has to be passed along with the tuple from the spout function:
*)

// data schema for the topology, every case is a unqiue stream
type Schema = 
    | Original of int
    | Even of int
    | Odd of int

// numbers spout - produces messages
let numbers source =
    async { 
        let! (tupleId,number) = source()
        return Some(tupleId, Original (number)) 
    }

// add 1 bolt - consumes and emits messages to either Even or Odd stream
let addOne (input,emit) =
    async { 
        match input with
        | Original x -> 
            match x % 2 with
            | 1 -> Even (x+1)
            | _ -> Odd (x+1)
        | _ -> failwithf "unexpected input: %A" input
        |> emit
    }

// terminating bolt - consumes messages
let logResult (info,input) =
    async { 
        match input with
        | Even x
        | Odd x -> info (sprintf "Got: %A" input)
        | _ -> failwithf "unexpected input: %A" input
    }

(**
Here we mimic an external source and implement all three possible cases: produce a new message, retry a failed one (indefinetely) and ack a successfully processed.
*)
open FsShelter.Topology

type QueueCmd =
    | Get of AsyncReplyChannel<TupleId*int>
    | Ack of TupleId
    | Nack of TupleId

// faking an external source here
let source = 
    let rnd = Random()
    let count = ref 0L
    let pending = Dictionary()
    let nextId() = Threading.Interlocked.Increment &count.contents

    MailboxProcessor.Start (fun inbox -> 
        let rec loop nacked = 
            async { 
                let! cmd = inbox.Receive()
                return! loop <|
                       match cmd, nacked with
                       | Get rc, [] ->
                            let tupleId,number = string(nextId()), rnd.Next(0, 100)
                            pending.Add(tupleId,number)
                            rc.Reply(tupleId,number)
                            []
                       | Get rc,(tupleId,number)::xs ->
                            pending.Add(tupleId,number)
                            rc.Reply (tupleId,number)
                            xs
                       | Ack id, _ -> 
                            pending.Remove id |> ignore
                            nacked
                       | Nack id, _ -> 
                            (id,pending.[id])::nacked
            }
        loop [])

(**
Anchoring 
--------------------
In order to provide processing guarantees Storm needs to construct and track the state of entire "tuple tree", which is built out by emitting "anchored" tuples.
FsShelter implements anchoring statically: instead of ad-hoc, as determined by a component, it is a property of the stream _leading to_ an emit. 
Consequently the implementation of emit (anchored/unanchored) is determined by the topology graph and completely transparent to the bolt that processes a tuple that will be used as an anchor. 

*)

//define the storm topology
open FsShelter.DSL

#nowarn "25" // for stream matching expressions
let sampleTopology = topology "Guaranteed" {
    let s1 = numbers
             |> runReliableSpout (fun log cfg () -> source.PostAndAsyncReply Get)  // ignoring logging and cfg available
                                 (fun _ -> Ack >> source.Post, Nack >> source.Post)
    let b1 = addOne
             |> runBolt (fun log cfg tuple emit -> (tuple,emit)) // pass incoming tuple and emit function
             |> withParallelism 2
    
    let b2 = logResult
             |> runBolt (fun log cfg ->
                            let mylog = Common.Logging.asyncLog ("odd.log")
                            fun tuple emit -> (mylog,tuple))
             |> withParallelism 1

    let b3 = logResult
             |> runBolt (fun log cfg -> 
                            let mylog = Common.Logging.asyncLog ("even.log") 
                            fun tuple emit -> (mylog,tuple))
             |> withParallelism 1

    yield s1 ==> b1 |> shuffle.on Original  // emit from s1 to b1 on Original stream and anchor immediately following emits to this tuple
    yield b1 --> b2 |> shuffle.on Odd       // anchored emit from b1 to b2 on Odd stream 
    yield b1 --> b3 |> shuffle.on Even      // anchored emit from b1 to b2 on Even stream 
}

(**
Resulting topology graph:

![SVG](svg/Guaranteed.svg "Guaranteed (SVG)")

The solid lines represent "anchoring" streams and the dotted lines indicate the outer limits of the processing guarantees: a tuple emitted along a dotted line is only anchored if the line leading to it is solid.
*)
