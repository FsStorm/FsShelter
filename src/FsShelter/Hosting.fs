module FsShelter.Hosting

open FsShelter.Topology
open FsShelter.Multilang
open Disruptor.Dsl
open System.Threading
open System.Diagnostics
open System.Diagnostics.Metrics

module Telemetry =
    let internal activitySource = new ActivitySource("FsShelter.Hosting")
    let internal meter = new Meter("FsShelter.Hosting")
    let internal tuplesEmitted = meter.CreateCounter<int64>("fsshelter.tuples.emitted", description = "Number of tuples emitted by spouts")
    let internal tuplesAcked = meter.CreateCounter<int64>("fsshelter.tuples.acked", description = "Number of tuples acked")
    let internal tuplesNacked = meter.CreateCounter<int64>("fsshelter.tuples.nacked", description = "Number of tuples nacked")
    let internal tuplesExpired = meter.CreateCounter<int64>("fsshelter.tuples.expired", description = "Number of tuples expired by acker rotation")
    let internal processingTime = meter.CreateHistogram<float>("fsshelter.task.processing_time", unit = "ms", description = "Time to process a single message")
    let internal pendingCounter = meter.CreateUpDownCounter<int>("fsshelter.spout.pending", description = "Number of pending spout tuples")

[<AutoOpen>]
module internal Types =
    type TaskId = int

    type AnchoredTupleId = System.ValueTuple<int64, int64>

    and [<Struct>] TaskMsg<'t, 'msg> =
        | Start of rtt:RuntimeTopology<'t>
        | Stop
        | Tick
        | Other of msg:'msg

    and AckerMsg =
        | Anchor of aid:AnchoredTupleId
        | Ok of okid:AnchoredTupleId
        | Fail of fid:AnchoredTupleId
        | Track of taid:TaskId * tid:TupleId * int64

    and Send<'t> = 't -> unit
    and Shutdown = unit -> unit
    and Channel<'t> = Send<'t> * Shutdown

    and RuntimeTopology<'t> =
        { systemTask : TaskId * Channel<TaskMsg<'t, unit>>
          ackerTasks : Map<TaskId, Channel<TaskMsg<'t, AckerMsg>>>
          spoutTasks : Map<TaskId, ComponentId * Channel<TaskMsg<'t, InCommand<'t>>>>
          boltTasks : Map<TaskId, ComponentId * Channel<TaskMsg<'t, InCommand<'t>>>> }

    type TreeState =
        | Pending of TupleId * TaskId * int64 * int64
        | Complete of srcId:TupleId * src:TaskId
        | Done

    type Envelope<'msg>() =
        member val Msg:'msg voption = ValueOption.ValueNone with get, set
        member val ParentContext:ActivityContext voption = ValueNone with get, set
        member val TaskId:TaskId = 0 with get, set

    and MsgProcessor<'msg> = 'msg -> bool -> unit

module internal Routing =
    let mkTupleRouter mkIds (streams: Map<(StreamId * ComponentId), Stream<'t>>) (boltTasks:Map<TaskId, ComponentId * Channel<TaskMsg<'t, InCommand<'t>>>>) =
        let sinksOfComp =
            let bolts = boltTasks |> Map.groupBy (fun (_, (compId, (_, _))) -> compId) (fun (_, (_, (send, _))) -> send)
            memoize (fun compId -> bolts.[compId] |> Array.ofSeq)
        
        let mkGroup (instances:_ array) =
            function
            | All -> 
                fun _ _ msg -> for i = 0 to instances.Length - 1 do instances.[i] msg
            | Shuffle when instances.Length = 1 -> 
                fun _ _ msg -> instances.[0] msg
            | Shuffle -> 
                let ix tupleId = abs(tupleId.GetHashCode() % instances.Length)
                fun tupleId _ msg -> instances.[ix tupleId] msg
            | Direct -> 
                fun _ _ _ -> ()
            | Fields (map, _) -> 
                fun _ tuple msg ->
                    let ix = (map tuple).GetHashCode() % instances.Length |> abs
                    instances.[ix] msg

        let mkDistributors taskId map (KeyValue((streamId, dstId), streamDef)) =
            let f = match map |> Map.tryFind streamId with
                    | Some f -> f
                    | _ -> fun _ -> ignore
            let instances = sinksOfComp dstId
            let group = mkGroup instances streamDef.Grouping
            map |> Map.add streamId (fun mkIds tuple ->
                                        let ids = mkIds ()
                                        f mkIds tuple
                                        ids |> Seq.iter (fun tupleId -> 
                                                        let msg = Tuple(tuple, tupleId, fst streamId, snd streamId, taskId) |> Other
                                                        group tupleId tuple msg))
        let direct = 
            memoize (fun dstId -> let (_, (send, _)) = boltTasks |> Map.find dstId in Other >> send)

        fun taskId ->
            let distributors =
                streams
                |> Seq.fold (mkDistributors taskId) Map.empty
                |> Map.toSeq
                |> dict
            
            function
            | (anchors:TupleId list, srcId:TupleId option, tuple:'t, compId, stream, Some dstId) ->
                mkIds anchors srcId ()
                |> List.iter (fun tupleId -> Tuple(tuple, tupleId, compId, stream, taskId) |> direct dstId)
            | (anchors, srcId, tuple, compId, stream, _) ->
                match distributors.TryGetValue((compId, stream)) with
                | true, d -> tuple |> d (mkIds anchors srcId)
                | _ -> ()

    let mkRouter tasks sendMap =
        let sinks = 
            tasks 
            |> Seq.map sendMap
            |> Seq.toArray
                            
        fun cmd -> 
            for i = 0 to sinks.Length - 1 do sinks.[i] cmd

    let toTask taskId tasks =
        let (_, (send, _)) = tasks |> Map.find taskId in send

    let inline direct (_, (send, _)) = send

module internal TupleTree = 
    open System
    
    let seed = ref (int DateTime.Now.Ticks)
    let mkIdGenerator() =
        let rnd = Random(Threading.Interlocked.Increment &seed.contents)
        let rec nextId () =
            let v = rnd.NextInt64()
            if v = 0L then nextId()
            else v
        nextId

    let inline ackerOfAnchor (ackers:_ array) (anchorId:int64) =
        let i = abs (anchorId % (int64 ackers.Length))
        ackers.[int i]
    
    let inline toAcker ackers anchorId = Other >> (ackerOfAnchor ackers anchorId |> Routing.direct)

    let track nextId ackers taskId _ sourceTupleId =
        let anchorId = nextId()
        let toAcker = toAcker ackers anchorId
        match sourceTupleId with
        | Some sid -> Track(taskId, sid, anchorId) |> toAcker
        | _ -> ()
        fun () ->
            let tupleId = nextId()
            Anchor(struct(anchorId, tupleId)) |> toAcker
            [Anchored(anchorId, tupleId)]

    let anchor nextId ackers anchors _ =
        let anchors = 
            anchors 
            |> List.choose (fun (tid: TupleId) ->
                match tid with
                | Anchored(a, _) -> Some(a, toAcker ackers a)
                | _ -> None)
        fun () ->
            let tupleId = nextId()
            let anchoredIds = anchors |> List.map (fun a -> (a, tupleId))
            
            anchoredIds 
            |> List.iter (fun ((aid, enqueue), id) -> Anchor(struct(aid, id)) |> enqueue)
            
            match anchoredIds with
            | [] -> [Unanchored tupleId]
            | _ -> anchoredIds |> List.map (fun ((aid, _), id) -> Anchored(aid, id))

    let mkAck toResult ackers = 
        fun (tid: TupleId) ->
            match tid with
            | Anchored(a, id) -> toResult struct(a, id) |> toAcker ackers a
            | _ -> ()

module internal Channel =
    
    open Disruptor
    open Disruptor.Dsl
    open System.Threading.Tasks

    type MsgHandler<'msg> = Disruptor.IEventHandler<Envelope<'msg>>

    let private publish (ringBuffer: RingBuffer<Envelope<'msg>>) (msg: 'msg) =
        let seqno = ringBuffer.Next()
        let entry = ringBuffer.[seqno]
        entry.Msg <- ValueSome msg
        let current = Activity.Current
        entry.ParentContext <- if isNull current then ValueNone else ValueSome current.Context
        ringBuffer.Publish seqno

    let private restoreActivity (parentCtx: ActivityContext voption) (name: string) =
        match parentCtx with
        | ValueSome ctx -> Telemetry.activitySource.StartActivity(name, ActivityKind.Internal, ctx)
        | ValueNone -> null

    let private withHandlers (onMsg: MsgProcessor<'msg voption>) (onExn: exn->unit) (disruptor: Disruptor<_>) =
        { new IExceptionHandler<Envelope<'msg>> with
            member __.HandleEventException(exn:exn, seqno:int64, eob:Envelope<'msg>) = onExn exn
            member __.HandleEventException(exn:exn, seqno:int64, batch:EventBatch<Envelope<'msg>>) = onExn exn
            member __.HandleOnTimeoutException(exn:exn, seqno:int64) = onExn exn
            member __.HandleOnStartException(exn:exn) = onExn exn
            member __.HandleOnShutdownException(exn:exn) = onExn exn }
        |> disruptor.SetDefaultExceptionHandler
        { new MsgHandler<'msg> with
            member __.OnEvent(ev, _, endOfBatch) =
                let activity = restoreActivity ev.ParentContext "channel.process"
                try onMsg ev.Msg endOfBatch
                finally if not (isNull activity) then activity.Dispose()
            member __.OnTimeout(_) = onMsg ValueNone true }
        |> disruptor.HandleEventsWith
        |> ignore

    let startWithTimeout ringSize waitTimeoutMs onException onData =
        let disruptor = Disruptor<Envelope<'msg>>(System.Func<Envelope<'msg>> Envelope,
                                                  ringSize,
                                                  TaskScheduler.Default,
                                                  ProducerType.Multi,
                                                  TimeoutBlockingWaitStrategy(System.TimeSpan.FromMilliseconds(float waitTimeoutMs)))
        disruptor
        |> withHandlers onData onException
        let ringBuffer = disruptor.Start()
        publish ringBuffer, disruptor.Halt

    let start ringSize onException onData =
        let disruptor = Disruptor<Envelope<'msg>>(System.Func<Envelope<'msg>> Envelope,
                                                  ringSize,
                                                  TaskScheduler.Default)
        disruptor
        |> withHandlers (fun msg _endOfBatch -> ValueOption.iter onData msg) onException
        let ringBuffer = disruptor.Start()
        publish ringBuffer, disruptor.Halt

    let private publishWithTaskId (ringBuffer: RingBuffer<Envelope<'msg>>) (taskId: TaskId) (msg: 'msg) =
        let seqno = ringBuffer.Next()
        let entry = ringBuffer.[seqno]
        entry.Msg <- ValueSome msg
        entry.TaskId <- taskId
        let current = Activity.Current
        entry.ParentContext <- if isNull current then ValueNone else ValueSome current.Context
        ringBuffer.Publish seqno

    let private withExecutorHandlers (onEvent: Envelope<'msg> -> bool -> unit) (onTimeout: unit -> unit) (onExn: exn->unit) (disruptor: Disruptor<_>) =
        { new IExceptionHandler<Envelope<'msg>> with
            member __.HandleEventException(exn:exn, seqno:int64, eob:Envelope<'msg>) = onExn exn
            member __.HandleEventException(exn:exn, seqno:int64, batch:EventBatch<Envelope<'msg>>) = onExn exn
            member __.HandleOnTimeoutException(exn:exn, seqno:int64) = onExn exn
            member __.HandleOnStartException(exn:exn) = onExn exn
            member __.HandleOnShutdownException(exn:exn) = onExn exn }
        |> disruptor.SetDefaultExceptionHandler
        { new MsgHandler<'msg> with
            member __.OnEvent(ev, _, endOfBatch) =
                let activity = restoreActivity ev.ParentContext "channel.process"
                try onEvent ev endOfBatch
                finally if not (isNull activity) then activity.Dispose()
            member __.OnTimeout(_) = onTimeout() }
        |> disruptor.HandleEventsWith
        |> ignore

    let startExecutor ringSize onException (tasks: (TaskId * ('msg -> bool -> unit)) array) =
        let handlers = tasks |> Array.map (fun (_, h) -> h)
        let taskIndex = System.Collections.Generic.Dictionary<TaskId, int>(tasks.Length)
        for i = 0 to tasks.Length - 1 do
            let (tid, _) = tasks.[i]
            taskIndex.[tid] <- i
        let disruptor = Disruptor<Envelope<'msg>>(System.Func<Envelope<'msg>> Envelope,
                                                  ringSize,
                                                  TaskScheduler.Default)
        disruptor
        |> withExecutorHandlers
            (fun ev endOfBatch ->
                match ev.Msg with
                | ValueSome m -> handlers.[taskIndex.[ev.TaskId]] m endOfBatch
                | ValueNone -> ())
            ignore
            onException
        let ringBuffer = disruptor.Start()
        let send (taskId: TaskId) : Send<'msg> =
            fun msg -> publishWithTaskId ringBuffer taskId msg
        send, disruptor.Halt

    let startExecutorWithTimeout ringSize waitTimeoutMs onException (tasks: (TaskId * ('msg voption -> bool -> unit)) array) onTimeout =
        let handlers = tasks |> Array.map (fun (_, h) -> h)
        let taskIndex = System.Collections.Generic.Dictionary<TaskId, int>(tasks.Length)
        for i = 0 to tasks.Length - 1 do
            let (tid, _) = tasks.[i]
            taskIndex.[tid] <- i
        let disruptor = Disruptor<Envelope<'msg>>(System.Func<Envelope<'msg>> Envelope,
                                                  ringSize,
                                                  TaskScheduler.Default,
                                                  ProducerType.Multi,
                                                  TimeoutBlockingWaitStrategy(System.TimeSpan.FromMilliseconds(float waitTimeoutMs)))
        disruptor
        |> withExecutorHandlers
            (fun ev endOfBatch ->
                match ev.Msg with
                | ValueSome m -> handlers.[taskIndex.[ev.TaskId]] (ValueSome m) endOfBatch
                | ValueNone -> ())
            onTimeout
            onException
        let ringBuffer = disruptor.Start()
        let send (taskId: TaskId) : Send<'msg> =
            fun msg -> publishWithTaskId ringBuffer taskId msg
        send, disruptor.Halt

module internal RuntimeTopology =
    open System
    open System.Diagnostics

    let s n = 1000 * n
    let m n = 60 * (s n)

    let private mkAcker log (highWater:int) (topology:Topology<'t>) = 
        let numBuckets = 3
        let buckets = Array.init numBuckets (fun _ -> System.Collections.Generic.Dictionary<int64, TreeState>(highWater / numBuckets))
        let mutable currentBucket = 0
        let trace = if topology.Conf |> Conf.optionOrDefault TOPOLOGY_DEBUG then log LogLevel.Trace else ignore
        let mutable sendToSpout = fun (_:TaskId) (_:InCommand<'t>) -> failwith "The acker is not started."

        let tryFind anchor =
            let mutable result = ValueNone
            let mutable i = currentBucket
            let mutable count = 0
            while count < numBuckets && result.IsNone do
                match buckets.[i].TryGetValue anchor with
                | true, v -> result <- ValueSome (i, v)
                | _ -> ()
                i <- (i + numBuckets - 1) % numBuckets
                count <- count + 1
            result

        let xor tupleId anchor =
            match tryFind anchor with
            | ValueSome (i, Pending(sourceId, taskId, v, ts)) ->
                let v = v ^^^ tupleId
                let state = if v = 0L then Complete(sourceId, taskId) else Pending(sourceId, taskId, v, ts)
                ValueSome (i, state)
            | _ -> ValueNone

        let totalCount () = buckets |> Array.sumBy (fun b -> b.Count)

        let rotate () =
            let oldestBucket = (currentBucket + 1) % numBuckets
            let expired = buckets.[oldestBucket]
            let mutable expiredCount = 0
            for (KeyValue(_, state)) in expired do
                match state with
                | Pending(sourceId, taskId, _, _) ->
                    trace (fun _ -> sprintf "Expiring anchor in rotating bucket")
                    sendToSpout taskId (Nack sourceId)
                    expiredCount <- expiredCount + 1
                | _ -> ()
            if expiredCount > 0 then Telemetry.tuplesExpired.Add(int64 expiredCount)
            expired.Clear()
            currentBucket <- oldestBucket

        let handler = function
            | Start rtt ->
                log LogLevel.Debug (fun _ -> "Starting acker...")
                sendToSpout <- fun sid ->
                    let (send, _) = rtt.spoutTasks |> Map.find sid |> snd
                    fun msg -> send (Other msg : TaskMsg<'t, _>)
            | Stop ->
                log LogLevel.Debug (fun _ -> "Stopping acker...")
            | Tick ->
                let count = totalCount()
                trace (fun _ -> sprintf "InFlight: %d" count)
                rotate()
            | Other(Track (taskId, sid, tupleId)) -> 
                let count = totalCount()
                if count > highWater * 2 then
                    log LogLevel.Warn (fun _ -> sprintf "Acker capacity exceeded (%d), nacking tuple" count)
                    sendToSpout taskId (Nack sid)
                else
                    buckets.[currentBucket].Add(tupleId, Pending(sid, taskId, 0L, 0L))
            | Other(Anchor(struct(anchor, tupleId))) -> 
                trace (fun _ -> sprintf "Anchoring: %A <- %A" anchor tupleId)
                match xor tupleId anchor with
                | ValueSome (i, state) -> buckets.[i].[anchor] <- state
                | ValueNone -> ()
            | Other(Fail(struct(anchor, _))) ->
                match tryFind anchor with
                | ValueSome (i, Pending(sourceId, taskId, _, _)) ->
                    trace (fun _ -> sprintf "Failing anchor: %A" anchor)
                    sendToSpout taskId (Nack sourceId)
                    Telemetry.tuplesNacked.Add(1L)
                    buckets.[i].Remove anchor |> ignore
                | _ ->
                    trace (fun _ -> sprintf "Can't nack, not tracking: %A" anchor)
            | Other(Ok(struct(anchor, tupleId))) ->
                match xor tupleId anchor with
                | ValueSome (i, Complete(sourceId, taskId)) ->
                    trace (fun _ -> sprintf "Acking anchor: %A due to %A" anchor tupleId)
                    sendToSpout taskId (Ack sourceId)
                    Telemetry.tuplesAcked.Add(1L)
                    buckets.[i].Remove anchor |> ignore
                | ValueSome (i, state) -> 
                    trace (fun _ -> sprintf "xoring anchor: %A with %A" anchor tupleId)
                    buckets.[i].[anchor] <- state
                | ValueNone -> ()
        handler

    let private mkSystem log nextId (topology:Topology<'t>) taskId = 
        let tick bolts dstId = 
            let tuple = TupleSchema.mkTick()
            let route = Other >> Routing.mkRouter (bolts |> Seq.where (fun (KeyValue(_, (compId, _))) -> compId = dstId)) (fun (KeyValue(_, (_, (send, _)))) -> send)
            match tuple with
            | Some t -> fun _ -> InCommand.Tuple(t, Unanchored(nextId()), "__system", "__tick", taskId) |> route
            | _ -> failwith "Topology schema doesn't define \"__tick\" tuple"
        let systemTick tasks sendMap = 
            let route = Routing.mkRouter tasks sendMap
            fun _ -> route Tick
        let startTimers (rtt:RuntimeTopology<'t>) =
            topology.Bolts
            |> Seq.choose (fun (KeyValue(compId, b)) -> 
                    b.Conf 
                    |> Conf.option ConfOption.TOPOLOGY_TICK_TUPLE_FREQ_SECS 
                    |> Option.map (fun tf -> compId, tf))
            |> Seq.map (fun (compId, timeout) -> 
                    log LogLevel.Debug (fun _ -> sprintf "Starting timer with timeout %ds for: %s" timeout compId)
                    new Timer(TimerCallback(tick rtt.boltTasks compId), (), s timeout, s timeout)
                    :> IDisposable)
            |> Seq.append
            <| seq { 
                let messageTimeout : int = topology.Conf |> Conf.optionOrDefault TOPOLOGY_MESSAGE_TIMEOUT_SECS
                let ackerTickSecs = max 1 (messageTimeout / 2)
                let spoutTick = systemTick rtt.spoutTasks (fun (KeyValue(_, (_, (send, _)))) -> send)
                let ackerTick = systemTick rtt.ackerTasks (fun (KeyValue(_, (send, _))) -> send)
                yield new Timer(TimerCallback(spoutTick >> ackerTick), (), s ackerTickSecs, s ackerTickSecs) :> IDisposable 
            }
        
        let mutable timers = [||]
        function
        | Start rtt ->
            log LogLevel.Info (fun _ -> sprintf "Starting system for topology %s..." topology.Name)
            timers <- startTimers rtt |> Seq.toArray
        | Stop ->
            log LogLevel.Info (fun _ -> sprintf "Stopping system for topology: %s" topology.Name)
            timers |> Array.iter (fun t -> t.Dispose())
        | cmd ->
            log LogLevel.Error (fun _ -> sprintf "Unsupported command for system task: %A" cmd)
     
    let private mkSpout log compId (comp:Spout<'t>) (topology:Topology<'t>) taskId (runnable:Runnable<'t>) = 
        let conf = comp.Conf |> Map.join topology.Conf
        let maxPending : int = conf |> Conf.optionOrDefault TOPOLOGY_MAX_SPOUT_PENDING
        let debug = conf |> Conf.optionOrDefault TOPOLOGY_DEBUG 
        let trace = if debug then log LogLevel.Trace else ignore
        let mutable pending = 0

        let mkOutput rtt =
            let ackers = rtt.ackerTasks |> Map.toArray
            let mkIds = TupleTree.track (TupleTree.mkIdGenerator()) ackers taskId
            let emit = Routing.mkTupleRouter mkIds topology.Streams rtt.boltTasks taskId
            function
            | Emit (t, tupleId, _, stream, dstId, needIds) -> 
                let tuple = (List.empty, tupleId, t, compId, stream, dstId)
                trace (fun _ -> sprintf "> %+A" tuple)
                emit tuple
                Interlocked.Increment &pending |> ignore 
                Telemetry.tuplesEmitted.Add(1L)
                Telemetry.pendingCounter.Add(1)
            | Error (text, ex) -> log LogLevel.Error (fun _ -> sprintf "%s%+A" text ex)
            | Log (text, level) -> log level (fun _ -> text)
            | Sync -> ()
            | cmd -> failwithf "Unexpected command from a spout: %+A" cmd

        let mutable dispatcher = ignore
        let mutable issueNext = ignore
        let inline dispatchNext () =
            if pending < maxPending then
                dispatcher InCommand.Next
        let mutable tagList = TagList()
        tagList.Add("component.id", compId)
        tagList.Add("task.id", taskId)
        let handler (msg: TaskMsg<'t, InCommand<'t>>) =
            match msg with
            | Tick -> ()
            | Start rtt -> 
                let output = mkOutput rtt
                issueNext <- fun _ -> dispatchNext()
                log LogLevel.Info (fun _ -> sprintf "Starting %s..." compId)
                let dispatch = runnable conf output
                dispatcher <- (fun msg ->
                    let start = Stopwatch.GetTimestamp()
                    let activity = Telemetry.activitySource.StartActivity("spout.emit", ActivityKind.Producer)
                    if not (isNull activity) then
                        activity.SetTag("component.id", compId) |> ignore
                        activity.SetTag("task.id", taskId) |> ignore
                    try
                        if debug then trace (fun _ -> sprintf "< %+A" msg)
                        dispatch msg
                    finally
                        if not (isNull activity) then activity.Dispose()
                        Telemetry.processingTime.Record(Stopwatch.GetElapsedTime(start).TotalMilliseconds, &tagList))
                dispatcher InCommand.Activate
                for _ in 1..maxPending do issueNext()
            | Stop -> 
                dispatcher InCommand.Deactivate
                issueNext <- ignore
                dispatcher <- ignore
            | Other msg ->
                match msg with
                | Ack _ | Nack _ ->
                    Interlocked.Decrement &pending |> ignore
                    Telemetry.pendingCounter.Add(-1)
                    issueNext()
                    dispatcher msg
                | _ -> dispatcher msg
        let onTimeout () = issueNext()
        handler, onTimeout
            
    let mkBolt log compId (comp:Bolt<'t>) (topology:Topology<'t>) taskId (runnable:Runnable<'t>) = 
        let conf = comp.Conf |> Map.join topology.Conf
        let debug = conf |> Conf.optionOrDefault TOPOLOGY_DEBUG
        let trace = if debug then log LogLevel.Trace else ignore

        let mkOutput (rtt:RuntimeTopology<'t>) =
            let ackers = rtt.ackerTasks |> Map.toArray
            let mkIds = TupleTree.anchor (TupleTree.mkIdGenerator()) ackers
            let emit = Routing.mkTupleRouter mkIds topology.Streams rtt.boltTasks taskId
            let ack = TupleTree.mkAck Ok ackers
            let nack = TupleTree.mkAck Fail ackers
            function
            | Emit (t, tupleId, anchors, stream, dstId, needIds) -> 
                let tuple = (anchors, tupleId, t, compId, stream, dstId)
                trace (fun _ -> sprintf "> %+A" tuple)
                emit tuple
            | Error (text, ex) -> log LogLevel.Error (fun _ -> sprintf "%s%+A" text ex)
            | Log (text, level) -> log level (fun _ -> sprintf "%s" text)
            | OutCommand.Ok tid -> ack tid
            | OutCommand.Fail tid -> nack tid
            | cmd -> failwithf "Unexpected command: %+A" cmd

        let mutable dispatcher = ignore
        let mutable tagList = TagList()
        tagList.Add("component.id", compId)
        tagList.Add("task.id", taskId)

        let handler (msg: TaskMsg<'t, InCommand<'t>>) =
            match msg with
            | Start rtt ->        
                log LogLevel.Info (fun _ -> sprintf "Starting %s..." compId)
                let output = mkOutput rtt
                let dispatch = runnable conf output
                dispatcher <- (fun msg ->
                    let start = Stopwatch.GetTimestamp()
                    let activity = Telemetry.activitySource.StartActivity("bolt.process", ActivityKind.Consumer)
                    if not (isNull activity) then
                        activity.SetTag("component.id", compId) |> ignore
                        activity.SetTag("task.id", taskId) |> ignore
                    try
                        dispatch msg
                    finally
                        if not (isNull activity) then activity.Dispose()
                        Telemetry.processingTime.Record(Stopwatch.GetElapsedTime(start).TotalMilliseconds, &tagList))
                dispatcher InCommand.Activate
            | Stop ->
                log LogLevel.Info (fun _ -> sprintf "Stopping %s..." compId)
                dispatcher InCommand.Deactivate
                dispatcher <- ignore
            | Other msg -> 
                trace (fun _ -> sprintf "< %+A" msg)
                dispatcher msg
            | cmd -> 
                failwithf "Unsupported command for a bolt: %A" cmd
        handler

    let private mkAckerExecutor startLog (highWater:int) (topology:Topology<'t>) (taskIds:TaskId array) =
        taskIds |> Array.map (fun taskId ->
            let handler = topology |> mkAcker (startLog taskId) highWater
            (taskId, (fun msg _endOfBatch -> handler msg)))

    let private mkBoltExecutor startLog compId (comp:Bolt<'t>) (topology:Topology<'t>) (taskIds:TaskId array) (runnable:Runnable<'t>) =
        taskIds |> Array.map (fun taskId ->
            let handler = runnable |> mkBolt (startLog taskId) compId comp topology taskId
            (taskId, (fun msg _endOfBatch -> handler msg)))

    let private mkSpoutExecutor startLog compId (comp:Spout<'t>) (topology:Topology<'t>) (taskIds:TaskId array) (runnable:Runnable<'t>) =
        let taskEntries = taskIds |> Array.map (fun taskId ->
            let (handler, onTimeout) = runnable |> mkSpout (startLog taskId) compId comp topology taskId
            let wrappedHandler (msg: TaskMsg<'t, InCommand<'t>> voption) (endOfBatch: bool) =
                match msg with
                | ValueSome m -> handler m
                | ValueNone -> onTimeout()
            (taskId, wrappedHandler, onTimeout))
        let tasks = taskEntries |> Array.map (fun (tid, h, _) -> (tid, h))
        let onTimeouts = taskEntries |> Array.map (fun (_, _, ot) -> ot)
        let combinedTimeout () = for ot in onTimeouts do ot()
        tasks, combinedTimeout

    let private groupIntoExecutors (executorCount: int) (items: 'a array) : 'a array array =
        let groups = Array.init executorCount (fun _ -> ResizeArray())
        for i = 0 to items.Length - 1 do
            groups.[i % executorCount].Add(items.[i])
        groups |> Array.map (fun g -> g.ToArray())

    let ofTopology startLog onException (topology:Topology<'t>) : RuntimeTopology<'t> =
        let anchorOfStream = topology.Anchors.TryFind >> Option.defaultValue (fun _ -> [])
        let raiseNotRunnable compId = failwithf "%s: Only native FsShelter components can be hosted" compId
        let ackerExecutorCount = topology.Conf |> Conf.optionOrDefault ConfOption.TOPOLOGY_ACKER_EXECUTORS
        let ackerTaskCount = topology.Conf |> Conf.optionOrDefault ConfOption.TOPOLOGY_ACKER_TASKS
        let ringSize = topology.Conf |> Conf.optionOrDefault ConfOption.TOPOLOGY_EXECUTOR_RECEIVE_BUFFER_SIZE
        let spoutWaitMs : int = topology.Conf |> Conf.optionOrDefault ConfOption.TOPOLOGY_SLEEP_SPOUT_WAIT_STRATEGY_TIME_MS
        let nextTaskId =
            let s = (Seq.initInfinite id).GetEnumerator()
            fun _ -> s.MoveNext() |> ignore; s.Current
        let system taskId =
            taskId, 
            mkSystem (startLog taskId) (TupleTree.mkIdGenerator()) topology taskId |> Channel.start 16 onException

        // Ackers: ackerTaskCount tasks grouped into ackerExecutorCount executors
        let ackerTaskIds = [| for _ in 1..ackerTaskCount -> nextTaskId() |]
        let ackerGroups = groupIntoExecutors ackerExecutorCount ackerTaskIds
        let ackerTasks =
            ackerGroups |> Array.collect (fun groupTaskIds ->
                let tasks = mkAckerExecutor startLog ringSize topology groupTaskIds
                let executorRingSize = ringSize * groupTaskIds.Length
                let (send, halt) = Channel.startExecutor executorRingSize onException tasks
                groupTaskIds |> Array.map (fun tid -> tid, (send tid, halt)))
            |> Map.ofArray

        // Bolts: Parallelism tasks, grouped into Executors executors (default: 1:1)
        let boltTasks =
            topology.Bolts
            |> Seq.collect (fun (KeyValue(compId, b)) ->
                let runnable = match b.MkComp (anchorOfStream, b.Activate, b.Deactivate) with FuncRef r -> r | _ -> raiseNotRunnable compId
                let taskCount = int b.Parallelism
                let executorCount = b.Executors |> Option.map int |> Option.defaultValue taskCount |> min taskCount
                let boltTaskIds = [| for _ in 1..taskCount -> nextTaskId() |]
                let boltGroups = groupIntoExecutors executorCount boltTaskIds
                boltGroups |> Array.collect (fun groupTaskIds ->
                    let tasks = mkBoltExecutor startLog compId b topology groupTaskIds runnable
                    let executorRingSize = ringSize * groupTaskIds.Length
                    let (send, halt) = Channel.startExecutor executorRingSize onException tasks
                    groupTaskIds |> Array.map (fun tid -> tid, (compId, (send tid, halt)))))
            |> Map.ofSeq

        // Spouts: Parallelism tasks, grouped into Executors executors (default: 1:1)
        let spoutTasks =
            topology.Spouts
            |> Seq.collect (fun (KeyValue(compId, s)) ->
                let runnable = match s.MkComp() with FuncRef r -> r | _ -> raiseNotRunnable compId
                let taskCount = int s.Parallelism
                let executorCount = s.Executors |> Option.map int |> Option.defaultValue taskCount |> min taskCount
                let spoutTaskIds = [| for _ in 1..taskCount -> nextTaskId() |]
                let spoutGroups = groupIntoExecutors executorCount spoutTaskIds
                spoutGroups |> Array.collect (fun groupTaskIds ->
                    let (tasks, combinedTimeout) = mkSpoutExecutor startLog compId s topology groupTaskIds runnable
                    let executorRingSize = ringSize * groupTaskIds.Length
                    let (send, halt) = Channel.startExecutorWithTimeout executorRingSize spoutWaitMs onException tasks combinedTimeout
                    groupTaskIds |> Array.map (fun tid -> tid, (compId, (send tid, halt)))))
            |> Map.ofSeq
        
        { systemTask = nextTaskId() |> system
          ackerTasks = ackerTasks
          spoutTasks = spoutTasks
          boltTasks = boltTasks }

    let describe (topology:Topology<'t>) (rtt:RuntimeTopology<'t>) =
        let ackerExecutorCount = topology.Conf |> Conf.optionOrDefault ConfOption.TOPOLOGY_ACKER_EXECUTORS
        let ackerTaskCount = rtt.ackerTasks.Count
        seq {
            yield sprintf "\n\t%d (__system)" (fst rtt.systemTask)
            yield sprintf "\n\t__acker: %d tasks on %d executors" ackerTaskCount ackerExecutorCount
            for (KeyValue(taskId, _)) in rtt.ackerTasks do
                yield sprintf "\n\t  %d (__acker)" taskId
            for (KeyValue(taskId, (compId, _))) in rtt.spoutTasks do
                let s = topology.Spouts |> Map.find compId
                let executorCount = s.Executors |> Option.map int |> Option.defaultValue (int s.Parallelism) |> min (int s.Parallelism)
                yield sprintf "\n\t%d: %s (%d tasks on %d executors, %+A)" taskId compId (int s.Parallelism) executorCount s.Conf
            for (KeyValue(taskId, (compId, _))) in rtt.boltTasks do
                let b = topology.Bolts |> Map.find compId
                let executorCount = b.Executors |> Option.map int |> Option.defaultValue (int b.Parallelism) |> min (int b.Parallelism)
                yield sprintf "\n\t%d: %s (%d tasks on %d executors, %+A)" taskId compId (int b.Parallelism) executorCount b.Conf
        } |> Seq.fold (+) ""

    let activate rtt =
        rtt.ackerTasks |> Map.iter (fun _ (send, _) -> send (Start rtt))
        rtt.boltTasks |> Map.iter (fun _ (_, (send, _)) -> send (Start rtt))
        rtt.spoutTasks |> Map.iter (fun _ (_, (send, _)) -> send (Start rtt))
        let (_, (send, _)) = rtt.systemTask in send (Start rtt)

    let stop timeout rtt =
        // 1. Stop system (timers) first
        let (_, (send, _)) = rtt.systemTask in send Stop
        // 2. Stop spouts (stop producing new tuples)
        rtt.spoutTasks |> Map.iter (fun _ (_, (send, _)) -> send Stop)
        // 3. Wait for in-flight tuples to drain through the DAG
        Thread.Sleep (1000 * timeout)
        // 4. Stop bolts
        rtt.boltTasks |> Map.iter (fun _ (_, (send, _)) -> send Stop)
        // 5. Stop ackers last
        rtt.ackerTasks |> Map.iter (fun _ (send, _) -> send Stop)

    let shutdown rtt =
        let halted = System.Collections.Generic.HashSet<Shutdown>()
        let haltOnce (halt: Shutdown) = if halted.Add(halt) then halt()
        let (_, (_, shutdown)) = rtt.systemTask in shutdown()
        rtt.spoutTasks |> Map.iter (fun _ (_, (_, halt)) -> haltOnce halt)
        rtt.boltTasks |> Map.iter (fun _ (_, (_, halt)) -> haltOnce halt)
        rtt.ackerTasks |> Map.iter (fun _ (_, halt) -> haltOnce halt)

let private shutdownTimeout (conf:Conf) =
    conf
    |> Conf.option SUPERVISOR_WORKER_SHUTDOWN_SLEEP_SECS
    |> Option.defaultWith (fun () -> conf |> Conf.optionOrDefault TOPOLOGY_MESSAGE_TIMEOUT_SECS)

let runNoRestart (startLog:int->Log) (topology:Topology<'t>) =
    let log = System.Diagnostics.Process.GetCurrentProcess().Id |> startLog 
    let timeout = shutdownTimeout topology.Conf
    let exit (exn: exn) =
        log LogLevel.Error (fun _ -> sprintf "Unhandled exception: {%A}" exn)
        System.Environment.Exit 1

    let rtt = (topology |> RuntimeTopology.ofTopology startLog exit)
    log LogLevel.Info (fun _ -> sprintf "Hosting the topology: %s {%s}, \n with configuration: %A" topology.Name (RuntimeTopology.describe topology rtt) topology.Conf)
    RuntimeTopology.activate rtt

    fun () ->
        log LogLevel.Info (fun _ -> sprintf "Stopping topology: %s..." topology.Name) 
        RuntimeTopology.stop timeout rtt
        RuntimeTopology.shutdown rtt

let runWith (startLog:int->Log) (topology:Topology<'t>) =
    let log = System.Diagnostics.Process.GetCurrentProcess().Id |> startLog 

    let timeout = shutdownTimeout topology.Conf
    let maxRestarts = 5
    let sync = obj()
    let mutable rtt : RuntimeTopology<'t> option = None
    let mutable restartCount = 0
    let rec restart (ex:exn) =
        if Monitor.TryEnter sync then
            try
                try
                    log LogLevel.Error (fun _ -> sprintf "Error running the topology: %A" ex)
                    if restartCount >= maxRestarts then
                        log LogLevel.Error (fun _ -> sprintf "Max restarts (%d) exceeded, giving up" maxRestarts)
                    else
                        restartCount <- restartCount + 1
                        let backoffMs = min (1000 * (1 <<< restartCount)) 30000
                        log LogLevel.Info (fun _ -> sprintf "Restarting in %dms (attempt %d/%d)..." backoffMs restartCount maxRestarts)
                        Thread.Sleep backoffMs
                        stopAndShutdown ()
                        start ()
                with restartEx ->
                    log LogLevel.Error (fun _ -> sprintf "Error restarting the topology: %A" restartEx)
            finally
                Monitor.Exit sync
        else
            log LogLevel.Warn (fun _ -> sprintf "Restart already in progress, ignoring error: %A" ex)
    and start () =
        let r = topology |> RuntimeTopology.ofTopology startLog restart
        rtt <- Some r
        restartCount <- 0
        log LogLevel.Info (fun _ -> sprintf "Hosting the topology: %s {%s}, \n with configuration: %A" topology.Name (RuntimeTopology.describe topology r) topology.Conf)
        RuntimeTopology.activate r
    and stopAndShutdown () =
        match rtt with
        | Some r ->
            rtt <- None
            log LogLevel.Info (fun _ -> sprintf "Stopping topology: %s..." topology.Name) 
            RuntimeTopology.stop timeout r
            RuntimeTopology.shutdown r
        | _ -> ()

    start ()
    stopAndShutdown

let run topology = runWith (fun _ _ -> ignore) topology    