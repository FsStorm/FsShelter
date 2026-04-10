## Guaranteed Delivery

Reliable message processing ensures every tuple emitted by a spout is either fully processed through the entire DAG or retried on failure. This tutorial shows how to implement a spout backed by an external queue with ack/nack callbacks.

For background on how this works internally, see [Message Flow](message-flow.html) and [Acker Algorithm](acker-algorithm.html).

## Defining reliable spouts

Reliable spouts need to obtain an event ID from the source and forward acks/nacks back — so the source knows whether to commit or re-enqueue.
The obtained Id has to be passed along with the tuple from the spout function:

```fsharp
// data schema for the topology, every case is a unqiue stream
type Schema = 
    | Original of int
    | Even of int
    | Odd of int

// numbers spout - produces messages
let numbers source =
    let (tupleId,number) = source()
    Some(tupleId, Original (number)) 

// add 1 bolt - consumes and emits messages to either Even or Odd stream
let addOne (input,emit) =
    match input with
    | Original x -> 
        match x % 2 with
        | 1 -> Even (x+1)
        | _ -> Odd (x+1)
    | _ -> failwithf "unexpected input: %A" input
    |> emit

// terminating bolt - consumes messages
let logResult (info,input) =
    match input with
    | Even x
    | Odd x -> info (sprintf "Got: %A" input)
    | _ -> failwithf "unexpected input: %A" input
```

Here we mimic an external source and implement all three possible cases: produce a new message, retry a failed one (indefinetely) and ack a successfully processed.

```fsharp
open FsShelter.Topology

type QueueCmd =
    | Get of AsyncReplyChannel<TupleId*int>
    | Ack of TupleId
    | Nack of TupleId

// faking an external source here
let source = 
    let rnd = Random()
    let count = ref 0L
    let pending = Dictionary<TupleId,int>()
    let nextId() = Threading.Interlocked.Increment &count.contents

    MailboxProcessor.Start (fun inbox -> 
        let rec loop nacked = 
            async { 
                let! cmd = inbox.Receive()
                return! loop <|
                       match cmd, nacked with
                       | Get rc, [] ->
                            let tupleId,number = TupleId.ofString(string(nextId())), rnd.Next(0, 100)
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
```

## Anchoring

In order to provide processing guarantees Storm needs to construct and track the state of entire "tuple tree", which is built out by emitting "anchored" tuples.
FsShelter implements anchoring statically: instead of ad-hoc, as determined by a component, it is a property of the stream **leading to** an emit.
Consequently the implementation of emit (anchored/unanchored) is determined by the topology graph and completely transparent to the bolt that processes a tuple that will be used as an anchor.

```fsharp
//define the storm topology
open FsShelter.DSL

#nowarn "25" // for stream matching expressions
let sampleTopology = topology "Guaranteed" {
    let s1 = numbers
             |> Spout.runReliable (fun log cfg () -> source.PostAndReply Get)  // ignoring logging and cfg available
                                  (fun _ -> Ack >> source.Post, Nack >> source.Post)
                                  ignore                                       // no deactivation
    let b1 = addOne
             |> Bolt.run (fun log cfg tuple emit -> (tuple,emit)) // pass incoming tuple and emit function
             |> withParallelism 2
    
    let b2 = logResult
             |> Bolt.run (fun log cfg ->
                            let mylog text = log LogLevel.Info text
                            fun tuple emit -> (mylog,tuple))
             |> withParallelism 1

    let b3 = logResult
             |> Bolt.run (fun log cfg -> 
                            let mylog text = log LogLevel.Info text
                            fun tuple emit -> (mylog,tuple))
             |> withParallelism 1

    yield s1 ==> b1 |> Shuffle.on Original  // emit from s1 to b1 on Original stream and anchor immediately following emits to this tuple
    yield b1 --> b2 |> Shuffle.on Odd       // anchored emit from b1 to b2 on Odd stream 
    yield b1 --> b3 |> Shuffle.on Even      // anchored emit from b1 to b2 on Even stream 
}
```

Resulting topology graph:

![SVG](svg/Guaranteed.svg)

The solid lines represent "anchoring" streams and the dotted lines indicate the outer limits of the processing guarantees: a tuple emitted along a dotted line is only anchored if the line leading to it is solid.

For a detailed walkthrough of the XOR-tree tracking that powers this, see [Acker Algorithm](acker-algorithm.html). For end-to-end message flow scenarios including backpressure and timeouts, see [Message Flow](message-flow.html).
