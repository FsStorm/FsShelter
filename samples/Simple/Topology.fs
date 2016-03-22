module Sample.Topology

open System

// data schema for the topology, every case is a unqiue stream
type Schema = 
    | Original of int * float * Guid * Guid* string * string * string * DateTime * DateTimeOffset * DateTimeOffset
    | Incremented of int * float * Guid * Guid * string * string * string * DateTime * DateTimeOffset * DateTimeOffset

// numbers spout - produces messages
let numbers source = async { return Some(Original(source())) }

// add 1 bolt - consumes and emits messages to either Even or Odd stream
let addOne (input, emit) = 
    async { 
        match input with
        | Original(x, f, g1, g2, str1, str2, str3, dt, dto1, dto2) -> 
            Incremented(x + 1, f, g1, g2, str1, str2, str3, dt, dto1, dto2)
        | _ -> failwithf "unexpected input: %A" input
        |> emit
    }

// terminating bolt - consumes messages
let logResult (info, input) = 
    async { 
        match input with
        | Incremented(x, f, g1, g2, str1, str2, str3, dt, dto1, dto2) -> 
            info ((sprintf "%A" [ box x
                                  box f
                                  box g1
                                  box g2
                                  box str1
                                  box str2
                                  box str3
                                  box dt
                                  box dto1
                                  box dto2 ]).GetHashCode().ToString())
        | _ -> failwithf "unexpected input: %A" input
    }

// data source
open System

let source = 
    let rnd = Random()
    fun () -> rnd.Next(0, 100),
              rnd.NextDouble(),
              Guid.NewGuid(),
              Guid.NewGuid(),
              String.replicate (rnd.Next(0, 100)) "a",
              String.replicate (rnd.Next(0, 100)) "b",
              String.replicate (rnd.Next(0, 100)) "c",
              DateTime.Now,
              DateTimeOffset.Now,
              DateTimeOffset.Now

//define the storm topology
open FsShelter.DSL

#nowarn "25" // for stream matching expressions

let sampleTopology = 
    topology "Sample" { 
        let s1 = numbers |> runUnreliably (fun log cfg -> source) // ignoring Storm logging and cfg
        
        let b1 = 
            addOne
            |> runBolt (fun log cfg tuple emit -> (tuple, emit)) // pass incoming tuple and emit function
            |> withParallelism 2
        
        let b2 = 
            logResult
             |> runBolt (fun log cfg ->
                            let mylog = Common.Logging.asyncLog (System.Diagnostics.Process.GetCurrentProcess().Id.ToString()+"_sample.log")
                            fun tuple emit -> (mylog,tuple)) // example of passing Info-level logger into the bolt
            |> withParallelism 2
        
        yield s1 --> b1 |> shuffle.on Original // emit from s1 to b1 on Original stream
        yield b1 --> b2 |> shuffle.on Incremented // emit from b1 to b2 on Incremented stream
    }
