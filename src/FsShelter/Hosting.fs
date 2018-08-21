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

    and SystemMsg =
        | Tick
        
    and AckerMsg =
        | Anchor of AnchoredTupleId
        | Ok of AnchoredTupleId
        | Fail of AnchoredTupleId
        | Track of TaskId * TupleId * int64

    and TaskIn<'t> =
        | InCmd of InCommand<'t>
        | SystemCmd of SystemMsg
        | AckerCmd of AckerMsg

    and Input<'t> = unit -> TaskIn<'t>

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


    and Loop<'t> = (TaskIn<'t> -> unit) -> TaskMap<'t> -> Log -> Input<'t> -> unit

    and TaskControl<'t> =
        | Enqueue of TaskIn<'t>
        | Dequeue of AsyncReplyChannel<TaskIn<'t>>
        | Stop
        | Start of TaskMap<'t> * Log

    let (|AnchoredTuple|_|) (tupleId:string) =
        match tupleId.Split (':') with
        | [|anchor;tid|] -> Some (int64 anchor, int64 tid)
        | _ -> None
    
    type TreeState =
        | Pending of TupleId * TaskId * int64
        | Complete of srcId:TupleId * src:TaskId
        | Done

module internal Channel =
    let make (dispatch:Loop<_>) =
        let run self map input log token = 
            let rec loop () =
                let retry =
                    try
                        dispatch self map log input
                        log (fun _ -> "Finished")
                        ignore
                    with ex -> 
                        log (fun _ -> sprintf "Loop failed: %s, restarting..." (Exception.toString ex))
                        loop
                retry()                    
            loop()

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
                        run (Enqueue >> mb.Post) map (fun () -> mb.PostAndReply Dequeue) log ts.Token
                        Activate |> InCmd |> inQueue.Enqueue
                        return! loop log ts
                    | Stop -> return! shutdown ts
                }
            
            and loop log ts =
                async {
                    let! msg = mb.Receive()
                    match msg with
                    | Enqueue cmd -> enqueue cmd
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

    let mkTupleRouter (topology:Topology<'t>) taskId (tasks:Map<TaskId,RuntimeTask<'t>>) mkIds log =
        let sinksOfComp =
            let bolts =
                tasks
                |> Map.chooseBy (fun (_,task) -> 
                    match task with
                    | BoltTask (compId,channel) -> Some (compId,channel)
                    | _ -> None)
            memoize (fun compId -> bolts.[compId] |> Array.ofSeq)
        
        let mkGroup (instances:_ array) =
            function
            | All -> 
                fun _ _ -> instances :> _ seq
            | Shuffle when instances.Length = 1 -> 
                 fun _ _ -> instances :> _ seq
            | Shuffle -> 
                let ix tupleId = abs(tupleId.GetHashCode() % instances.Length)
                fun tupleId _ -> 
                     instances.[ix tupleId] |> Seq.singleton
            | Direct -> 
                fun _ _ -> Seq.empty
            | Fields (map,_) -> 
                fun _ tuple -> 
                    let ix = (map tuple).GetHashCode() % instances.Length |> abs
                    instances.[ix] |> Seq.singleton

        let mkDistributors map (KeyValue((streamId,dstId),streamDef)) =
            let f = match map |> Map.tryFind streamId with
                    | Some f -> f
                    | _ -> fun _ -> ignore
            let instances = sinksOfComp dstId
            let group = mkGroup instances streamDef.Grouping
            map |> Map.add streamId (fun mkIds tuple ->
                                        f mkIds tuple
                                        mkIds ()
                                        |> Seq.iter (fun tupleId -> 
                                                        let msg = Tuple(tuple,tupleId,fst streamId,snd streamId,taskId) |> InCmd |> Enqueue
                                                        group tupleId tuple
                                                        |> Seq.apply msg))
        let distributors =
            topology.Streams
            |> Seq.fold mkDistributors Map.empty
        
        let direct = memoize (fun dstId -> tasks |> Map.find dstId |> direct)
        function
        | struct (anchors:TupleId list,srcId:TupleId option,tuple:'t,compId,stream,Some dstId) ->
            mkIds anchors srcId ()
            |> List.iter (fun tupleId -> Tuple(tuple,tupleId,compId,stream,taskId) |> InCmd |> direct dstId)
        | struct (anchors,srcId,tuple,compId,stream,_) ->
            match distributors |> Map.tryFind (compId,stream) with
            | Some d -> tuple |> d (mkIds anchors srcId)
            | _ -> ()

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
    let seed = ref (int DateTime.Now.Ticks)
    let mkIdGenerator() =
        let rnd = Random(Interlocked.Increment &seed.contents)
        let bytes = Array.zeroCreate<byte> 8
        let rec nextId () =
            let v = lock bytes (fun _ -> 
                rnd.NextBytes(bytes)
                BitConverter.ToInt64 (bytes,0))
            if v = 0L then nextId()
            else v
        nextId

    let inline ackerOfAnchor (ackers:_ array) (anchorId:int64) =
        let i = abs (anchorId % (int64 ackers.Length))
        ackers.[int i]

    let track nextId ackers taskId _ sourceTupleId =
        let anchorId = nextId()
        let toAcker = AckerCmd >> (ackerOfAnchor ackers anchorId |> Routing.direct)
        match sourceTupleId with
        | Some sid -> Track(taskId,sid,anchorId) |> toAcker
        | _ -> ()
        fun () ->
            let tupleId = nextId()
            Anchor(anchorId,tupleId) |> toAcker
            [sprintf "%d:%d" anchorId tupleId]

    let anchor nextId ackers anchors _ =
        let anchors = 
            anchors 
            |> List.choose ((|AnchoredTuple|_|) >> Option.map (fun (a,_) -> a, ackerOfAnchor ackers a |> Routing.direct))
        fun () ->
            let tupleId = nextId()
            let anchoredIds = anchors |> List.map (fun a -> (a,tupleId))
            
            anchoredIds 
            |> List.iter (fun ((aid,enqueue),id) ->
                Anchor(aid,id) |> AckerCmd |> enqueue)
            
            match anchoredIds with
            | [] -> [string tupleId]
            | _ -> anchoredIds |> List.map (fun ((aid,_),id) -> sprintf "%d:%d" aid id)

    let mkAck toResult ackers = 
        function
        | AnchoredTuple (a,id) -> 
            let enqueue = ackerOfAnchor ackers a |> Routing.direct
            toResult (a,id) |> AckerCmd |> enqueue
        | _ -> ()

module internal Tasks =
    let s n = 1000 * n
    let m n = 60 * (s n)
    let mkSystem nextId (topology:Topology<'t>) self (tasks:TaskMap<'t>) log input = 
        let describe state taskId =
            function
            | SpoutTask (compId,_) -> sprintf "%s\n\t%d: %s (%+A)" state taskId compId (topology.Spouts |> Map.find compId).Conf
            | BoltTask (compId,_) -> sprintf "%s\n\t%d: %s (%+A)" state taskId compId (topology.Bolts |> Map.find compId).Conf
            | t -> sprintf "%s\n\t%d: %s" state taskId (t.ToString())
        let tick dstId = 
            let tuple = TupleSchema.mkTick()
            let route = Routing.mkRouter tasks (fun _ -> function BoltTask (compId,_) -> compId = dstId | _ -> false)
            match tuple with
            | Some t -> fun _ -> Tuple(t, (string <| nextId()), "__system", "__tick", 1) |> InCmd |> route
            | _ -> failwith "Topology schema doesn't define \"__tick\" tuple"
        let systemTick = 
            let msg = SystemCmd Tick 
            let route = Routing.mkRouter tasks (fun _ -> function AckerTask _ | SpoutTask _ -> true | _ -> false) 
            fun _ -> route msg
        let startTimers =
            topology.Bolts
            |> Seq.choose (fun (KeyValue(compId,b)) -> 
                    b.Conf 
                    |> Conf.option Conf.TOPOLOGY_TICK_TUPLE_FREQ_SECS 
                    |> Option.map (fun tf -> compId, tf))
            |> Seq.map (fun (compId,timeout) -> 
                    log(fun _ -> sprintf "Starting timer with timeout %ds for: %s" timeout compId)
                    new Timer(tick compId, (), s timeout, s timeout)
                    :> IDisposable)
            |> Seq.append
            <| seq { 
                yield new Timer(systemTick, (), s 30, s 30) :> IDisposable 
            }

        let rec loop timers =
            let cmd = input()
            match cmd with
            | InCmd Activate ->
                log(fun _ -> sprintf "Starting system for topology %s: [%s],\nconf: %A" topology.Name (tasks |> Map.fold describe "") topology.Conf)
                startTimers |> Seq.toArray |> loop
            | InCmd Deactivate -> 
                log(fun _ -> sprintf "Stopping system for topology: %s" topology.Name)
                timers |> Seq.iter (fun _ -> timers |> Seq.iter (fun t -> t.Dispose()))
            | _ -> log (fun _ -> sprintf "unexpect message received by __system: %+A" cmd)
        loop [||]


    let mkAcker (topology:Topology<'t>) self (taskMap:TaskMap<'t>) log input = 
        let inFlight = System.Collections.Generic.Dictionary<int64,TreeState>(100)
        let xor tupleId anchor =
            match inFlight.TryGetValue anchor with
            | (true,Pending(sourceId,taskId,v)) -> 
                let v = v ^^^ tupleId
                if v = 0L then Complete(sourceId,taskId) else Pending(sourceId,taskId,v)
            | _ -> Done
        
        let cleanup () =
            inFlight 
            |> Seq.filter (function KeyValue(anchor,Done) -> true | _ -> false) 
            |> Array.ofSeq 
            |> Array.iter (fun (KeyValue(anchor,_))-> inFlight.Remove anchor |> ignore)

        let rec loop () =
            let cmd = input()
            log(fun _ -> sprintf "< %+A" cmd)
            match cmd with
            | InCmd Activate ->
                log(fun _ -> "Starting acker...")
            | InCmd Deactivate ->
                log(fun _ -> "Stopping acker...")
            | AckerCmd (Track (taskId,sid,tupleId)) -> 
                inFlight.Add (tupleId, Pending(sid,taskId,0L))
            | AckerCmd (Anchor (anchor,tupleId)) -> 
                inFlight.[anchor] <- xor tupleId anchor
            | AckerCmd (Fail (anchor,tupleId)) ->
                match inFlight.TryGetValue anchor with
                | true,Pending(sourceId,taskId,_) ->
                    let channel = match taskMap |> Map.find taskId with
                                  | (SpoutTask (_,channel)) -> channel
                                  | _ -> failwithf "unable to find spout channel for the taskId: %A" taskId
                    Nack sourceId |> InCmd |> Enqueue |> channel
                    inFlight.[anchor] <- Done
                | _ -> ()
            | AckerCmd (Ok (anchor,tupleId)) ->
                let xored = xor tupleId anchor
                match xored with
                | Complete(sourceId,taskId) ->
                    let channel = match taskMap |> Map.find taskId with
                                  | (SpoutTask (_,channel)) -> channel
                                  | _ -> failwithf "unable to find spout channel for the taskId: %A" taskId
                    Ack sourceId |> InCmd |> Enqueue |> channel
                    inFlight.[anchor] <- Done
                | _ -> 
                    inFlight.[anchor] <- xored
            | SystemCmd Tick ->
                cleanup()
            | _ -> ()
            loop()
        loop()

    let mkSpout mkEmit compId (comp:Spout<'t>) (topology : Topology<'t>) (runnable:Runnable<'t>) self (tasks:TaskMap<'t>) log input = 
        let conf = comp.Conf |> Map.join topology.Conf
        let emit = mkEmit tasks log
        let pending = ref 0
        let nextMsg = Next |> InCmd
        let next () =
            nextMsg |> self
        let mkThrottle maxPending () =
            if !pending < maxPending then
                next ()
        let maxPending = comp.Conf 
                       |> Map.join topology.Conf 
                       |> Conf.option Conf.TOPOLOGY_MAX_SPOUT_PENDING
        let throttle = maxPending
                       |> Option.map mkThrottle
                       |> Option.defaultValue next
        log(fun _ -> sprintf "Starting %s..." compId)
        let rec input' () =
            let cmd = input() 
#if DEBUG                
            log(fun _ -> sprintf "< %+A" cmd)
#endif                
            match cmd with 
            | SystemCmd Tick ->
                match maxPending with
                | Some mx when !pending < mx -> Next
                | None -> Next
                | _ -> input'()
            | InCmd cmd -> 
                match cmd with
                | Activate -> 
                    throttle()
                | Nack _ | Ack _  -> 
                    Interlocked.Decrement &pending.contents |> ignore
                | _ -> ()
                cmd
            | _ -> input'()

        let output =
            function
            | Emit (t,tupleId,_,stream,dstId,needIds) -> 
                emit struct ([],tupleId,t,compId,stream,dstId)
                Interlocked.Increment &pending.contents |> ignore
            | Error (text,ex) -> log (fun _ -> sprintf "%+A\t%s%+A" LogLevel.Error text ex)
            | Log (text,level) -> log (fun _ -> sprintf "%+A\t%s" level text)
            | Sync -> throttle()
            | cmd -> failwithf "Unexpected command: %+A" cmd
        let io = (input', output)
        runnable io conf

    let mkBolt ackers mkEmit compId (comp:Bolt<'t>) (topology : Topology<'t>) (runnable:Runnable<'t>) self (tasks:TaskMap<'t>) log input = 
        let conf = comp.Conf |> Map.join topology.Conf
        let emit = mkEmit tasks log
        let ack = TupleTree.mkAck Ok ackers
        let nack = TupleTree.mkAck Fail ackers

        log(fun _ -> sprintf "Starting %s..." compId)
        let rec input' () =
            let cmd = input() 
            log(fun _ -> sprintf "< %+A" cmd)
            match cmd with 
            | InCmd cmd -> cmd
            | _ -> input'()

        let output =
            function
            | Emit (t,tupleId,anchors,stream,dstId,needIds) -> emit struct(anchors,tupleId,t,compId,stream,dstId)
            | Error (text,ex) -> log (fun _ -> sprintf "%+A\t%s%+A" LogLevel.Error text ex)
            | Log (text,level) -> log (fun _ -> sprintf "%+A\t%s" level text)
            | OutCommand.Ok tid -> ack tid
            | OutCommand.Fail tid -> nack tid
            | cmd -> failwithf "Unexpected command: %+A" cmd
        let io = (input', output)
        runnable io conf

    let mkTasks (topology:Topology<'t>) : Map<int,RuntimeTask<'t>> =
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
                        let runnable = match b.MkComp (anchorOfStream,b.Activate,b.Deactivate) with FuncRef r -> r | _ -> raiseNotRunnable() 
                        seq { for i in 1..(int b.Parallelism) ->
                                 fun taskId -> 
                                    let mkEmit tasks = 
                                        Routing.mkTupleRouter topology taskId tasks (TupleTree.anchor (TupleTree.mkIdGenerator()) ackers)
                                    let channel = Channel.make (mkBolt ackers mkEmit compId b topology runnable) 
                                    BoltTask (compId, channel)}) 
            yield! topology.Spouts
                   |> Seq.collect (fun (KeyValue(compId,s)) -> 
                        let runnable = match s.MkComp() with FuncRef r -> r | _ -> raiseNotRunnable()
                        seq { for i in 1..(int s.Parallelism) ->
                                fun taskId -> 
                                    let mkEmit tasks = 
                                        Routing.mkTupleRouter topology taskId tasks (TupleTree.track (TupleTree.mkIdGenerator()) ackers taskId)
                                    let channel = Channel.make (mkSpout mkEmit compId s topology runnable)
                                    SpoutTask (compId, channel)})
        }
        |> Seq.map (fun mkTask -> 
                        let taskId = nextTaskId() 
                        taskId, mkTask taskId)
        |> Map.ofSeq


let runWith (startLog:int->Log) (topology:Topology<'t>) =
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
 
