/// Defines 'multilang' storm interaction as described here:
/// http://FsShelter.apache.org/documentation/Multilang-protocol.html
module FsShelter.Multilang
open System

type Conf = Map<string,obj>

/// Storm log levels
type LogLevel = 
    | Trace = 0
    | Debug = 1
    | Info = 2
    | Warn = 3
    | Error = 4

type Context = 
    { ComponentId : string
      TaskId : int
      Components : Map<int, string> }

type InCommand<'t> = 
    | Handshake of conf : Conf * pidDir : string * context : Context
    | Ack of string
    | Nack of string
    | Tuple of tuple : 't * id : string * comp : string * stream : string * taskId : int
    | Next
    | TaskIds of int list
    | Heartbeat

type OutCommand<'t> = 
    | Pid of int
    | Ok of string
    | Fail of string
    | Emit of tuple : 't * id : string option * anchors : string list * stream : string * task : int option * needTaskIds : bool option
    | Log of msg : string * level : LogLevel
    | Error of msg : string * ex : Exception
    | Sync
