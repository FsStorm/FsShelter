module FsShelter.HostTests

open NUnit.Framework
open Swensen.Unquote
open FsShelter.TestTopology
open System

module Topologies =
    open FsShelter.DSL
    open FsShelter.Multilang
    open System.Threading

    type World = 
        { rnd : Random
          count : int64 ref
          acked: int64 ref }

    /// numbers spout - produces messages
    let numbers (world : World) =
        async { 
            return Some(string(Interlocked.Increment &world.count.contents), Original { x = world.rnd.Next(0, 100) }) 
        }
    
    let printBolt (log,t) =
        match t with 
        | Tick -> log (sprintf "time: %A" DateTime.Now)
        | _ -> () //log (sprintf "tuple: %A" t)
        |> async.Return

    let world = {rnd = Random(); count = ref 0L; acked = ref 0L}
    let t1 = topology "test" {
        let s1 = numbers
                 |> runReliableSpout (fun log _ -> world ) (fun w -> (fun _ -> Interlocked.Increment &w.acked.contents |> ignore), ignore)
        let b1 = split
                 |> runBolt (fun log _ t emit -> (t,emit))
                 |> withParallelism 2
        let b2 = printBolt
                 |> runBolt (fun log _ t _ -> (log LogLevel.Info),t)
                 //|> withConf [Conf.TOPOLOGY_TICK_TUPLE_FREQ_SECS, 1]

        yield s1 ==> b1 |> Shuffle.on Original
        yield b1 --> b2 |> Group.by (function Odd(n,_) -> (n.x)) 
        yield b1 --> b2 |> Group.by (function Even(x,str) -> (x.x,str.str))
    }

[<Test>]
[<Category("interactive")>]
let ``Hosts``() = 
//     let log taskId f = Diagnostics.Debug.WriteLine("{0}\t{1}: {2}",DateTime.Now,taskId,f())
     let log taskId = ignore
     
     let stop = 
         Topologies.t1 
         |> withConf [Conf.TOPOLOGY_MAX_SPOUT_PENDING, 200
                      Conf.TOPOLOGY_ACKER_EXECUTORS, 4]
         |> Host.runWith log 
     let startedAt = DateTime.Now
     let trace () = 
         Diagnostics.Debug.WriteLine("-- Emited: {0}, Acked: {1}, In-flight: {2}, rate: \t{3}", !Topologies.world.count, !Topologies.world.acked, (!Topologies.world.count - !Topologies.world.acked), (float !Topologies.world.acked)/(DateTime.Now - startedAt).TotalSeconds)
         Diagnostics.Debug.WriteLine("-- GC: {0}", GC.GetTotalMemory(false))
         Diagnostics.Debug.Flush()

     for i in 1..10 do 
         Threading.Thread.Sleep 10000
         trace()

     stop()
     
     trace()
