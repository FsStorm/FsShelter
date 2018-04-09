﻿module FsShelter.DSLTests

open NUnit.Framework
open Swensen.Unquote
open FsShelter.TestTopology
open System

[<Test>]
let ``spouts defined``() = 
    test <@ t1.Spouts |> Map.toList |> List.forall2 (fun sid2 (sid1,_) -> sid1 = sid2) ["s1"] @>

[<Test>]
let ``bolts defined``() = 
    test <@ t1.Bolts |> Map.toList |> List.forall2 (fun sid2 (sid1,_) -> sid1 = sid2) ["b1";"b2"] @>

[<Test>]
let ``streams defined``() = 
    test <@ t1.Streams |> Map.toList |> List.map fst |> List.sort |> List.forall2 (fun sid2 sid1 -> (fst sid1) = sid2) [("b1","Even");("b1","Odd");("s1","Original")] @>

[<Test>]
let ``generic streams defined``() = 
    test <@ t4.Streams |> Map.toList |> List.map fst |> List.sort |> List.forall2 (fun sid2 sid1 -> (fst sid1) = sid2) [("s1","Inner");("s1","Inner");("s1","Outer")] @>
    
[<Test>]
let ``nested generic streams defined``() = 
    test <@ t5.Streams |> Map.toList |> List.map fst |> List.sort |> List.forall2 (fun sid2 sid1 -> (fst sid1) = sid2) [("s1","Inner+Even");("s1","Inner+Original");("s1","Outer")] @>
    
[<Test>]
let ``nested generic grouped streams defined``() = 
    test <@ t6.Streams |> Map.toList |> List.map fst |> List.sort |> List.forall2 (fun sid2 sid1 -> (fst sid1) = sid2) [("b1","Inner+Odd");("s1","Inner+Even");("s1","Inner+Original");("s1","Outer")] @>

