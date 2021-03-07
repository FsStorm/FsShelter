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
                 |> Spout.runReliable (fun log _ -> world )
                                     (fun w -> (fun _ -> Interlocked.Increment &w.acked.contents |> ignore), ignore)
                                     ignore                                    
        let b1 = split
                 |> Bolt.run (fun log _ t emit -> (t,emit))
                 |> withParallelism 2
        let b2 = printBolt
                 |> Bolt.run (fun log _ t _ -> (log LogLevel.Info),t)
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
//    let log taskId f = TraceLog.asyncLog (fun _ -> sprintf "%d: %s" taskId (f()))
    let log taskId = ignore
    
    let stop = 
        Topologies.t1 
        |> withConf [ TOPOLOGY_MAX_SPOUT_PENDING 2
                      TOPOLOGY_ACKER_EXECUTORS 2]
//                      TOPOLOGY_DEBUG true]
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
                 |> withConf [ TOPOLOGY_MAX_SPOUT_PENDING 20
                               TOPOLOGY_ACKER_EXECUTORS 4]
                 |> fun t -> { t with Name = sprintf "t%d" i } 
                 |> fun t -> Hosting.runWith (log t.Name) t
         } |> Seq.toArray

     let startedAt = DateTime.Now
     let trace () = 
         TraceLog.asyncLog (fun _ -> sprintf  "-- Emited: %d, Acked: %d, In-flight: %d, rate: %4.2f" !Topologies.world.count !Topologies.world.acked (!Topologies.world.count - !Topologies.world.acked) ((float !Topologies.world.acked)/(DateTime.Now - startedAt).TotalSeconds))
         TraceLog.asyncLog (fun _ -> sprintf  "-- GC: %d, %d/%d/%d" (GC.GetTotalMemory false) (GC.CollectionCount 0) (GC.CollectionCount 1) (GC.CollectionCount 2))

     Threading.Thread.Sleep 10000

     stop
     |> Array.iter (fun s -> s())
     
     trace()


[<Test>]
[<Category("interactive")>]
let ``No emits``() = 
     let log taskId f = TraceLog.asyncLog (fun _ -> sprintf "%d: %s" taskId (f()))
     let none () : (string*Schema) option =
        // let now = DateTime.Now
        // match now.Millisecond % 20 with
        // | 0 -> Some (string now, Original {x = now.Second})
        // | _ -> 
               System.Threading.Thread.Sleep 1000
               None
     let noop (_:Schema) = ()
     let t = topology "test" {
        let s1 = none
                 |> Spout.runReliable (fun _ _ -> ()) (fun _ -> ignore, ignore) ignore
        let b1 = noop
                 |> Bolt.run (fun _ _ t emit -> t)
                 |> withParallelism 2
        yield s1 ==> b1 |> Shuffle.on Original
     }
     let stop = 
         t
         |> withConf [ TOPOLOGY_MAX_SPOUT_PENDING 1
                       TOPOLOGY_EXECUTOR_RECEIVE_BUFFER_SIZE 8
                       TOPOLOGY_ACKER_EXECUTORS 1
                       TOPOLOGY_DEBUG true]
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
     
     
[<Test>]
[<Category("interactive")>]
let ``Long-running bolt``() = 
     let log taskId f = TraceLog.asyncLog (fun _ -> sprintf "%d: %s" taskId (f()))
     let mutable t = Some ("", Original {x = 1})  
     let one () : (string*Schema) option =
           let ret = t
           t <- None
           System.Threading.Thread.Sleep 1000
           ret
     let sleep (t:Schema, emit) =
         System.Threading.Thread.Sleep (TimeSpan.FromMinutes 5.)
         emit t
     let t = topology "test" {
        let s1 = one
                 |> Spout.runReliable (fun _ _ -> ()) (fun _ -> ignore, ignore) ignore
        let b1 = sleep
                 |> Bolt.run (fun _ _ t emit -> t, emit)
        let b2 = sleep
                 |> Bolt.run (fun _ _ t emit -> t, emit)
        yield s1 ==> b1 |> Shuffle.on Original
        yield b1 ==> b2 |> Shuffle.on Original
     }
     let stop = 
         t
         |> withConf [ TOPOLOGY_MAX_SPOUT_PENDING 1
                       TOPOLOGY_EXECUTOR_RECEIVE_BUFFER_SIZE 8
                       TOPOLOGY_ACKER_EXECUTORS 1
                       TOPOLOGY_DEBUG true]
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

// [<Test>]
// let ``Just channels``() = 
//     let log taskId f = TraceLog.asyncLog (fun _ -> sprintf "%d: %s" taskId (f()))
//     TraceLog.asyncLog (fun _ -> sprintf "%d: starting" System.Threading.Thread.CurrentThread.ManagedThreadId)
//     let pumpNext =
//         let (send,_) = Hosting.Channel.start (log 1) 8 ignore (fun eob (next,self) -> next |> self)
//         fun next self -> send (next,self)
//     let (send,_) = Hosting.Channel.start (log 2) 8 ignore (fun eob v -> Threading.Thread.Sleep 1000)
//     for i in 1..100 do
//         pumpNext i send
 
 //    let longWaitStrategy () =
//        let gate = obj()
//        { new IWaitStrategy with
//            member __.SignalAllWhenBlocking () =
//                lock gate (fun _ -> Monitor.PulseAll gate)
//            member __.WaitFor (sequence: int64, cursor: Sequence, dependentSequence: ISequence, barrier: ISequenceBarrier) =
//                if cursor.Value < sequence then
//                    lock gate (fun _ -> barrier.CheckAlert(); Monitor.Wait gate |> ignore)
//                let mutable availableSequence = dependentSequence.Value
//                while availableSequence < sequence do
//                    barrier.CheckAlert()
//                    Thread.Sleep 1000
//                    availableSequence <- dependentSequence.Value
//                availableSequence }
