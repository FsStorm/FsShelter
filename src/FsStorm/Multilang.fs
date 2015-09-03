/// Defines 'multilang' storm interaction as described here:
/// http://storm.apache.org/documentation/Multilang-protocol.html
module Storm.Multilang

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

type InCommand = 
    | Handshake of conf : Map<string, string> * pidDir : string * context : Context
    | Ack of string
    | Fail of string
    | Tuple of tuple : obj seq * id : string * comp : string * stream : string * taskId : int64
    | Next

type OutCommand = 
    | Pid of int
    | Emit of tuple : obj seq * id : string option * anchors : string seq * stream : string option * needTaskIds : bool option
    | Log of msg : string * level : LogLevel
    | Sync
