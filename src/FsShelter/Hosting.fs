module FsShelter.Hosting

open FsShelter.Topology
open FsShelter.Task
open FsShelter.Multilang
open Disruptor.Dsl

[<AutoOpen>]
module internal Types =
    type TaskId = int

    type AnchoredTupleId = (struct (int64 * int64))

    and [<Struct>] TaskMsg<'t,'msg> =
        | Start of rtt:RuntimeTopology<'t>
        | Stop
        | Tick
        | Other of 'msg

    and [<Struct>] AckerMsg =
        | Anchor of aid:AnchoredTupleId
        | Ok of okid:AnchoredTupleId
        | Fail of fid:AnchoredTupleId
        | Track of taid:TaskId * tid:TupleId * int64

    and Send<'t> = 't -> unit
    and Shutdown = unit -> unit
    and Channel<'t> = Send<'t> * Shutdown

    and RuntimeTopology<'t> =
        { systemTask : TaskId * Channel<TaskMsg<'t,unit>>
          ackerTasks : Map<TaskId, Channel<TaskMsg<'t,AckerMsg>>>
          spoutTasks : Map<TaskId, ComponentId * Channel<TaskMsg<'t,InCommand<'t>>>>
          boltTasks : Map<TaskId, ComponentId * Channel<TaskMsg<'t,InCommand<'t>>>> }

    let (|AnchoredTuple|_|) (tupleId:string) =
        match tupleId.Split ':' with
        | [|anchor;tid|] -> Some (int64 anchor, int64 tid)
        | _ -> None
    
    type [<Struct>] TreeState =
        | Pending of TupleId * TaskId * int64
        | Complete of srcId:TupleId * src:TaskId
        | Done

    type Envelope<'msg>() =
        member val Msg:'msg voption = ValueOption.ValueNone with get, set

    and MsgProcessor<'msg> = bool -> 'msg -> unit

module internal Routing =
    let mkTupleRouter mkIds (streams: Map<(StreamId * ComponentId),Stream<'t>>) (boltTasks:Map<TaskId, ComponentId * Channel<TaskMsg<'t,InCommand<'t>>>>) =
        let sinksOfComp =
            let bolts = boltTasks |> Map.groupBy (fun (_,(compId,(_,_))) -> compId) (fun (_,(_,(send,_))) -> send)
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

        let mkDistributors taskId map (KeyValue((streamId,dstId),streamDef)) =
            let f = match map |> Map.tryFind streamId with
                    | Some f -> f
                    | _ -> fun _ -> ignore
            let instances = sinksOfComp dstId
            let group = mkGroup instances streamDef.Grouping
            map |> Map.add streamId (fun mkIds tuple ->
                                        f mkIds tuple
                                        mkIds ()
                                        |> Seq.iter (fun tupleId -> 
                                                        let msg = Tuple(tuple,tupleId,fst streamId,snd streamId,taskId) |> Other
                                                        group tupleId tuple
                                                        |> Seq.apply msg))
        let direct = 
            memoize (fun dstId -> let (_,(send,_)) = boltTasks |> Map.find dstId in Other >> send)

        fun taskId ->
            let distributors =
                streams
                |> Seq.fold (mkDistributors taskId) Map.empty
            
            function
            | (anchors:TupleId list,srcId:TupleId option,tuple:'t,compId,stream,Some dstId) ->
                mkIds anchors srcId ()
                |> List.iter (fun tupleId -> Tuple(tuple,tupleId,compId,stream,taskId) |> direct dstId)
            | (anchors,srcId,tuple,compId,stream,_) ->
                match distributors |> Map.tryFind (compId,stream) with
                | Some d -> tuple |> d (mkIds anchors srcId)
                | _ -> ()

    let inline mkRouter tasks =
        let sinks = 
            tasks 
            |> Seq.map (fun (KeyValue(_,(_,(send,_)))) -> send)
            |> Seq.cache
                            
        fun cmd -> 
            sinks
            |> Seq.apply cmd

    let inline toTask taskId tasks =
        let (_,(send,_)) = tasks |> Map.find taskId in send

    let inline direct (_,(send,_)) = send

module internal TupleTree = 
    open System

    let seed = ref (int DateTime.Now.Ticks)
    let mkIdGenerator() =
        let rnd = Random(Threading.Interlocked.Increment &seed.contents)
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
    
    let inline toAcker ackers anchorId = Other >> (ackerOfAnchor ackers anchorId |> Routing.direct)

    let track nextId ackers taskId _ sourceTupleId =
        let anchorId = nextId()
        let toAcker = toAcker ackers anchorId
        match sourceTupleId with
        | Some sid -> Track(taskId,sid,anchorId) |> toAcker
        | _ -> ()
        fun () ->
            let tupleId = nextId()
            Anchor(struct(anchorId,tupleId)) |> toAcker
            [sprintf "%d:%d" anchorId tupleId]

    let anchor nextId ackers anchors _ =
        let anchors = 
            anchors 
            |> List.choose ((|AnchoredTuple|_|) >> Option.map (fun (a,_) -> a, toAcker ackers a))
        fun () ->
            let tupleId = nextId()
            let anchoredIds = anchors |> List.map (fun a -> (a,tupleId))
            
            anchoredIds 
            |> List.iter (fun ((aid,enqueue),id) ->
                Anchor(struct(aid,id)) |> enqueue)
            
            match anchoredIds with
            | [] -> [string tupleId]
            | _ -> anchoredIds |> List.map (fun ((aid,_),id) -> sprintf "%d:%d" aid id)

    let mkAck toResult ackers = 
        function
        | AnchoredTuple (a,id) -> 
            toResult struct(a,id) |> toAcker ackers a
        | _ -> ()

module internal Channel =
    
    open Disruptor
    open Disruptor.Dsl
    open System.Threading.Tasks

    type MsgHandler<'msg> = Disruptor.IEventHandler<Envelope<'msg>>
    type ExceptionHandler = Disruptor.IExceptionHandler

    let private createDisruptor<'msg> ringSize =
        // default is multi-producer + BlockingWait
        Dsl.Disruptor<Envelope<'msg>>(System.Func<Envelope<'msg>>(Envelope),ringSize,TaskScheduler.Default)

    let private publish (ringBuffer: RingBuffer<Envelope<'msg>>) (msg: 'msg) =
        let seqno = ringBuffer.Next()
        let entry = ringBuffer.[seqno]
        entry.Msg <- ValueSome msg
        ringBuffer.Publish seqno

    let private withHandler (f: MsgProcessor<'msg>) (disruptor:Disruptor<_>) =
        { new MsgHandler<'msg> with
            member __.OnNext(ev: Envelope<'msg>, seqno, eob) =
                match ev.Msg with | ValueSome msg -> msg |> f eob | _ -> () }
        |> disruptor.HandleEventsWith

    let private withCleanup (group:EventHandlerGroup<Envelope<'msg>>) =
        group.Then [|
            { new MsgHandler<'msg> with
                member __.OnNext(ev: Envelope<'msg>, seqno, eob) =
                    ev.Msg <- ValueNone }|]
        

    let private withExceptionHandler (f:exn->unit) (disruptor:Disruptor<_>) =
        { new ExceptionHandler with
            member __.HandleEventException(exn:exn, seqno, eob) = f exn
            member __.HandleOnStartException(exn:exn) = f exn
            member __.HandleOnShutdownException(exn:exn) = f exn }
        |> disruptor.HandleExceptionsWith
        disruptor

    let start ringSize onException onData =
        let disruptor = createDisruptor ringSize
        disruptor
        |> withExceptionHandler onException
        |> withHandler onData
        // |> withCleanup
        |> ignore
        let ringBuffer = disruptor.Start()
        publish ringBuffer, disruptor.Shutdown

module internal RuntimeTopology =
    open System
    open System.Threading
    let s n = 1000 * n
    let m n = 60 * (s n)


    let private mkAcker log (topology:Topology<'t>) = 
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

        let mutable sendToSpout = fun _ -> failwith "The acker is not started."
        fun _ cmd ->
            match cmd with
            | Start rtt ->
                log(fun _ -> "Starting acker...")
                sendToSpout <- fun sid -> let (send,_) = rtt.spoutTasks |> Map.find sid |> snd in Other >> send
            | Stop ->
                log(fun _ -> "Stopping acker...")
            | Tick ->
                cleanup()
            | Other(Track (taskId,sid,tupleId)) -> 
                inFlight.Add (tupleId, Pending(sid,taskId,0L))
            | Other(Anchor (struct(anchor,tupleId))) -> 
                inFlight.[anchor] <- xor tupleId anchor
            | Other(Fail (struct(anchor,tupleId))) ->
                match inFlight.TryGetValue anchor with
                | true,Pending(sourceId,taskId,_) ->
                    Nack sourceId |> sendToSpout taskId
                    inFlight.[anchor] <- Done
                | _ -> ()
            | Other(Ok (struct(anchor,tupleId))) ->
                let xored = xor tupleId anchor
                match xored with
                | Complete(sourceId,taskId) ->
                    Ack sourceId |> sendToSpout taskId
                    inFlight.[anchor] <- Done
                | _ -> 
                    inFlight.[anchor] <- xored

    let private mkSystem log nextId (topology:Topology<'t>) taskId = 
        let tick bolts dstId = 
            let tuple = TupleSchema.mkTick()
            let route = Other >> Routing.mkRouter (bolts |> Seq.where  (fun (KeyValue(_,(compId,_))) -> compId = dstId))
            match tuple with
            | Some t -> fun _ -> InCommand.Tuple(t, (string <| nextId()), "__system", "__tick", taskId) |> route
            | _ -> failwith "Topology schema doesn't define \"__tick\" tuple"
        let systemTick spouts = 
            let route = Routing.mkRouter spouts
            fun _ -> route Tick
        let startTimers (rtt:RuntimeTopology<'t>) =
            topology.Bolts
            |> Seq.choose (fun (KeyValue(compId,b)) -> 
                    b.Conf 
                    |> Conf.option Conf.TOPOLOGY_TICK_TUPLE_FREQ_SECS 
                    |> Option.map (fun tf -> compId, tf))
            |> Seq.map (fun (compId,timeout) -> 
                    log(fun _ -> sprintf "Starting timer with timeout %ds for: %s" timeout compId)
                    new Timer(TimerCallback(tick rtt.boltTasks compId), (), s timeout, s timeout)
                    :> IDisposable)
            |> Seq.append
            <| seq { 
                yield new Timer(TimerCallback(systemTick rtt.spoutTasks), (), s 30, s 30) :> IDisposable 
            }
        
        let mutable timers = [||]
        fun _ ->
            function
            | Start rtt ->
                log(fun _ -> sprintf "Starting system for topology %s..." topology.Name)
                timers <- startTimers rtt |> Seq.toArray
            | Stop ->
                log(fun _ -> sprintf "Stopping system for topology: %s" topology.Name)
                timers |> Array.iter (fun _ -> timers |> Seq.iter (fun t -> t.Dispose()))
            | cmd ->
                log(fun _ -> sprintf "Unsupported command for system task: %A" cmd)

    let private mkSpout log compId (comp:Spout<'t>) (topology:Topology<'t>) taskId (runnable:Runnable<'t>) = 
        let next = Other Next
        let mutable pending = 0
        let inline throttled maxPending self =
            if pending < maxPending then // TODO: Interlocked?
                next |> self
        let inline unthrottled self = next |> self
        let conf = comp.Conf |> Map.join topology.Conf
        let maxPending = comp.Conf 
                       |> Map.join topology.Conf 
                       |> Conf.option Conf.TOPOLOGY_MAX_SPOUT_PENDING
        let throttle = maxPending |> Option.map throttled |> Option.defaultValue unthrottled

        let mkOutput rtt issueNext =
            let ackers = rtt.ackerTasks |> Map.toArray
            let mkIds = TupleTree.track (TupleTree.mkIdGenerator()) ackers taskId
            let emit = Routing.mkTupleRouter mkIds topology.Streams rtt.boltTasks taskId
            function
            | Emit (t,tupleId,_,stream,dstId,needIds) -> 
                emit ([],tupleId,t,compId,stream,dstId)
                Interlocked.Increment &pending |> ignore
            | Error (text,ex) -> log (fun _ -> sprintf "%+A\t%s%+A" LogLevel.Error text ex)
            | Log (text,level) -> log (fun _ -> sprintf "%+A\t%s" level text)
            | Sync -> issueNext()
            | cmd -> failwithf "Unexpected command: %+A" cmd


        let dispatcher = runnable conf
        let mutable issueNext = ignore
        let mutable out = ignore
        fun eob (cmd:TaskMsg<'t,InCommand<'t>>) ->
            match cmd with
            | Tick ->
                ()
            | Start rtt -> 
                let (_,(self,_)) = rtt.spoutTasks |> Map.find taskId
                issueNext <- fun _ -> throttle self
                out <- mkOutput rtt issueNext
                log(fun _ -> sprintf "Starting %s..." compId)
                dispatcher out InCommand.Activate
            | Stop -> 
                issueNext <- ignore
                out <- ignore
                dispatcher out InCommand.Deactivate
            | Other msg ->
#if DEBUG                
                log(fun _ -> sprintf "< %+A" msg)
#endif
                match msg with
                | Ack _ | Nack _ -> Interlocked.Decrement &pending |> ignore
                | _ -> ()
                dispatcher out msg
            if eob then issueNext()
            
    let mkBolt log compId (comp:Bolt<'t>) (topology:Topology<'t>) taskId (runnable:Runnable<'t>) = 
        let conf = comp.Conf |> Map.join topology.Conf

        let mkOutput (rtt:RuntimeTopology<'t>) =
            let ackers = rtt.ackerTasks |> Map.toArray
            let mkIds = TupleTree.anchor (TupleTree.mkIdGenerator()) ackers
            let emit = Routing.mkTupleRouter mkIds topology.Streams rtt.boltTasks taskId
            let ack = TupleTree.mkAck Ok ackers
            let nack = TupleTree.mkAck Fail ackers
            function
            | Emit (t,tupleId,anchors,stream,dstId,needIds) -> emit (anchors,tupleId,t,compId,stream,dstId)
            | Error (text,ex) -> log (fun _ -> sprintf "%+A\t%s%+A" LogLevel.Error text ex)
            | Log (text,level) -> log (fun _ -> sprintf "%+A\t%s" level text)
            | OutCommand.Ok tid -> ack tid
            | OutCommand.Fail tid -> nack tid
            | cmd -> failwithf "Unexpected command: %+A" cmd

        let dispatcher = runnable conf
        let mutable out = ignore

        fun _ ->
            function
            | Start rtt ->        
                log(fun _ -> sprintf "Starting %s..." compId)
                out <- mkOutput rtt 
                dispatcher out InCommand.Activate
            | Stop ->
                log(fun _ -> sprintf "Stopping %s..." compId)
                dispatcher out InCommand.Deactivate
            | Other msg -> 
#if DEBUG                
                log(fun _ -> sprintf "< %+A" msg)
#endif
                dispatcher out msg
            | cmd -> 
                failwithf "Usupported command for a bolt: %A" cmd

    let ofTopology startLog onException (topology:Topology<'t>) : RuntimeTopology<'t> =
        let anchorOfStream = topology.Anchors.TryFind >> Option.defaultValue (fun _ -> [])
        let raiseNotRunnable compId = failwithf "%s: Only native FsShelter components can be hosted" compId
        let ackersCount = topology.Conf |> Conf.optionOrDefault Conf.TOPOLOGY_ACKER_EXECUTORS
        let ringSize = topology.Conf |> Conf.optionOrDefault Conf.TOPOLOGY_EXECUTOR_RECEIVE_BUFFER_SIZE
        let nextTaskId =
            let s = (Seq.initInfinite id).GetEnumerator()
            fun _ -> s.MoveNext() |> ignore; s.Current
        let system taskId =
            taskId, 
            mkSystem (startLog taskId) (TupleTree.mkIdGenerator()) topology taskId |> Channel.start 16 onException

        let ackers = [| for i in 1..ackersCount -> 
                            fun taskId ->
                                taskId,
                                topology |> mkAcker (startLog taskId) |> Channel.start (ringSize*2) onException |]
        let bolts = topology.Bolts
                   |> Seq.collect (fun (KeyValue(compId,b)) -> 
                        let runnable = match b.MkComp (anchorOfStream,b.Activate,b.Deactivate) with FuncRef r -> r | _ -> raiseNotRunnable compId
                        seq { for i in 1..(int b.Parallelism) ->
                                 fun taskId -> 
                                    let channel = runnable |> mkBolt (startLog taskId) compId b topology taskId |> Channel.start ringSize onException
                                    taskId, (compId, channel)})
        let spouts = topology.Spouts
                   |> Seq.collect (fun (KeyValue(compId,s)) -> 
                        let runnable = match s.MkComp() with FuncRef r -> r | _ -> raiseNotRunnable compId
                        seq { for i in 1..(int s.Parallelism) ->
                                 fun taskId -> 
                                    let channel = runnable |> mkSpout (startLog taskId) compId s topology taskId |> Channel.start ringSize onException
                                    taskId, (compId, channel)})
        
        { systemTask = nextTaskId() |> system
          ackerTasks = ackers |> Seq.map (fun a -> nextTaskId() |> a ) |> Map.ofSeq 
          spoutTasks = spouts |> Seq.map (fun s -> nextTaskId() |> s ) |> Map.ofSeq
          boltTasks = bolts |> Seq.map (fun b -> nextTaskId() |> b ) |> Map.ofSeq }

let runWith (startLog:int->Log) (topology:Topology<'t>) =
    let log = System.Diagnostics.Process.GetCurrentProcess().Id |> startLog 
    let describe (rtt:RuntimeTopology<'t>) =
        seq {
            yield sprintf "\n\t%d (__system)" (fst rtt.systemTask)
            for (KeyValue(taskId,_)) in rtt.ackerTasks do
                yield sprintf "\n\t%d (__acker)" taskId
            for (KeyValue(taskId,(compId,_))) in rtt.spoutTasks do
                yield sprintf "\n\t%d: %s (%+A)" taskId compId (topology.Spouts |> Map.find compId).Conf
            for (KeyValue(taskId,(compId,_))) in rtt.boltTasks do
                yield sprintf "\n\t%d: %s (%+A)" taskId compId (topology.Bolts |> Map.find compId).Conf
        } |> Seq.fold (+) ""

    let activate rtt =
        rtt.ackerTasks |> Map.iter (fun _ (send,_) -> send (Start rtt))
        rtt.boltTasks |> Map.iter (fun _ (_,(send,_)) -> send (Start rtt))
        rtt.spoutTasks |> Map.iter (fun _ (_,(send,_)) -> send (Start rtt))
        let (_,(send,_)) = rtt.systemTask in send (Start rtt)

    let stop rtt =
        let (_,(send,_)) = rtt.systemTask in send Stop
        rtt.spoutTasks |> Map.iter (fun _ (_,(send,_)) -> send Stop)
        rtt.boltTasks |> Map.iter (fun _ (_,(send,_)) -> send Stop)
        rtt.ackerTasks |> Map.iter (fun _ (send,_) -> send Stop)

    let shutdown rtt =
        let (_,(_,shutdown)) = rtt.systemTask in shutdown()
        rtt.spoutTasks |> Map.iter (fun _ (_,(_,shutdown)) -> shutdown())
        rtt.boltTasks |> Map.iter (fun _ (_,(_,shutdown)) -> shutdown())
        rtt.ackerTasks |> Map.iter (fun _ (_,shutdown) -> shutdown())

    let mutable rtt = None
    let rec restart (ex:exn) =
        log(fun _ -> sprintf "Error running the topology: %A,\n restarting..." ex) 
        stopAndShutdown ()
        start ()
    and start () =
        rtt <- Some (topology |> RuntimeTopology.ofTopology startLog restart)
        log(fun _ -> sprintf "Hosting the topology: %s {%s},\n with configuration: %A" topology.Name (describe rtt.Value) topology.Conf)
        System.Threading.Thread.Sleep 1000 //TODO: Read a Conf option
        activate rtt.Value
    and stopAndShutdown () =
        match rtt with
        | Some rtt ->
            stop rtt
            System.Threading.Thread.Sleep 1000 //TODO: Read a Conf option
            shutdown rtt
        | _ -> ()

    start ()
    stopAndShutdown

let run topology = runWith (fun _ -> ignore) topology    