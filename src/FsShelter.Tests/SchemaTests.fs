module FsShelter.SchemaTests

open NUnit.Framework
open Swensen.Unquote
open FsShelter.TestTopology
open System

[<Test>]
let ``schema inferred``() = 
    t1.Streams.["Odd"].Schema =! ["Item1.x";"Item2"]

[<Test>]
let ``schema unfolds``() = 
    t1.Streams.["Even"].Schema =! ["number.x";"str.str"]

[<Test>]
let ``schema unfolds only a level deep``() = 
    t2.Streams.["Nested"].Schema =! ["Item.nested"; "Item.xs"; "Item.m"; "Item.gxs"; "Item.d"]

[<Test>]
let ``schema unfolds wide case``() = 
    t2.Streams.["JustFields"].Schema =! ["Item1";"Item2";"Item3";"Item4";"Item5";"Item6";"Item7";"Item8";"Item9";"Item10"]

[<Test>]
let ``schema mapping respects displayName``() = 
    do TupleSchema.mapSchema<Schema>() |> Map.ofArray |> Map.find "__tick" |> ignore

[<Test>]
let ``schema produces a tuple``() = 
    let (constr,deconst) = TupleSchema.mapSchema<Schema>() |> Map.ofArray |> Map.find "Even"
    let mutable xs = []
    Even({x=1},{str="a"}) |> deconst (box >> (fun v -> xs <- v::xs))
    xs =! [box "a"; box 1]

[<Test>]
let ``schema reads a tuple``() = 
    let (constr,deconst) = TupleSchema.mapSchema<Schema>() |> Map.ofArray |> Map.find "Even"
    let mutable xs = [box "a"; box 1]
    let f = constr (function | t when t = typeof<string> -> box "a" | t when t = typeof<int> -> box 1 | _ -> failwith "?")
    Even({x=1},{str="a"}) =! f()
//
//[<Test>]
//let ``schema produces tuples fast``() = 
//    let f = deconst (string >> ignore)
//    let t = Even({x=1},{str="a"})
//    let timer = System.Diagnostics.Stopwatch.StartNew()
//    for _ in 1..100000 do
//        f t
//    timer.Stop()
//    printf "Serialized @ %A tuples/ms" (100000L/timer.ElapsedMilliseconds)
    