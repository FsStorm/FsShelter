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
        let x = Interlocked.Increment &world.count.contents
        //if (x % 5L) = 0L then 
        //    do! Async.Sleep 1000
        //    return None
        //else 
            //return Some(string x, Original { x = world.rnd.Next(0, 100) }) 
        Some(string x, Original { x = world.rnd.Next(0, 100) }) 
    
    let printBolt (log,t) =
        match t with 
        | Original _ -> log (sprintf "Init: %A" DateTime.Now)
        | MaybeString _ -> log (sprintf "Shutdown: %A" DateTime.Now)
        | Tick 
        | _ -> () //log (sprintf "tuple: %A" t)

    let world = {rnd = Random(); count = ref 0L; acked = ref 0L}
    
    let t1 = topology "test" {
        let s1 = numbers
                 |> runReliableSpout (fun log _ -> world )
                                     (fun w -> (fun _ -> Interlocked.Increment &w.acked.contents |> ignore), ignore)
                                     ignore                                    
        let b1 = split
                 |> runBolt (fun log _ t emit -> (t,emit))
                 |> withParallelism 2
        let b2 = printBolt
                 |> runBolt (fun log _ t _ -> (log LogLevel.Info),t)
                 |> withActivation (Original {x=1})
                 |> withDeactivation (MaybeString None)
                 //|> withConf [Conf.TOPOLOGY_TICK_TUPLE_FREQ_SECS, 1]

        yield s1 ==> b1 |> Shuffle.on Original
        yield b1 --> b2 |> Group.by (function Odd(n,_) -> (n.x)) 
        yield b1 --> b2 |> Group.by (function Even(x,str) -> (x.x,str.str))
    }


[<Test>]
[<Category("interactive")>]
let ``Hosts``() = 
//     let log taskId f = TraceLog.asyncLog (fun _ -> sprintf "%d: %s" taskId (f()))
     let log taskId = ignore
     
     let stop = 
         Topologies.t1 
         |> withConf [ConfOption.TOPOLOGY_MAX_SPOUT_PENDING 20
                      ConfOption.TOPOLOGY_ACKER_EXECUTORS 2]
         |> Hosting.runWith log 
     let startedAt = DateTime.Now
     let proc = System.Diagnostics.Process.GetCurrentProcess()
     let mutable procTime = proc.TotalProcessorTime.TotalMilliseconds
     let trace () =
         let s = sprintf "-- Emited: %d, Acked: %d, In-flight: %d, rate: %4.2f" !Topologies.world.count !Topologies.world.acked (!Topologies.world.count - !Topologies.world.acked) ((float !Topologies.world.acked)/(DateTime.Now - startedAt).TotalSeconds) 
         TraceLog.asyncLog (fun _ -> s)
         let s = sprintf "-- GC: %dKB, %d/%d/%d" (GC.GetTotalMemory(false)/1024L) (GC.CollectionCount 0) (GC.CollectionCount 1) (GC.CollectionCount 2)
         TraceLog.asyncLog (fun _ -> s)
         let s = sprintf "-- CPU: %4.2f" (proc.TotalProcessorTime.TotalMilliseconds - procTime)
         TraceLog.asyncLog (fun _ -> s)
         procTime <- proc.TotalProcessorTime.TotalMilliseconds

     for i in 1..50 do 
         Threading.Thread.Sleep 10000
         trace()

     stop()
     
     trace()

[<Test>]
[<Category("interactive")>]
let ``Several``() = 
//     let log name taskId f = TraceLog.formatLine "{0}\t{1}:{2} {3}" [|DateTime.Now,name,taskId,f()|]
     let log _ _ = ignore 
     
     let stop = 
         seq {
             for i in 1..10 ->
                 Topologies.t1 
                 |> withConf [ConfOption.TOPOLOGY_MAX_SPOUT_PENDING 20
                              ConfOption.TOPOLOGY_ACKER_EXECUTORS 4]
                 |> fun t -> { t with Name = sprintf "t%d" i } 
                 |> fun t -> Hosting.runWith (log t.Name) t
         } |> Seq.toArray

     let startedAt = DateTime.Now
     let trace () = 
         TraceLog.asyncLog (fun _ -> sprintf  "-- Emited: %d, Acked: %d, In-flight: %d, rate: %4.2f" !Topologies.world.count !Topologies.world.acked (!Topologies.world.count - !Topologies.world.acked) ((float !Topologies.world.acked)/(DateTime.Now - startedAt).TotalSeconds))
         TraceLog.asyncLog (fun _ -> sprintf  "-- GC: %d, %d/%d/%d" (GC.GetTotalMemory(false)) (GC.CollectionCount 0) (GC.CollectionCount 1) (GC.CollectionCount 2))

     Threading.Thread.Sleep 10000

     stop
     |> Array.iter (fun s -> s())
     
     trace()
