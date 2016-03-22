module FsShelter.TaskTests

open NUnit.Framework
open Swensen.Unquote
open FsShelter.TestTopology
open System

let tt = FsShelter.Task.ofTopology t1

[<Test>]
let ``spout is runnable``() = 
    tt "s1" |> ignore

[<Test>]
let ``bolts are runnable``() = 
    tt "b1" |> ignore
    tt "b2" |> ignore
