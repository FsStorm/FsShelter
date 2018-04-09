module FsShelter.SchemaTests

open NUnit.Framework
open Swensen.Unquote
open FsShelter.TestTopology
open System

[<Test>]
let ``schema inferred``() = 
    t1.Streams.[("s1","Original"),"b1"].Schema =! ["Item.x"]
    t1.Streams.[("b1","Odd"),"b2"].Schema =! ["Item1.x";"Item2"]

[<Test>]
let ``schema unfolds``() = 
    t1.Streams.[("b1","Even"),"b2"].Schema =! ["number.x";"str.str"]

[<Test>]
let ``schema unfolds only a level deep``() = 
    t2.Streams.[("s2","Nested"),"b3"].Schema =! ["Item.nested"; "Item.xs"; "Item.m"; "Item.gxs"; "Item.d"]

[<Test>]
let ``schema unfolds wide case``() = 
    t2.Streams.[("s2","JustFields"),"b3"].Schema =! ["Item1";"Item2";"Item3";"Item4";"Item5";"Item6";"Item7";"Item8";"Item9";"Item10"]

[<Test>]
let ``schema unfolds generics``() = 
    t4.Streams.[("s1","Outer"),"b1"].Schema =! ["Item"]
    t4.Streams.[("s1","Inner"),"b2"].Schema =! ["Item.x"]
    t4.Streams.[("s1","Inner"),"b3"].Schema =! ["number.x";"str.str"]

[<Test>]
let ``schema unfolds nested generics``() = 
    t5.Streams.[("s1","Outer"),"b1"].Schema =! ["Item"]
    t5.Streams.[("s1","Inner+Original"),"b2"].Schema =! ["Item.x"]
    t5.Streams.[("s1","Inner+Even"),"b3"].Schema =! ["number.x";"str.str"]

[<Test>]
let ``schema unfolds nested grouped generics``() = 
    t6.Streams.[("s1","Outer"),"b1"].Schema =! ["Item"]
    t6.Streams.[("s1","Inner+Original"),"b2"].Schema =! ["Item.x"]
    t6.Streams.[("s1","Inner+Even"),"b2"].Schema =! ["number.x";"str.str"]

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
let ``generic schema produces a tuple``() = 
    let (constr,deconst) = TupleSchema.mapSchema<GenericSchema<Schema>>() |> Map.ofArray |> Map.find "Inner"
    let mutable xs = []
    GenericSchema.Inner(Even({x=1},{str="a"})) |> deconst (box >> (fun v -> xs <- v::xs))
    xs =! [box (Even({x=1},{str="a"}))]

[<Test>]
let ``generic nested schema produces a tuple``() = 
    let (constr,deconst) = TupleSchema.mapSchema<GenericNestedSchema<Schema>>() |> Map.ofArray |> Map.find "Inner+Even"
    let mutable xs = []
    Inner(Even({x=1},{str="a"})) |> deconst (box >> (fun v -> xs <- v::xs))
    //(Even({x=1},{str="a"})) |> deconst (box >> (fun v -> xs <- v::xs))
    xs =! [box "a"; box 1]

[<Test>]
let ``schema reads a tuple``() = 
    let (constr,deconst) = TupleSchema.mapSchema<Schema>() |> Map.ofArray |> Map.find "Even"
    let f = constr (function | t when t = typeof<string> -> box "a" | t when t = typeof<int> -> box 1 | _ -> failwith "?")
    Even({x=1},{str="a"}) =! f()

[<Test>]
let ``schema mapping reflects generics``() = 
    TupleSchema.mapSchema<GenericSchema<Schema>>() |> Array.map fst |> List.ofArray =! ["Outer"; "Inner"]

[<Test>]
let ``schema mapping reflects nested generics``() = 
    let nested = TupleSchema.mapSchema<Schema>() |> Array.map (fst >> (fun s -> if s.Contains("tick") then s else sprintf "Inner+%s" s)) |> List.ofArray
    TupleSchema.mapSchema<GenericNestedSchema<Schema>>() |> Array.map fst |> List.ofArray =! "Outer" :: nested
    
[<Test>]
let ``schema reads a generic tuple``() = 
    let (constr,deconst) = TupleSchema.mapSchema<GenericSchema<Schema>>() |> Map.ofArray |> Map.find "Inner"
    let f = constr (function | t when t = typeof<Schema> -> box <| Even({x=1},{str="a"}) | _ -> failwith "?")
    GenericSchema.Inner(Even({x=1},{str="a"})) =! f()

[<Test>]
let ``schema reads a nested generic tuple``() = 
    let (constr,deconst) = TupleSchema.mapSchema<GenericNestedSchema<Schema>>() |> Map.ofArray |> Map.find "Inner+Even"
    let f = constr (function | t when t = typeof<string> -> box "a" | t when t = typeof<int> -> box 1 | _ -> failwith "?")
    GenericNestedSchema.Inner(Even({x=1},{str="a"})) =! f()

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