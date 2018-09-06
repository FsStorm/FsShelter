module FsShelter.ConfTests

open NUnit.Framework
open Swensen.Unquote
open FsShelter
open System

[<Test>]
let ``Defaults order``() = 
     Conf.ofList [ ConfOption.TOPOLOGY_MAX_SPOUT_PENDING 2]
     |> Conf.option ConfOption.TOPOLOGY_MAX_SPOUT_PENDING =! Some 2

[<Test>]
let ``Precedence``() = 
     let t = TestTopology.t2
             |> withConf [ ConfOption.TOPOLOGY_MAX_SPOUT_PENDING 2 ]
     t.Spouts.["s2"].Conf
     |> Map.join t.Conf
     |> Conf.option ConfOption.TOPOLOGY_MAX_SPOUT_PENDING =! Some 1

[<Test>]
let ``Defaults``() = 
     let t = TestTopology.t1
             |> withConf [ ConfOption.TOPOLOGY_MAX_SPOUT_PENDING 2 ]
     t.Spouts.["s1"].Conf
     |> Map.join t.Conf
     |> Conf.option ConfOption.TOPOLOGY_MAX_SPOUT_PENDING =! Some 2
