namespace StormDSL
open System
// a DSL to define storm topologies in F#
// modeled after the Clojure DSL
// http://storm.apache.org/documentation/Clojure-DSL.html

///References the function that will run when
///Strom invokes the program. The 'Name' is passed in the configuration
type ScriptRef = {Func:Storm.Configuration->Async<unit>}

type Component = 
    | Shell of command:string * args:string
    | Java  of className:string * args:string list
    | Local of ScriptRef 

type StreamOutput =
    | Default of string list
    | Named   of string * string list

type StreamId =
   | DefaultStream of string          //component id
   | Stream        of string * string //component id , stream name

type SpoutSpec = 
   {
    Id          : string
    Outputs     : StreamOutput list
    Spout       : Component
    Parallelism : int
    Config      : FsJson.Json
   }

type Grouping = 
    | Shuffle 
    | Fields of string list
    | All
    | Direct

type BoltSpec =
  {
    Id          : string
    Outputs     : StreamOutput list
    Inputs      : (StreamId*Grouping) list //string is component id
    Bolt        : Component
    Parallelism : int
    Config      : FsJson.Json
   }

type Topology = 
    {
        TopologyName    : string;
        Spouts          : SpoutSpec list; 
        Bolts           : BoltSpec list
    }