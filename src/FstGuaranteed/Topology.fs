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
open Storm.Topology
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

let asynclog (name:string) = 
    let writer = new IO.StreamWriter(
                    new IO.FileStream(IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),sprintf "logs/%s" name), 
                                      IO.FileMode.Create, 
                                      IO.FileAccess.Write, 
                                      IO.FileShare.Read))
    let mb = MailboxProcessor.Start (fun inbox -> 
                async { 
                    while true do
                        let! (text:string) = inbox.Receive()
                        text |> (sprintf "%s %s" (DateTime.Now.ToString("HH:mm:ss.ff"))) |> writer.WriteLine
                })
    mb.Post

//define the storm topology
open Storm.DSL

#nowarn "25" // for stream matching expressions
let sampleTopology = topology "FstGuaranteed" {
    let s1 = numbers
             |> runReliably (fun log cfg () -> source.PostAndAsyncReply Get)  // ignoring logging and cfg available
                            (fun _ -> Ack >> source.Post, Nack >> source.Post)
    let b1 = addOne
             |> runBolt (fun log cfg tuple emit -> (tuple,emit)) // pass incoming tuple and emit function
             |> withParallelism 2
    
    let b2 = logResult
             |> runBolt (fun log cfg ->
                            let mylog = asynclog ("odd.log")
                            fun tuple emit -> (mylog,tuple)) // example of passing Info-level logger into the bolt
             |> withParallelism 1

    let b3 = logResult
             |> runBolt (fun log cfg -> 
                            let mylog = asynclog ("even.log") 
                            fun tuple emit -> (mylog,tuple)) // example of passing Info-level logger into the bolt
             |> withParallelism 1

    yield s1 ==> b1 |> shuffle.on Original // emit from s1 to b1 on Original stream and anchor immediately following emits to this tree
    yield b1 --> b2 |> shuffle.on Odd //(function Odd n -> n) // emit from b1 to b2 on Odd stream and group tuples by n
    yield b1 --> b3 |> shuffle.on Even //(function Even n -> n) // emit from b1 to b2 on Even stream and group tuples by n
}
