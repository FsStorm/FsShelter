module Storm.DSLTests

open NUnit.Framework
open Swensen.Unquote
open Storm.TestTopology
open System

[<Test>]
let ``spouts defined``() = 
    test <@ t1.Spouts |> Map.toList |> List.forall2 (fun sid2 (sid1,_) -> sid1 = sid2) ["s1"] @>

[<Test>]
let ``bolts defined``() = 
    test <@ t1.Bolts |> Map.toList |> List.forall2 (fun sid2 (sid1,_) -> sid1 = sid2) ["b1";"b2"] @>

[<Test>]
let ``streams defined``() = 
    test <@ t1.Streams |> Map.toList |> List.map fst |> List.sort |> List.forall2 (fun sid2 sid1 -> sid1 = sid2) ["Even";"Odd";"Original"] @>
    
