module FsShelter.TaskTests

open NUnit.Framework
open Swensen.Unquote
open FsShelter.TestTopology
open FsShelter.Multilang
open System

let tt = FsShelter.Task.ofTopology t1

[<Test>]
let ``spout is runnable``() = 
    tt "s1" |> ignore

[<Test>]
let ``bolts are runnable``() = 
    tt "b1" |> ignore
    tt "b2" |> ignore

let ttAsync = FsShelter.Task.ofTopology t1Async

[<Test>]
let ``async bolt is runnable``() =
    ttAsync "b2" |> ignore

let ttAsyncTerminator = FsShelter.Task.ofTopology t1AsyncTerminator

[<Test>]
let ``async terminator bolt is runnable``() =
    ttAsyncTerminator "b2" |> ignore

let ttAsyncSpout = FsShelter.Task.ofTopology t1AsyncSpout

[<Test>]
let ``async reliable spout is runnable``() =
    ttAsyncSpout "s1" |> ignore

let ttAsyncUnreliableSpout = FsShelter.Task.ofTopology t1AsyncUnreliableSpout

[<Test>]
let ``async unreliable spout is runnable``() =
    ttAsyncUnreliableSpout "s1" |> ignore


