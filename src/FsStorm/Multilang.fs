/// Defines 'multilang' storm interaction as described here:
/// http://storm.apache.org/documentation/Multilang-protocol.html
module Storm.Multilang
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
      TaskId : int64
      Components : Map<int64, string> }

type InCommand<'t> = 
    | Handshake of conf : Conf * pidDir : string * context : Context
    | Ack of string
    | Nack of string
    | Tuple of tuple : 't * id : string * comp : string * stream : string * taskId : int64
    | Next
    | TaskIds of int64 list
    | Heartbeat

type OutCommand<'t> = 
    | Pid of int
    | Ok of string
    | Fail of string
    | Emit of tuple : 't * id : string option * anchors : string list * stream : string * needTaskIds : bool option
    | Log of msg : string * level : LogLevel
    | Sync

let internal logOfException (ex:Exception) =
    let sb = new System.Text.StringBuilder()
    let rec loop (ex:Exception) =
        sb.AppendLine(ex.Message).AppendLine(ex.StackTrace) |> ignore
        if isNull ex.InnerException then
            sb.ToString()
        else
            sb.AppendLine("========") |> ignore
            loop ex.InnerException
    Log(loop ex, LogLevel.Error)
