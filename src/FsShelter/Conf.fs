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
          TOPOLOGY_MAX_SPOUT_PENDING 128
          TOPOLOGY_EXECUTOR_RECEIVE_BUFFER_SIZE 256
          TOPOLOGY_MESSAGE_TIMEOUT_SECS 30 ]
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

