module FsShelter.ConfTests

open NUnit.Framework
open Swensen.Unquote
open FsShelter
open System

[<Test>]
let ``Defaults order``() = 
     Conf.ofList [ Conf.TOPOLOGY_MAX_SPOUT_PENDING, box 2]
     |> Conf.option Conf.TOPOLOGY_MAX_SPOUT_PENDING =! Some 2

[<Test>]
let ``Precedence``() = 
     let t = TestTopology.t2
             |> withConf [ Conf.TOPOLOGY_MAX_SPOUT_PENDING, box 2 ]
     t.Spouts.["s2"].Conf
     |> Map.join t.Conf
     |> Conf.option Conf.TOPOLOGY_MAX_SPOUT_PENDING =! Some 1

[<Test>]
let ``Defaults``() = 
     let t = TestTopology.t1
             |> withConf [ Conf.TOPOLOGY_MAX_SPOUT_PENDING, box 2 ]
     t.Spouts.["s1"].Conf
     |> Map.join t.Conf
     |> Conf.option Conf.TOPOLOGY_MAX_SPOUT_PENDING =! Some 2
