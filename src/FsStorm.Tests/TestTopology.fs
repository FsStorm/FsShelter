module Storm.TestTopology


//defines the spout and bolt 
open System
open System.Collections.Generic

type Number = 
    { x : int32 }

type String = 
    { str : string }

type Nested = {nested:String}

type Schema = 
    | Original of Number
    | Even of number:Number*str:String
    | Odd of Number*string
    | MaybeString of string option
    | Nested of Nested

type World = 
    { rnd : Random
      count : int64 ref }

/// numbers spout - produces messages
let numbers (world : World) =
    async { 
        return Some(string(Threading.Interlocked.Increment &world.count.contents), Original { x = world.rnd.Next(0, 100) }) 
    }

/// split bolt - consumes and emits messages
let split (input,emit) =
    async { 
        match input with
        | Original { x = x } -> 
            match x % 2 with
            | 0 -> Even ({x=x}, {str="even"})
            | _ -> Odd ({x=x}, "odd" )
        | _ -> failwithf "unexpected input: %A" input
        |> emit
    }

/// terminating bolt - consumes messages
let resultBolt (info,input) =
    async { 
        match input with
        | Even ({x = x}, {str=str})
        | Odd ({x = x},str) -> info (sprintf "Got %A" input)
        | _ -> failwithf "unexpected input: %A" input
    }

open Storm.DSL

#nowarn "25" // for stream matching expressions
let t1 = topology "test" {
    let s1 = numbers
             |> runReliably (fun _ _ -> {rnd = Random(); count = ref 0L}) (fun _ -> ignore, ignore)
    let b1 = split
             |> runBolt (fun _ _ t emit -> (t,emit))
             |> withParallelism 2
    let b2 = resultBolt
             |> runBolt (fun _ _ t _ -> ignore,t)
    yield s1 ==> b1 |> shuffle.on Original
    yield b1 --> b2 |> group.by (function Odd(n,_) -> (n.x)) 
    yield b1 --> b2 |> group.by (function Even(x,str) -> (x.x,str.str))
}

let t2 = topology "test2" {
    let s2 = shell "cmd" ""
             |> asSpout<Schema>
             |> withConf []
    let b3 = java "class" ["args"]
             |> asBolt<Schema>
             |> withConf []
    yield s2 --> b3 |> all.on Nested
}
