module FstGuaranteed.Topology

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
            | 0 -> Even (x+1)
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
open Storm.Topology
open System

type QueueCmd =
    | Get of AsyncReplyChannel<TupleId*int>
    | Ack of TupleId
    | Nack of TupleId

// faking an external source here
let source = 
    let rnd = Random()
    let count = ref 0L

    MailboxProcessor.Start (fun inbox -> 
        let rec loop pending nacked = 
            async { 
                let! cmd = inbox.Receive()
                return! loop 
                    <|| match cmd,nacked with
                        | Get rc,[] ->
                            let tupleId,number = string(Threading.Interlocked.Increment &count.contents), rnd.Next(0, 100)
                            rc.Reply (tupleId,number)
                            [tupleId,number],[]
                        | Get rc,(tupleId,number)::remaining ->
                            rc.Reply (tupleId,number)
                            pending,remaining
                        | Ack id, _ -> 
                            pending |> List.filter (fun (tupleId,_) -> tupleId <> id), nacked |> List.filter (fun (tupleId,_) -> tupleId <> id)
                        | Nack id, _ -> 
                            pending, (pending |> List.find (fun (tupleId,_) -> tupleId = id))::nacked
            }
        loop [] [])

//define the storm topology
open Storm.DSL

#nowarn "25" // for stream matching expressions
let sampleTopology = topology "FstGuaranteed" {
    let s1 = numbers
             |> runReliably (fun log cfg () -> source.PostAndAsyncReply(fun rc -> Get(rc)))  // ignoring logging and cfg available
                            (fun _ -> Ack >> source.Post, Nack >> source.Post)
    let b1 = addOne
             |> runBolt (fun log cfg tuple emit -> (tuple,emit)) // pass incoming tuple and emit function
             |> withParallelism 2
    
    let b2 = logResult
             |> runBolt (fun log cfg tuple emit -> ((log Storm.Multilang.LogLevel.Info),tuple)) // example of passing Info-level logger into the bolt
             |> withParallelism 2

    yield s1 ==> b1 |> shuffle.on Original // emit from s1 to b1 on Original stream and anchor immediately following emits to this tree
    yield b1 --> b2 |> group.by (function Odd n -> n) // emit from b1 to b2 on Odd stream and group tuples by n
    yield b1 --> b2 |> group.by (function Even n -> n) // emit from b1 to b2 on Even stream and group tuples by n
}
