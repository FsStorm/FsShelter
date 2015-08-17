/// Topoloy authoring
namespace StormDSL
open System
// a DSL to define storm topologies in F#
// modeled after the Clojure DSL
// http://storm.apache.org/documentation/Clojure-DSL.html

///References the function that will run when
///Strom invokes the program. The 'Name' is passed in the configuration
type ScriptRef = {Func:Storm.Configuration->Async<unit>}

/// Component definition, https://github.com/apache/storm/blob/master/storm-core/src/storm.thrift#L74
type Component = 
    | Shell of command:string * args:string
    | Java  of className:string * args:string list
    | Local of ScriptRef 

/// Output stream definition
type StreamOutput =
    | Default of string list
    | Named   of string * string list

/// Input stream reference
type StreamId =
   | DefaultStream of string          //component id
   | Stream        of string * string //component id , stream name

/// Corresponds to https://github.com/apache/storm/blob/master/storm-core/src/storm.thrift#L95
type SpoutSpec = 
   {
    Id          : string
    Outputs     : StreamOutput list
    Spout       : Component
    Parallelism : int
    Config      : FsJson.Json
   }

/// Corresponds to https://github.com/apache/storm/blob/master/storm-core/src/storm.thrift#L52
type Grouping = 
    | Shuffle 
    | Fields of string list
    | All
    | Direct

/// Corresponds to https://github.com/apache/storm/blob/master/storm-core/src/storm.thrift#L102
type BoltSpec =
  {
    Id          : string
    Outputs     : StreamOutput list // optional outputs
    Inputs      : (StreamId*Grouping) list //string is component id
    Bolt        : Component
    Parallelism : int
    Config      : FsJson.Json
   }

/// Aggregate all spouts and bolts
type Topology = 
    {
        TopologyName    : string;
        Spouts          : SpoutSpec list; 
        Bolts           : BoltSpec list
    }