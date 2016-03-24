module Guaranteed.Topology

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

/// In-memory "reliable" queue implementation
open FsShelter.Topology
open System
open System.Collections.Generic

type QueueCmd =
    | Get of AsyncReplyChannel<TupleId*int>
    | Ack of TupleId
    | Nack of TupleId

// faking an external source here
let source = 
    let rnd = Random()
    let count = ref 0L
    let pending = Dictionary()

    MailboxProcessor.Start (fun inbox -> 
        let rec loop nacked = 
            async { 
                let! cmd = inbox.Receive()
                return! loop <| match cmd, nacked with
                                | Get rc, [] ->
                                    let tupleId,number = string(Threading.Interlocked.Increment &count.contents), rnd.Next(0, 100)
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

    yield s1 ==> b1 |> shuffle.on Original // emit from s1 to b1 on Original stream and anchor immediately following emits to this tuple
    yield b1 --> b2 |> shuffle.on Odd // anchored emit from b1 to b2 on Odd stream 
    yield b1 --> b3 |> shuffle.on Even // anchored emit from b1 to b2 on Even stream 
}
