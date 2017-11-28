module FsShelter.TestTopology


//defines the spout and bolt 
open System
open System.Collections.Generic
open Hopac

type Number = 
    { x : int32 }

type String = 
    { str : string }

type Nested = {nested:String; xs:Number list; m:Map<int,Number option>; gxs:List<Number>; d:Dictionary<string,Number option>}

type Schema = 
    | Original of Number
    | Even of number:Number*str:String
    | Odd of Number*string
    | MaybeString of string option
    | NullableGuid of Guid Nullable
    | Nested of Nested
    | JustFields of int * float * Guid * Guid * string * string * string * DateTime * DateTimeOffset * DateTimeOffset
    | [<ComponentModel.DisplayName("__tick")>] Tick

type World = 
    { rnd : Random
      count : int64 ref }

/// numbers spout - produces messages
let numbers (world : World) =
    job { 
        return Some(string(Threading.Interlocked.Increment &world.count.contents), Original { x = world.rnd.Next(0, 100) }) 
    }

/// split bolt - consumes and emits messages
let split (input,(emit:_->Job<_>)) =
    job { 
        do! match input with
            | Original { x = x } -> 
                match x % 2 with
                | 0 -> Even ({x=x}, {str="even"})
                | _ -> Odd ({x=x}, "odd" )
            | _ -> failwithf "unexpected input: %A" input
            |> emit
    }

/// terminating bolt - consumes messages
let resultBolt (info,input) =
    job { 
        match input with
        | Even ({x = x}, {str=str})
        | Odd ({x = x},str) -> info (sprintf "Got %A" input)
        | _ -> failwithf "unexpected input: %A" input
    }

open FsShelter.DSL

#nowarn "25" // for stream matching expressions
let t1 = topology "test" {
    let s1 = numbers
             |> runReliableSpout (fun _ _ -> {rnd = Random(); count = ref 0L}) (fun _ -> ignore, ignore)
    let b1 = split
             |> runBolt (fun _ _ t emit -> (t,emit))
             |> withParallelism 2
    let b2 = resultBolt
             |> runBolt (fun _ _ t _ -> ignore,t)
    yield s1 ==> b1 |> Shuffle.on Original
    yield b1 --> b2 |> Group.by (function Odd(n,_) -> (n.x)) 
    yield b1 --> b2 |> Group.by (function Even(x,str) -> (x.x,str.str))
}

let t2 = topology "test2" {
    let s2 = shell "cmd" ""
             |> asSpout<Schema>
             |> withConf [Conf.TOPOLOGY_MAX_SPOUT_PENDING, 1]
    let b3 = java "class" ["args"]
             |> asBolt<Schema>
             |> withConf []
    yield s2 --> b3 |> All.on Nested
    yield s2 --> b3 |> All.on JustFields
}

let t3 = topology "test3" {
    let s1 = shell "cmd" ""
             |> asSpout<Schema>
             |> withConf []
    let b1 = java "class" ["args"]
             |> asBolt<Schema>
             |> withConf []
    let b2 = java "class" ["args"]
             |> asBolt<Schema>
             |> withConf []
    yield s1 --> b1 |> Shuffle.on Original
    yield s1 --> b2 |> Shuffle.on Original
}
