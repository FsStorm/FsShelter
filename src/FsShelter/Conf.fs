namespace FsShelter

/// Storm configuration options
type ConfOption =
    | TOPOLOGY_ENABLE_MESSAGE_TIMEOUTS of bool
    | TOPOLOGY_MESSAGE_TIMEOUT_SECS of int32
    | TOPOLOGY_MAX_SPOUT_PENDING of int32
    | TOPOLOGY_EXECUTOR_RECEIVE_BUFFER_SIZE of int32
    | TOPOLOGY_TICK_TUPLE_FREQ_SECS of int32
    | TOPOLOGY_ACKER_EXECUTORS of int32
    | TOPOLOGY_BACKPRESSURE_ENABLE of bool
    | TOPOLOGY_MAX_TASK_PARALLELISM of int32
    | TOPOLOGY_MULTILANG_SERIALIZER of string
    | TOPOLOGY_DEBUG of bool
    | TOPOLOGY_ACKER_TASKS of int32
    | TOPOLOGY_SLEEP_SPOUT_WAIT_STRATEGY_TIME_MS of int32
    | SUPERVISOR_WORKER_SHUTDOWN_SLEEP_SECS of int32
    | CLUSTER_SEEDS of string
    | CLUSTER_LISTEN of string
    | CLUSTER_HEARTBEAT_MS of int32
    | CLUSTER_SUSPECT_TIMEOUT_MS of int32
    | CLUSTER_QUORUM of int32
    | CLUSTER_STABILIZE_MS of int32
    | CLUSTER_STATE_DIR of string
    | CLUSTER_SEND_QUEUE_BOUND of int32
    | CLUSTER_MAX_FRAME_BYTES of int32
    | Other of string * obj
    
module ConfOption =
    let (|AsTuple|) =
        function
        | TOPOLOGY_ENABLE_MESSAGE_TIMEOUTS v -> "topology.enable.message.timeouts", box v
        | TOPOLOGY_MESSAGE_TIMEOUT_SECS v -> "topology.message.timeout.secs", box v
        | TOPOLOGY_MAX_SPOUT_PENDING v -> "topology.max.spout.pending", box v
        | TOPOLOGY_EXECUTOR_RECEIVE_BUFFER_SIZE v -> "topology.executor.receive.buffer.size", box v
        | TOPOLOGY_TICK_TUPLE_FREQ_SECS v -> "topology.tick.tuple.freq.secs", box v
        | TOPOLOGY_ACKER_EXECUTORS v -> "topology.acker.executors", box v
        | TOPOLOGY_BACKPRESSURE_ENABLE v -> "topology.backpressure.enable", box v
        | TOPOLOGY_MAX_TASK_PARALLELISM v -> "topology.max.task.parallelism", box v
        | TOPOLOGY_MULTILANG_SERIALIZER v -> "topology.multilang.serializer", box v
        | TOPOLOGY_DEBUG v -> "topology.debug", box v
        | TOPOLOGY_ACKER_TASKS v -> "topology.acker.tasks", box v
        | TOPOLOGY_SLEEP_SPOUT_WAIT_STRATEGY_TIME_MS v -> "topology.sleep.spout.wait.strategy.time.ms", box v
        | SUPERVISOR_WORKER_SHUTDOWN_SLEEP_SECS v -> "supervisor.worker.shutdown.sleep.secs", box v
        | CLUSTER_SEEDS v -> "cluster.seeds", box v
        | CLUSTER_LISTEN v -> "cluster.listen", box v
        | CLUSTER_HEARTBEAT_MS v -> "cluster.heartbeat.ms", box v
        | CLUSTER_SUSPECT_TIMEOUT_MS v -> "cluster.suspect.timeout.ms", box v
        | CLUSTER_QUORUM v -> "cluster.quorum", box v
        | CLUSTER_STABILIZE_MS v -> "cluster.stabilize.ms", box v
        | CLUSTER_STATE_DIR v -> "cluster.state.dir", box v
        | CLUSTER_SEND_QUEUE_BOUND v -> "cluster.send.queue.bound", box v
        | CLUSTER_MAX_FRAME_BYTES v -> "cluster.max.frame.bytes", box v
        | Other (k,v) -> k,v

type Conf = Map<string,obj>

[<RequireQualifiedAccess>]
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Conf =
    open ConfOption

    let empty : Conf =
        Map.empty

    let ofList =
        List.map (|AsTuple|) >> Map.ofList

    let toDict conf =
        conf |> Map.toSeq |> Seq.toDict
        
    let Default =
        [ TOPOLOGY_BACKPRESSURE_ENABLE true
          TOPOLOGY_DEBUG false
          TOPOLOGY_ENABLE_MESSAGE_TIMEOUTS true
          TOPOLOGY_ACKER_EXECUTORS 2
          TOPOLOGY_ACKER_TASKS 4
          TOPOLOGY_MAX_SPOUT_PENDING 128
          TOPOLOGY_EXECUTOR_RECEIVE_BUFFER_SIZE 256
          TOPOLOGY_MESSAGE_TIMEOUT_SECS 30
          TOPOLOGY_SLEEP_SPOUT_WAIT_STRATEGY_TIME_MS 100
          CLUSTER_HEARTBEAT_MS 1000
          CLUSTER_SUSPECT_TIMEOUT_MS 5000
          CLUSTER_STABILIZE_MS 2000
          CLUSTER_SEND_QUEUE_BOUND 1024
          CLUSTER_MAX_FRAME_BYTES (256 * 1024) ]
        |> ofList

    let option (mkKey:'arg->ConfOption) conf =
        let def = Unchecked.defaultof<'arg>
        let (AsTuple (key,_)) = mkKey def
        conf 
        |> Map.join Default
        |> Map.tryFind key
        |> Option.map unbox

    let optionOrDefault (mkKey:'arg->ConfOption) conf =
        let def = Unchecked.defaultof<'arg>
        let (AsTuple (key,_)) = mkKey def
        conf 
        |> Map.join Default
        |> Map.find key
        |> unbox

