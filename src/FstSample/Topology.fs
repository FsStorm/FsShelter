module FstSample.Topology

// data schema for the topology, every case is a unqiue stream
type Schema = 
    | Original of int
    | Incremented of int

// numbers spout - produces messages
let numbers source =
    async { 
        return Some(Original (source())) 
    }

// add 1 bolt - consumes and emits messages to either Even or Odd stream
let addOne (input,emit) =
    async { 
        match input with
        | Original x -> Incremented (x+1)
        | _ -> failwithf "unexpected input: %A" input
        |> emit
    }

// terminating bolt - consumes messages
let logResult (info,input) =
    async { 
        match input with
        | Incremented x -> info (sprintf "Got: %A" input)
        | _ -> failwithf "unexpected input: %A" input
    }

// data source
open System
let source = 
    let rnd = Random()
    fun () -> rnd.Next(0, 100)

//define the storm topology
open Storm.DSL

#nowarn "25" // for stream matching expressions
let sampleTopology = topology "FstSample" {
    let s1 = numbers
             |> runUnreliably (fun log cfg -> source)  // ignoring logging and cfg available
    let b1 = addOne
             |> runBolt (fun log cfg tuple emit -> (tuple,emit)) // pass incoming tuple and emit function
             |> withParallelism 2
    let b2 = logResult
             |> runBolt (fun log cfg tuple emit -> ((log Storm.Multilang.LogLevel.Info),tuple)) // example of passing Info-level Storm logger into the bolt
             |> withParallelism 2

    yield s1 --> b1 |> shuffle.on Original // emit from s1 to b1 on Original stream
    yield b1 --> b2 |> shuffle.on Incremented  // emit from b1 to b2 on Incremented stream
}
