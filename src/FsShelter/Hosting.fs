/// Self-hosting
module FsShelter.Host

open System
open System.IO
open System.Threading
open FsShelter.Multilang
open FsShelter.Topology
open FsShelter.Task

[<AutoOpen>]
module internal Types =
    type TaskId = int

    type AnchoredTupleId = int64 * int64

    type TaskMap<'t> = Map<TaskId,RuntimeTask<'t>>

    and AckerMsg =
        | Cleanup
        | Anchor of AnchoredTupleId
        | Ok of AnchoredTupleId
        | Fail of AnchoredTupleId
        | Track of TaskId * TupleId * int64

    and TaskIn<'t> =
        | InCmd of InCommand<'t>
        | AckerCmd of AckerMsg

    and Input<'t> = unit -> Async<TaskIn<'t>>

    and Channel<'t> = TaskControl<'t> -> unit

    and RuntimeTask<'t> =
        | SystemTask of Channel<'t>
        | AckerTask of Channel<'t>
        | SpoutTask of ComponentId * Channel<'t>
        | BoltTask of ComponentId * Channel<'t>
        with override x.ToString() =
                match x with
                | SystemTask _ -> "__system"
                | AckerTask _ -> "__acker"
                | SpoutTask (compId,_) -> compId
                | BoltTask (compId,_) -> compId


    and Loop<'t> = (TaskIn<'t> -> unit) -> TaskMap<'t> -> Log -> Input<'t> -> Async<unit>

    and TaskControl<'t> =
        | Enqueue of TaskIn<'t>
        | Dequeue of AsyncReplyChannel<TaskIn<'t>>
        | Stop
        | Start of TaskMap<'t> * Log

    let (|AnchoredTuple|_|) (tupleId:string) =
        match tupleId.Split (':') with
        | [|anchor;tid|] -> Some (int64 anchor, int64 tid)
        | _ -> None
    
    [<Struct>]
    type TreeState =
        | Pending of TupleId*TaskId*int64
        | Complete

module internal Channel =
    let make (dispatch:Loop<_>) =
        let run self map input log token = 
            let rec loop () =
                dispatch self map log input
                |> Async.Catch 
                |> Async.map (function | Choice2Of2 ex -> 
                                            log (fun _ -> sprintf "Loop failed: %s, restarting..." (Exception.toString ex))
                                            Async.Start (loop(),token)
                                       | _ -> log (fun _ -> "Finished")) 
            Async.Start (loop(),token)

        let inQueue = System.Collections.Generic.Queue<TaskIn<_>>()
        let outQueue = System.Collections.Generic.Queue<AsyncReplyChannel<_>>()
        let enqueue msg =
            match outQueue.Count, inQueue.Count with
            | 0,_ -> inQueue.Enqueue msg
            | _,0 ->
                let rc = outQueue.Dequeue()
                rc.Reply msg
            | _ ->
                inQueue.Enqueue msg
                let rc = outQueue.Dequeue()
                let msg = inQueue.Dequeue()
                rc.Reply msg

        let dequeue rc =
            match inQueue.Count, outQueue.Count with
            | 0,_ -> outQueue.Enqueue rc
            | _,0 -> inQueue.Dequeue() |> rc.Reply
            | _ -> 
                outQueue.Enqueue rc
                let rc = outQueue.Dequeue()
                inQueue.Dequeue() |> rc.Reply

        let inbox = MailboxProcessor.Start(fun mb ->
            let rec init () =
                async {
                    let! msg = mb.Receive()
                    let ts = new CancellationTokenSource()
                    match msg with
                    | Start (map, log) -> 
                        run (Enqueue >> mb.Post) map (fun () -> mb.PostAndAsyncReply Dequeue) log ts.Token
                        Activate |> InCmd |> inQueue.Enqueue
                        return! loop log ts
                    | Stop -> return! shutdown ts
                }
            
            and loop log ts =
                async {
                    let! msg = mb.Receive()
                    match msg with
                    | Enqueue cmd -> 
                        // match cmd with
                        // | AckerCmd Cleanup -> 
                        //     log (fun _ -> sprintf "\t%d/%d" inQueue.Count outQueue.Count)
                        // | _ -> ()
                        enqueue cmd
                    | Dequeue rc -> dequeue rc
                    | Stop -> return! shutdown ts
                    return! loop log ts
                }

            and shutdown ts =
                async {
                    let wait = 1000 // TODO: Read a Conf option
                    let shutdownStarted = DateTime.Now
                    let! rc = 
                        if outQueue.Count > 0 then outQueue.Dequeue() |> Some |> async.Return
                        else mb.Receive wait |> Async.Catch |> Async.map (function Choice1Of2(Dequeue rc) -> Some rc | _ -> None)
                    rc |> Option.iter (fun rc -> Deactivate |> InCmd |> rc.Reply)
                    let remainingWait = ((float wait) - (DateTime.Now - shutdownStarted).TotalMilliseconds)
                    do! Async.Sleep (int (max remainingWait 0.))
                    ts.Cancel false
                }
            init ()
        )
        inbox.Post


module internal Routing =
    let direct =
        function
        | SystemTask (channel)
        | AckerTask (channel)
        | SpoutTask (_,channel)
        | BoltTask (_,channel) -> Enqueue >> channel

    let mkTupleRouter (topology : Topology<'t>) taskId (tasks:Map<TaskId,RuntimeTask<'t>>) =
        let sinks =
            tasks
            |> Map.chooseBy (fun (taskId,task) -> 
                match task with
                | BoltTask (compId,channel) -> Some (compId,channel)
                | _ -> None)
        
        let group (instances:_ array) =
            function
            | All -> 
                fun _ _ -> instances :> _ seq
            | Shuffle when instances.Length = 1 -> 
                 fun _ _ -> instances :> _ seq
            | Shuffle -> 
                let ix tupleId = Math.Abs(tupleId.GetHashCode() % instances.Length)
                fun tupleId _ -> 
                     instances.[ix tupleId] |> Seq.singleton
            | Direct -> 
                fun _ _ -> Seq.empty
            | Fields (map,_) -> 
                fun tupleId tuple -> 
                    let ix = (map tuple).GetHashCode() % instances.Length
                    instances.[ix] |> Seq.singleton

        let channels =
            topology.Streams
            |> Seq.fold (fun map (KeyValue((streamId,dstId),stream)) ->
                let f = match map |> Map.tryFind streamId with
                        | Some f -> f
                        | _ -> fun _ _ -> Seq.empty
                let all = sinks |> Map.find dstId |> Array.ofSeq
                let group = group all stream.Grouping
                map |> Map.add streamId (fun tupleId tuple -> f tupleId tuple |> Seq.append (group tupleId tuple))) Map.empty
        
        let direct = memoize (fun taskId -> tasks |> Map.find taskId |> direct)
        function
        | (tuple,tupleId,srcId,stream,Some dstId) ->
            Tuple(tuple,tupleId,srcId,stream,taskId) |> InCmd |> direct taskId
        | (tuple,tupleId,srcId,stream,_) ->
            channels 
            |> Map.find (srcId,stream)
            |> fun f -> f tupleId tuple |> Seq.apply (Tuple(tuple,tupleId,srcId,stream,taskId) |> InCmd |> Enqueue)

    let mkRouter tasks filter =
        let sinks = 
            tasks 
            |> Map.filter filter
            |> Seq.map (fun (KeyValue(_,task)) -> direct task)
            |> Seq.cache
                            
        fun cmd -> 
            sinks
            |> Seq.apply cmd

    let toTask taskId tasks =
        tasks |> Map.find taskId |> direct 

module internal TupleTree = 
    let mkIdGenerator() =
        let rnd = Random()
        let bytes = Array.zeroCreate<byte> 8;
        let rec nextId () =
            rnd.NextBytes(bytes)
            let v = BitConverter.ToInt64 (bytes,0)
            if v = 0L then nextId()
            else v
        nextId

    let inline ackerOfAnchor (ackers:_ array) (anchorId:int64) =
        let i = Math.Abs (anchorId % (int64 ackers.Length))
        ackers.[int i]

    let newId nextId =
        let tupleId = nextId()
        [string tupleId]

    let track nextId ackers taskId srcId route stream tuple sourceTupleId dstId =
        let tupleId = nextId()
        match sourceTupleId with
        | Some sid -> Track(taskId,sid,tupleId) |> AckerCmd |> (ackerOfAnchor ackers tupleId |> Routing.direct)
        | _ -> ()
        let internalTupleId = sprintf "%d:%d" tupleId tupleId
        (tuple, internalTupleId, srcId, stream, dstId) |> route

    let anchor nextId ackers srcId route anchors tupleIds stream tuple dstId =
        let tupleId = nextId()
        let anchoredIds =
            anchors 
            |> List.choose (|AnchoredTuple|_|)
            |> List.map (fun (a,_) -> (a,tupleId))
        
        anchoredIds 
        |> List.iter (fun (a,id) ->
            let enqueue = ackerOfAnchor ackers a |> Routing.direct
            Anchor(a,id) |> AckerCmd |> enqueue)
        
        match anchoredIds with
        | [] -> [string tupleId]
        | _ -> anchoredIds |> List.map (fun (a,id) -> sprintf "%d:%d" a id)
        |> List.iter (fun tupleId -> (tuple, tupleId, srcId, stream, dstId) |> route)

    let mkAck ackers = 
        function
        | AnchoredTuple (a,id) -> 
            let enqueue = ackerOfAnchor ackers a |> Routing.direct
            Ok(a,id) |> AckerCmd |> enqueue
        | _ -> ()

    let mkNack ackers = 
        function
        | AnchoredTuple (a,id) -> 
            let enqueue = ackerOfAnchor ackers a |> Routing.direct
            Fail(a,id) |> AckerCmd |> enqueue
        | _ -> ()

module internal Tasks =
    let s n = 1000 * n
    let m n = 60 * (s n)
    let mkSystem nextId (topology : Topology<'t>) self (tasks:TaskMap<'t>) log input = 
        let describe state taskId =
            function
            | SpoutTask (compId,_) -> sprintf "%s\n\t%d: %s (%+A)" state taskId compId (topology.Spouts |> Map.find compId).Conf
            | BoltTask (compId,_) -> sprintf "%s\n\t%d: %s (%+A)" state taskId compId (topology.Bolts |> Map.find compId).Conf
            | t -> sprintf "%s\n\t%d: %s" state taskId (t.ToString())
        let tick dstId = 
            let tuple = TupleSchema.mkTick()
            let route = Routing.mkRouter tasks (fun _ -> function BoltTask (compId,_) -> compId = dstId | _ -> false)
            fun _ -> Tuple(tuple.Value, (string <| nextId()), "__system", "__tick", 1) |> InCmd |> route
        let cleanup = 
            let msg = AckerCmd Cleanup 
            let route = Routing.mkRouter tasks (fun _ -> function AckerTask _ -> true | _ -> false) 
            fun _ -> route msg
        let startTimers =
            topology.Bolts
            |> Seq.choose (fun (KeyValue(compId,b)) -> 
                    b.Conf 
                    |> Conf.option Conf.TOPOLOGY_TICK_TUPLE_FREQ_SECS 
                    |> Option.map (fun tf -> compId, tf))
            |> Seq.map (fun (compId,timeout) -> 
                    log(fun _ -> sprintf "Starting timer with timeout %ds for: %s" timeout compId)
                    new System.Threading.Timer(tick compId, (), s timeout, s timeout)
                    :> IDisposable)
            |> Seq.append
            <| seq { 
                yield new System.Threading.Timer(cleanup, (), s 30, s 30) :> IDisposable 
            }

        let rec loop timers =
            async {
                let! cmd = input()
                match cmd with
                | InCmd Activate ->
                    log(fun _ -> sprintf "Starting system for topology %s: [%s]" topology.Name (tasks |> Map.fold describe ""))
                    return! startTimers |> Seq.toArray |> loop
                | InCmd Deactivate -> 
                    log(fun _ -> sprintf "Stopping system for topology: %s" topology.Name)
                    timers |> Seq.iter (fun _ -> timers |> Seq.iter (fun t -> t.Dispose()))
                | _ -> log (fun _ -> sprintf "unexpect message received by __system: %+A" cmd)
            }
        loop [||]


    let mkAcker (topology : Topology<'t>) self (taskMap:TaskMap<'t>) log input = 
        let inFlight = System.Collections.Generic.Dictionary<int64,TreeState>(100)
        let xor tupleId anchor =
            match inFlight.TryGetValue anchor with
            | (true,Pending(sourceId,taskId,v)) -> 
                let v = v ^^^ tupleId
                if v = 0L then Complete else Pending(sourceId,taskId,v)
            | _ -> Complete
        
        let check tupleId anchor cont =
            match xor tupleId anchor with
            | Pending(sourceId,taskId,_) ->
                inFlight.[anchor] <- Complete
                let (SpoutTask (_,channel)) = taskMap |> Map.find taskId
                cont sourceId |> InCmd |> Enqueue |> channel
            | _ -> ()

        let cleanup () =
            inFlight 
            |> Array.ofSeq 
            |> Seq.filter (function KeyValue(anchor,Complete) -> true | _ -> false) 
            |> Seq.iter (fun (KeyValue(anchor,_))-> inFlight.Remove anchor |> ignore)

        let rec loop () =
            async {
                let! cmd = input()
                match cmd with
                | InCmd Activate ->
                    log(fun _ -> "Starting acker...")
                | InCmd Deactivate ->
                    log(fun _ -> "Stopping acker...")
                    return ()
                | AckerCmd (Track (taskId,sid,tupleId)) -> 
                    inFlight.Add (tupleId, Pending(sid,taskId,tupleId))
                | AckerCmd (Anchor (anchor,tupleId)) -> 
                    inFlight.[anchor] <- xor tupleId anchor
                | AckerCmd (Fail (anchor,tupleId)) ->
                    check tupleId anchor Nack
                | AckerCmd (Ok (anchor,tupleId)) ->
                    check tupleId anchor Ack
                | AckerCmd Cleanup ->
                    cleanup()
                | _ -> ()
                return! loop()
            }
        loop()

    let mkSpout mkRoute compId (comp:Spout<'t>) (topology : Topology<'t>) (runnable:Runnable<'t>) self (tasks:TaskMap<'t>) log input = 
        let conf = comp.Conf |> Map.join topology.Conf
        let route = mkRoute tasks
        let mutable pending = 0
        let mkThrottle maxPending =
            let check cont =
                pending <- pending - 1
                while pending < maxPending do
                    cont ()
            check
        let noThrottle = ignore
        let throttle = comp.Conf 
                        |> Map.join topology.Conf 
                        |> Conf.option Conf.TOPOLOGY_MAX_SPOUT_PENDING
                        |> Option.map mkThrottle
                        |> Option.defaultValue noThrottle
        let next = 
            let msg = Next |> InCmd
            fun () -> 
                pending <- pending + 1
                msg |> self
        async {
            log(fun _ -> sprintf "Starting %s..." compId)
            let rec input' () =
                async {
                    let! cmd = input() 
                    match cmd with 
                    | InCmd cmd -> 
                        match cmd with
                        | Activate -> next()
                        | Nack _ | Ack _ -> throttle next 
                        | _ -> ()
                        return cmd
                    | _ -> return! input'()
                }                
            let output =
                function
                | Emit (t,tupleId,_,stream,dstId,needIds) -> 
                    route stream t tupleId dstId
                | Error (text,ex) -> log (fun _ -> sprintf "%+A\t%s%+A" LogLevel.Error text ex)
                | Log (text,level) -> log (fun _ -> sprintf "%+A\t%s" level text)
                | Sync -> ()
                | cmd -> failwithf "Unexpected command: %+A" cmd
            let io = (input', output)
            return! runnable io conf
        }

    let mkBolt ackers mkRoute compId (comp:Bolt<'t>) (topology : Topology<'t>) (runnable:Runnable<'t>) self (tasks:TaskMap<'t>) log input = 
        let conf = comp.Conf |> Map.join topology.Conf
        let route = mkRoute tasks
        let ack = TupleTree.mkAck ackers
        let nack = TupleTree.mkNack ackers
        async {
            log(fun _ -> sprintf "Starting %s..." compId)
            let rec input' () =
                async {
                    let! cmd = input() 
                    match cmd with 
                    | InCmd cmd -> 
                        return cmd
                    | _ -> return! input'()
                }                
            let output =
                function
                | Emit (t,tupleId,anchors,stream,dstId,needIds) -> route anchors tupleId stream t dstId
                | Error (text,ex) -> log (fun _ -> sprintf "%+A\t%s%+A" LogLevel.Error text ex)
                | Log (text,level) -> log (fun _ -> sprintf "%+A\t%s" level text)
                | OutCommand.Ok tid -> ack tid
                | OutCommand.Fail tid -> nack tid
                | cmd -> failwithf "Unexpected command: %+A" cmd
            let io = (input', output)
            return! runnable io conf
        }

    let mkTasks (topology : Topology<'t>) : Map<int,RuntimeTask<'t>> =
        let anchorOfStream = topology.Anchors.TryFind >> Option.defaultValue (fun _ -> [])
        let raiseNotRunnable() = failwith "Only native FsShelter components can be hosted"
        let ackersCount = topology.Conf |> Conf.optionOrDefault Conf.TOPOLOGY_ACKER_EXECUTORS
        let system = Channel.make (mkSystem (TupleTree.mkIdGenerator()) topology) |> SystemTask
        let ackers = [| for i in 1..ackersCount -> Channel.make (mkAcker topology) |> AckerTask |]
        let nextTaskId =
            let mutable taskId = 0
            fun () ->
                taskId <- taskId + 1
                taskId
        seq { 
            yield (fun _ -> system)
            yield! seq { for i in 1..ackers.Length -> fun _ -> ackers.[i-1] }
            yield! topology.Bolts
                   |> Seq.collect (fun (KeyValue(compId,b)) -> 
                        let runnable = match b.MkComp anchorOfStream with FuncRef r -> r | _ -> raiseNotRunnable() 
                        seq { for i in 1..(int b.Parallelism) ->
                                 fun taskId -> 
                                    let send tasks = TupleTree.anchor (TupleTree.mkIdGenerator()) ackers compId (Routing.mkTupleRouter topology taskId tasks)
                                    let channel = Channel.make (mkBolt ackers send compId b topology runnable) 
                                    BoltTask (compId, channel)}) 
            yield! topology.Spouts
                   |> Seq.collect (fun (KeyValue(compId,s)) -> 
                        let runnable = match s.MkComp() with FuncRef r -> r | _ -> raiseNotRunnable()
                        seq { for i in 1..(int s.Parallelism) ->
                                fun taskId -> 
                                    let send tasks = TupleTree.track (TupleTree.mkIdGenerator()) ackers taskId compId (Routing.mkTupleRouter topology taskId tasks)
                                    let channel = Channel.make (mkSpout send compId s topology runnable)
                                    SpoutTask (compId, channel)})
        }
        |> Seq.map (fun mkTask -> 
                        let taskId = nextTaskId() 
                        taskId, mkTask taskId)
        |> Map.ofSeq


let runWith (startLog : int->Log) (topology : Topology<'t>) =
    let tasks = Tasks.mkTasks topology

    let start =
        fun taskId ->
            function
            | SystemTask channel
            | AckerTask channel
            | SpoutTask (_,channel)
            | BoltTask (_,channel) -> 
                Start (tasks, startLog taskId) 
                |> channel

    let stop _ =
        function
        | SystemTask channel
        | AckerTask channel
        | SpoutTask (_,channel)
        | BoltTask (_,channel) -> 
            Stop |> channel
            Thread.Sleep 1000 //TODO: Read a Conf option

    tasks |> Map.iter start

    fun () -> tasks |> Map.iter stop

let run topology = runWith (fun _ -> ignore) topology
 
