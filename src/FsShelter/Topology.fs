namespace FsShelter

/// Topology data model
module Topology =
    open Multilang

    /// Tuple id
    type TupleId = string
    /// Component id
    type ComponentId = string
    /// Stream id
    type StreamId = ComponentId*string
    /// Signature for anchoring implementation
    type ToAnchors = TupleId->TupleId list
    /// Signature for pluggable IO implementation
    type IO<'t> = (unit->Async<InCommand<'t>>)*(OutCommand<'t>->unit)
    /// Signature for a final runnable component
    type Runnable<'t> = IO<'t>->Conf->Async<unit>

    /// Storm Componend abstraction
    type Component<'t> = 
        | FuncRef of Runnable<'t>
        | Shell of command : string * args : string
        | Java of className : string * args : string list

    /// Storm Spout abstraction
    type Spout<'t> = {
        MkComp:unit->Component<'t>
        Parallelism:uint32
        Conf:Conf 
    } with static member WithConf (s,conf) = {s with Conf = conf}
           static member WithParallelism (s,p) = {s with Parallelism = p}

    /// Storm Bolt abstraction
    type Bolt<'t> = {
        MkComp:(StreamId->ToAnchors)->Component<'t>
        Parallelism:uint32
        Conf:Conf 
    } with static member WithConf (s,conf) = {s with Bolt.Conf = conf}
           static member WithParallelism (s,p) = {s with Bolt.Parallelism = p}

    /// Storm stream grouping abstraction
    type Grouping<'t> = 
        | Shuffle
        | Fields of getValue:('t->obj) * names:string list
        | All
        | Direct

    /// Storm Stream abstraction
    type Stream<'t> = {
        Src:ComponentId
        Dst:ComponentId
        Grouping:Grouping<'t>
        Anchoring:bool
        Schema:string list
    }

    /// Storm Topology abstraction
    type Topology<'t> = { 
        Name:string 
        Spouts:Map<ComponentId,Spout<'t>>
        Bolts:Map<ComponentId,Bolt<'t>>
        Streams:Map<StreamId*ComponentId,Stream<'t>>
        Anchors:Map<StreamId,ToAnchors>
        Conf:Conf 
    } with static member WithConf (s,conf) = {s with Topology.Conf = conf}
