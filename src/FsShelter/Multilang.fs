/// Defines 'multilang' storm interaction as described here:
/// http://FsShelter.apache.org/documentation/Multilang-protocol.html
module FsShelter.Multilang
open System

/// Storm runtime context
type Context = 
    { ComponentId : string
      TaskId : int
      Components : Map<int, string> }

/// Storm messages
type InCommand<'t> = 
    | Handshake of conf : Conf * pidDir : string * context : Context
    | Ack of TupleId
    | Nack of TupleId
    | Tuple of tuple : 't * id : TupleId * comp : string * stream : string * taskId : int
    | Next
    | TaskIds of int list
    | Heartbeat
    | Activate
    | Deactivate

/// Shell messages
type OutCommand<'t> = 
    | Pid of int
    | Ok of TupleId
    | Fail of TupleId
    | Emit of tuple : 't * id : TupleId option * anchors : TupleId list * stream : string * task : int option * needTaskIds : bool option
    | Log of msg : string * level : LogLevel
    | Error of msg : string * ex : Exception
    | Sync
