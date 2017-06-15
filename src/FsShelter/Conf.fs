namespace FsShelter

/// Storm configuration map
type Conf = Map<string,obj>

[<RequireQualifiedAccess>]
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Conf =

    [<Literal>]
    let TOPOLOGY_ENABLE_MESSAGE_TIMEOUTS = "topology.enable.message.timeouts"

    [<Literal>]
    let TOPOLOGY_MESSAGE_TIMEOUT_SECS = "topology.message.timeout.secs"

    [<Literal>]
    let TOPOLOGY_MAX_SPOUT_PENDING = "topology.max.spout.pending"

    [<Literal>]
    let TOPOLOGY_TICK_TUPLE_FREQ_SECS = "topology.tick.tuple.freq.secs"

    [<Literal>]
    let TOPOLOGY_ACKER_EXECUTORS = "topology.acker.executors"

    [<Literal>]
    let TOPOLOGY_BACKPRESSURE_ENABLE = "topology.backpressure.enable"

    [<Literal>]
    let TOPOLOGY_MAX_TASK_PARALLELISM = "topology.max.task.parallelism"

    [<Literal>]
    let TOPOLOGY_MULTILANG_SERIALIZER = "topology.multilang.serializer"

    [<Literal>]
    let TOPOLOGY_DEBUG = "topology.debug"

    let empty : Conf =
        Map.empty

    let ofList =
        Map.ofList

    let toDict conf =
        conf |> Map.toSeq |> Seq.toDict
        
    let Default =
        [ TOPOLOGY_BACKPRESSURE_ENABLE, box true
          TOPOLOGY_ENABLE_MESSAGE_TIMEOUTS, box true
          TOPOLOGY_ACKER_EXECUTORS, box 1
          TOPOLOGY_MAX_TASK_PARALLELISM, box 1
          TOPOLOGY_MESSAGE_TIMEOUT_SECS, box 30 ]
        |> Map.ofList

    let option key conf =
        let conf = Default |> Map.join conf
        conf 
        |> Map.tryFind key
        |> Option.map unbox

    let optionOrDefault key conf =
        let conf = Default |> Map.join conf
        conf 
        |> Map.find key
        |> unbox

