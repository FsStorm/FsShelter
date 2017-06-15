module FsShelter.HostTests

open NUnit.Framework
open Swensen.Unquote
open FsShelter.TestTopology
open System

module Topologies =
    open FsShelter.DSL
    open FsShelter.Multilang
    
    let printBolt (log,t) =
        match t with 
        | Tick -> log (sprintf "time: %A" DateTime.Now)
        | _ -> log (sprintf "tuple: %A" t)
        |> async.Return

    let t1 = topology "test" {
        let s1 = numbers
                 |> runReliableSpout (fun _ _ -> {rnd = Random(); count = ref 0L}) (fun _ -> ignore, ignore)
        let b1 = split
                 |> runBolt (fun log _ t emit -> (t,emit))
                 |> withParallelism 2
        let b2 = resultBolt
                 |> runBolt (fun log _ t _ -> (log LogLevel.Info),t)
        let b3 = printBolt
                 |> runBolt (fun log _ t _ -> (log LogLevel.Info),t)
                 |> withConf [Conf.TOPOLOGY_TICK_TUPLE_FREQ_SECS, box 1]

        yield s1 ==> b1 |> Shuffle.on Original
        yield s1 --> b3 |> Shuffle.on Original
        yield b1 --> b2 |> Group.by (function Odd(n,_) -> (n.x)) 
        yield b1 --> b2 |> Group.by (function Even(x,str) -> (x.x,str.str))
    }

[<Test>]
let ``Hosts``() = 
     let log taskId f = Diagnostics.Debug.WriteLine("{0}\t{1}: {2}",DateTime.Now,taskId,f())
     System.Diagnostics.Debug.WriteLine (sprintf "%A" Topologies.t1.Bolts)
     
     let stop = Topologies.t1 |> Host.runWith log 
     Threading.Thread.Sleep 5000
     stop()
