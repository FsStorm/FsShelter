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

    type Deliver<'t> = TupleId list -> string -> 't -> TupleId option -> TaskId option -> unit

    type Ack = TupleId -> unit

    type Nack = TupleId -> unit

    type Throttle = (unit->unit) -> (int->int) -> unit

    type TaskMap<'t> = Map<TaskId,RuntimeTask<'t>>

    and AckerCmd =
        | Anchor of AnchoredTupleId
        | Ok of AnchoredTupleId
        | Fail of AnchoredTupleId
        | Track of TaskId * TupleId * int64

    and TaskInput<'t> =
        | PublicCmd of InCommand<'t>
        | AckerCmd of AckerCmd

    and InternalIO<'t> = (unit -> Async<TaskInput<'t>>) * (OutCommand<'t> -> unit)

    and Channel<'t> = TaskControl<'t> -> unit

    and RuntimeTask<'t> =
        | SystemTask of Channel<'t>
        | AckerTask of Channel<'t>
        | SpoutTask of ComponentId * Channel<'t>
        | BoltTask of ComponentId * Channel<'t>

    and Loop<'t> = TaskMap<'t> -> InternalIO<'t> -> Async<unit>

    and TaskControl<'t> =
        | Enqueue of TaskInput<'t>
        | Dequeue of AsyncReplyChannel<TaskInput<'t>>
        | Handle of OutCommand<'t>
        | Stop
        | Start of TaskMap<'t> * Deliver<'t> * Ack * Nack * Throttle

    let (|AnchoredTuple|_|) (tupleId:string) =
        match tupleId.Split (':') with
        | [|anchor;tid|] -> Some (int64 anchor, int64 tid)
        | _ -> None

module internal Tasks =
    let mkChannel log (dispatch:Loop<_>) =
        let mkIO (agent:MailboxProcessor<_>) : InternalIO<_> =
            (fun () -> agent.PostAndAsyncReply Dequeue), 
             Handle >> agent.Post

        let run map io token = 
            let loop =
                dispatch map io
                |> Async.Catch 
                |> Async.map (function | Choice2Of2 ex -> log (fun _ -> sprintf "FATAL: %A" ex)
                                       | _ -> log (fun _ -> "Finished")) 
            Async.Start (loop,token)

        let inbox = MailboxProcessor.Start(fun mb ->
            let queue = System.Collections.Generic.Queue<TaskInput<_>>()
            let next () = Next |> PublicCmd |> queue.Enqueue
            let control =
                function
                | Enqueue (PublicCmd (Nack id))
                | Enqueue (PublicCmd (Ack id)) -> 
                    fun x -> x - 1
                | Handle (Emit (_,Some _,_,_,_,_)) ->
                    fun x -> x + 1
                | _ -> id
            let rec start () =
                async {
                    let! msg = mb.Receive()
                    let ts = new CancellationTokenSource()
                    match msg with
                    | Start (map, publish, ack, nack, throttle) -> 
                        run map (mkIO mb) ts.Token
                        Activate |> PublicCmd |> queue.Enqueue
                        do! Async.Sleep (1) // TODO: Read a Conf option
                        id |> throttle next
                        return! loop publish ack nack (control >> throttle next) ts
                    | Stop -> return! shutdown ts
                }
            
            and loop send ack nack throttle ts =
                async {
                    let! msg = mb.Receive()
                    let ts = new CancellationTokenSource()
                    match msg with
                    | Enqueue cmd -> queue.Enqueue cmd
                    | Dequeue rc -> if queue.Count > 0 then rc.Reply (queue.Dequeue())
                    | Handle(Emit (t,tupleId,anchors,stream,dstId,needIds)) -> send anchors stream t tupleId dstId
                    | Handle(Error (text,ex)) -> log (fun _ -> sprintf "%A\t%s%A" LogLevel.Error text ex)
                    | Handle(Log (text,level)) -> log (fun _ -> sprintf "%A\t%s" level text)
                    | Handle(OutCommand.Ok tid) -> ack tid
                    | Handle(OutCommand.Fail tid) -> nack tid
                    | Handle _ -> ()
                    | Stop -> return! shutdown ts
                    msg |> throttle
                    return! loop send ack nack throttle ts
                }

            and shutdown ts =
                async {
                    Deactivate |> PublicCmd |> queue.Enqueue
                    do! Async.Sleep (1) // TODO: Read a Conf option
                    ts.Cancel false
                }
            start ()
        )
        inbox.Post

    let tasksOfComp taskMap =
        let compTasks = 
            taskMap 
            |> Map.groupBy (fun (key,value) -> 
                            match value with
                            | SpoutTask (compId,_)
                            | BoltTask (compId,_) -> Some compId
                            | _ -> None)
        fun compId -> compTasks |> Map.find (Some compId)

    let mkSystem (topology : Topology<'t>) (taskMap:TaskMap<'t>) (in', out') = 
        let tick = TupleSchema.mkTick()
        let describe taskId =
            function
            | SystemTask _ -> "__system"
            | AckerTask _ -> "__acker"
            | SpoutTask (compId,_) -> compId
            | BoltTask (compId,_) -> sprintf "%s (%A)" compId (topology.Bolts |> Map.find compId).Conf
        let startTimers =
            topology.Bolts
            |> Seq.choose (fun (KeyValue(compId,b)) -> 
                    b.Conf 
                    |> Conf.option Conf.TOPOLOGY_TICK_TUPLE_FREQ_SECS 
                    |> Option.map (fun tf -> compId, tf))
            |> Seq.map (fun (compId,timeout) -> 
                    Log(sprintf "Started timer with timeout %ds for: %A" timeout compId, LogLevel.Info) |> out'
                    fun tasksOfComp ->
                        let emit _= 
                            Log(sprintf "Tick for: %A" compId, LogLevel.Info) |> out'
                            tasksOfComp compId
                            |> Seq.iter (fun taskId -> Emit(tick.Value, None, List.empty, "__tick", Some taskId, None) |> out')
                        let timer = new System.Threading.Timer(
                                        emit,
                                        (),
                                        (1000 * timeout),
                                        (1000 * timeout))
                        timer :> IDisposable)

        let rec loop timers =
            async {
                let! cmd = in'()
                match cmd with
                | PublicCmd Activate ->
                    Log(sprintf "Starting system for topology %s: %A" topology.Name (taskMap |> Map.map describe), LogLevel.Info) |> out'
                    return! startTimers |> Seq.map (fun mkTimer -> mkTimer (tasksOfComp taskMap)) |> Seq.toArray |> loop
                | PublicCmd Deactivate -> 
                    Log(sprintf "Stopping system for topology: %s" topology.Name, LogLevel.Info) |> out'
                    timers |> Seq.iter (fun _ -> timers |> Seq.iter (fun t -> t.Dispose()))
                    Log (sprintf "unexpect message received by system: %A" cmd, LogLevel.Error) |> out' 
                | _ -> Log (sprintf "unexpect message received by system: %A" cmd, LogLevel.Error) |> out' 
            }
        loop [||]

    [<Struct>]
    type TreeState =
        | Pending of TupleId*TaskId*int64
        | Complete

    let mkAcker (topology : Topology<'t>) (taskMap:TaskMap<'t>) (in', out') = 
        let inFlight = System.Collections.Generic.Dictionary<int64,TreeState>(1000)
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
                match taskMap |> Map.tryFind taskId with
                | Some (SpoutTask (_,channel)) -> 
                    cont sourceId |> PublicCmd |> Enqueue |> channel
                | other ->
                    Log(sprintf "unexpected task %A for anchor: %d" other anchor, LogLevel.Error) |> out'
            | _ -> ()

        let rec loop () =
            async {
                let! cmd = in'()
                match cmd with
                | PublicCmd Activate ->
                    Log("Starting acker...", LogLevel.Info) |> out'
                | PublicCmd Deactivate ->
                    Log("Stopping acker...", LogLevel.Info) |> out'
                | AckerCmd (Track (taskId,sid,tupleId)) -> 
                    inFlight.[tupleId] <- Pending(sid,taskId,tupleId)
                | AckerCmd (Anchor (anchor,tupleId)) -> 
                    inFlight.[anchor] <- xor tupleId anchor
                | AckerCmd (Fail (anchor,tupleId)) ->
                    check tupleId anchor Nack
                | AckerCmd (Ok (anchor,tupleId)) ->
                    check tupleId anchor Ack
                | _ -> ()
                return! loop()
            }
        loop()

    let mkRunnable compId conf (runnable:Runnable<'t>) (taskMap:TaskMap<'t>) (in', out') = 
        async {
            Log(sprintf "Starting %s..." compId, LogLevel.Info) |> out'
            let io = (in' >> Async.map (function PublicCmd cmd -> cmd), out')
            return! runnable io conf
        }

    let mkTasks (startLog : int->Log) (topology : Topology<'t>) : Map<int,RuntimeTask<'t>> =
        let anchor = topology.Anchors.TryFind
                         >> function
                            | Some toAnchor -> toAnchor 
                            | None -> fun _ -> []

        let raiseNotRunnable() = failwith "Only native FsShelter components can be hosted"

        let nextTaskId =
            let mutable taskId = 0
            fun () ->
                taskId <- taskId + 1
                taskId
        
        let ackersCount = 
            topology.Conf 
            |> Conf.optionOrDefault Conf.TOPOLOGY_ACKER_EXECUTORS

        seq { 
            yield (fun mkChannel -> 
                        let channel = mkChannel (mkSystem topology)
                        SystemTask channel)

            yield! (fun mkChannel -> 
                        let channel = mkChannel (mkAcker topology)
                        AckerTask channel)
                   |> Seq.replicate ackersCount

            yield! topology.Spouts
                   |> Seq.collect (fun (KeyValue(compId,s)) -> 
                        (fun mkChannel ->
                            let conf = s.Conf |> Map.join topology.Conf
                            let runnable = match s.MkComp() with FuncRef r -> r | _ -> raiseNotRunnable()
                            let channel = mkChannel (mkRunnable compId conf runnable)
                            SpoutTask (compId, channel))
                        |> Seq.replicate (int s.Parallelism))
            
            yield! topology.Bolts
                   |> Seq.collect (fun (KeyValue(compId,b)) -> 
                        (fun mkChannel  ->
                            let conf = b.Conf |> Map.join topology.Conf
                            let runnable = match b.MkComp anchor with FuncRef r -> r | _ -> raiseNotRunnable() 
                            let channel = mkChannel (mkRunnable compId conf runnable) 
                            BoltTask (compId, channel))
                        |> Seq.replicate (int b.Parallelism)) 
        }
        |> Seq.map (fun mkTask -> 
                        let taskId = nextTaskId()
                        let log = startLog taskId
                        taskId,mkTask (mkChannel log))
        |> Map.ofSeq

module internal Post =
    let inline findAcker (ackers:_ array) (anchorId:int64) =
        let i = anchorId % (int64 ackers.Length)
        ackers.[int i]

    let mkPublish (tasks:Map<TaskId,RuntimeTask<'t>>) (topology : Topology<'t>) =
        let sinks =
            tasks
            |> Seq.map (fun (KeyValue(taskId,task)) -> 
                match task with
                | BoltTask (compId,channel) -> Some (compId,channel)
                | _ -> None)
            |> Seq.choose id
            |> Seq.groupBy (fun (compId,channel) -> compId)
            |> Seq.map (fun (k,v) -> k,v |> Seq.map snd |> Seq.toArray)
            |> Map.ofSeq
        
        let group (instances:_ array) =
            function
            | All -> 
                fun _ _ -> instances :> _ seq
            | Shuffle -> 
                let ix tupleId = tupleId.GetHashCode() % instances.Length
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
                let all = sinks |> Map.find dstId
                let group = group all stream.Grouping
                map |> Map.add streamId (fun tupleId tuple -> f tupleId tuple |> Seq.append (group tupleId tuple))) Map.empty
        
        fun stream srcId tupleId tuple msg ->
            channels 
            |> Map.find (srcId,stream)
            |> fun f -> f tupleId tuple |> Seq.iter (fun channel -> channel (Enqueue msg))

    let nextId =
        let max = System.Int64.MaxValue |> float;
        let rnd = Random()
        fun () -> rnd.NextDouble() * max |> int64

    let direct =
        function
        | SystemTask (channel)
        | AckerTask (channel)
        | SpoutTask (_,channel)
        | BoltTask (_,channel) -> Enqueue >> channel

    let track ackers tasks publish srcId taskId _ stream tuple sourceTupleId dstId =
        let tupleId = nextId()
        match sourceTupleId with
        | Some sid -> Track(taskId,sid,tupleId) |> AckerCmd |> (findAcker ackers tupleId |> direct)
        | _ -> ()
        let internalTupleId = sprintf "%d:%d" tupleId tupleId
        let enqueue = 
            match dstId with
            | Some dst -> tasks |> Map.find dst |> direct
            | _ -> publish stream srcId internalTupleId tuple

        Tuple(tuple, internalTupleId, srcId, stream, taskId) |> PublicCmd |> enqueue


    let send tasks publish srcId taskId tupleIds stream tuple _ dstId =
        let enqueue tupleId = 
            match dstId with
            | Some dst -> tasks |> Map.find dst |> direct
            | _ -> publish stream srcId tupleId tuple
        tupleIds 
        |> List.iter (fun tupleId -> Tuple(tuple, tupleId, srcId, stream, taskId) |> PublicCmd |> enqueue tupleId)

    let newId _ =
        let tupleId = nextId()
        [string tupleId]

    let anchor ackers anchors =
        let tupleId = nextId()
        let anchoredIds =
            anchors 
            |> List.choose (|AnchoredTuple|_|)
            |> List.map (fun (a,_) -> (a,tupleId))
        
        anchoredIds 
        |> List.iter (fun (a,id) ->
            let enqueue = findAcker ackers a |> direct
            Anchor(a,id) |> AckerCmd |> enqueue)
        
        match anchoredIds with
        | [] -> [string tupleId]
        | _ -> anchoredIds |> List.map (fun (a,id) -> sprintf "%d:%d" a id)

    let ack ackers = 
        function
        | AnchoredTuple (a,id) -> 
            let enqueue = findAcker ackers a |> direct
            Ok(a,id) |> AckerCmd |> enqueue
        | _ -> ()

    let nack ackers = 
        function
        | AnchoredTuple (a,id) -> 
            let enqueue = findAcker ackers a |> direct
            Fail(a,id) |> AckerCmd |> enqueue
        | _ -> ()

    let mkThrottle maxPending =
        let mutable pending = 0
        let check cont change =
            pending <- change pending
            if pending < maxPending then cont ()
        check

    let noThrottle _ = ignore

open Post
let runWith (startLog : int->Log) (topology : Topology<'t>) =
    let tasks = Tasks.mkTasks startLog topology
    let publish = mkPublish tasks topology

    let start =
        let ackers = 
            tasks 
            |> Seq.choose (function (KeyValue(taskId,AckerTask control)) -> Some (AckerTask control) | _ -> None )
            |> Array.ofSeq
        fun taskId ->
            function
            | SystemTask channel -> 
                Start (tasks, newId >> send tasks publish "" taskId, ignore, ignore, noThrottle) |> channel
            | AckerTask channel -> 
                Start (tasks, newId >> send tasks publish "" taskId, ignore, ignore, noThrottle) |> channel
            | SpoutTask (compId,channel) -> 
                Start (tasks, track ackers tasks publish compId taskId, ignore, ignore, mkThrottle 100) |> channel // TODO: Read a Conf option
            | BoltTask (compId,channel) -> 
                Start (tasks, anchor ackers >> send tasks publish compId taskId, ack ackers, nack ackers, noThrottle) |> channel

    let stop _ =
        function
        | SystemTask channel
        | AckerTask channel
        | SpoutTask (_,channel)
        | BoltTask (_,channel) -> Stop |> channel

    tasks |> Map.iter start

    fun () -> tasks |> Map.iter stop

 
