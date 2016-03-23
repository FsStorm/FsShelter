module FsShelter.ThriftIO

open Multilang
open System
open Thrift.Protocol
open Thrift.Transport
open Prolucid.ThriftShell
open TupleSchema
open System.IO

type V = Messages.Variant
let vNone = V(None = Messages.NoneStruct())

let private typeMap = 
    [
        typeof<string>, 
            ((fun (v:obj) -> V(StrVal = (unbox v))), fun (v:V) -> box v.StrVal)
        typeof<int>, 
            ((fun (v:obj) -> V(Int32Val = (unbox v))), fun (v:V) -> box v.Int32Val)
        typeof<int64>, 
            ((fun (v:obj) -> V(Int64Val = (unbox v))), fun (v:V) -> box v.Int64Val)
        typeof<int16>, 
            ((fun (v:obj) -> V(Int16Val = (unbox v))), fun (v:V) -> box v.Int16Val)
        typeof<bool>, 
            ((fun (v:obj) -> V(BoolVal = (unbox v))), fun (v:V) -> box v.BoolVal)
        typeof<byte>, 
            ((fun (v:obj) -> V(ByteVal = (unbox v))), fun (v:V) -> box v.ByteVal)
        typeof<Guid>, 
            ((fun (v:obj) -> V(BytesVal = ((unbox<Guid> v).ToByteArray()))), fun (v:V) -> box (Guid v.BytesVal))
        typeof<double>, 
            ((fun (v:obj) -> V(DoubleVal = (unbox v))), fun (v:V) -> box v.DoubleVal)
        typeof<single>, 
            ((fun (v:obj) -> V(DoubleVal = (float (unbox<single> v)))), fun (v:V) -> box (single v.DoubleVal))
        typeof<DateTime>, 
            ((fun (v:obj) -> V(Iso8601Val = ((unbox<DateTime> v).ToString "o"))), fun (v:V) -> box (DateTime.Parse v.Iso8601Val))
        typeof<DateTimeOffset>, 
            ((fun (v:obj) -> V(Iso8601Val = ((unbox<DateTimeOffset> v).ToString "o"))), fun (v:V) -> box (DateTimeOffset.Parse v.Iso8601Val))

        typeof<string option>, 
            ((fun (v:obj) -> match unbox v with | Some v -> V(StrVal = v) | _ -> vNone), 
             fun (v:V) -> box <| match v.__isset.strVal with | true -> Some v.StrVal | _ -> None)
        typeof<int option>, 
            ((fun (v:obj) -> match unbox v with | Some v -> V(Int32Val = v) | _ -> vNone), 
             fun (v:V) -> box <| match v.__isset.int32Val with | true -> Some v.Int32Val | _ -> None)
        typeof<int64 option>, 
            ((fun (v:obj) -> match unbox v with | Some v -> V(Int64Val = v) | _ -> vNone), 
             fun (v:V) -> box <| match v.__isset.int64Val with | true -> Some v.Int64Val | _ -> None)
        typeof<int16 option>, 
            ((fun (v:obj) -> match unbox v with | Some v -> V(Int16Val = v) | _ -> vNone), 
             fun (v:V) -> box <| match v.__isset.int16Val with | true -> Some v.Int16Val | _ -> None)
        typeof<bool option>, 
            ((fun (v:obj) -> match unbox v with | Some v -> V(BoolVal = v) | _ -> vNone), 
             fun (v:V) -> box <| match v.__isset.boolVal with | true -> Some v.BoolVal | _ -> None)
        typeof<byte option>, 
            ((fun (v:obj) -> match unbox v with | Some v -> V(ByteVal = v) | _ -> vNone), 
             fun (v:V) -> box <| match v.__isset.byteVal with | true -> Some v.ByteVal | _ -> None)
        typeof<Guid option>, 
            ((fun (v:obj) -> match unbox<Guid option> v with | Some v -> V(BytesVal = v.ToByteArray()) | _ -> vNone), 
             fun (v:V) -> box <| match v.__isset.bytesVal with | true -> Some (Guid v.BytesVal) | _ -> None)
        typeof<double option>, 
            ((fun (v:obj) -> match unbox v with | Some v -> V(DoubleVal = v) | _ -> vNone), 
             fun (v:V) -> box <| match v.__isset.doubleVal with | true -> Some v.DoubleVal | _ -> None)
        typeof<single option>, 
            ((fun (v:obj) -> match unbox<single option> v with | Some v -> V(DoubleVal = (double v)) | _ -> vNone), 
             fun (v:V) -> box <| match v.__isset.doubleVal with | true -> Some (single v.DoubleVal) | _ -> None)
        typeof<DateTime option>, 
            ((fun (v:obj) -> match unbox<DateTime option> v with | Some v -> V(Iso8601Val = (v.ToString "o")) | _ -> vNone), 
             fun (v:V) -> box <| match v.__isset.iso8601Val with | true -> Some (DateTime.Parse v.Iso8601Val) | _ -> None)
        typeof<DateTimeOffset option>, 
            ((fun (v:obj) -> match unbox<DateTimeOffset option> v with | Some v -> V(Iso8601Val = (v.ToString "o")) | _ -> vNone), 
             fun (v:V) -> box <| match v.__isset.iso8601Val with | true -> Some (DateTimeOffset.Parse v.Iso8601Val) | _ -> None)
    ] |> dict
    
let private toVariant = function
    | null -> vNone
    | v -> let t = v.GetType()
           match typeMap.TryGetValue t with 
           | true, (f,_) -> f v
           | _ -> V(BytesVal = IO.Common.blobSerialize v)
    
let private ofVariant t (v:V) =
    match typeMap.TryGetValue t with 
    | true, (_,f) -> f v
    | _ when v.__isset.bytesVal -> IO.Common.blobDeserialize t v.BytesVal
    | _ -> null

let private ofAnyVariant = function
    | (v:V) when v.__isset.boolVal -> box v.BoolVal
    | (v:V) when v.__isset.byteVal -> box v.ByteVal
    | (v:V) when v.__isset.doubleVal -> box v.DoubleVal
    | (v:V) when v.__isset.int16Val -> box v.Int16Val
    | (v:V) when v.__isset.int32Val -> box v.Int32Val
    | (v:V) when v.__isset.int64Val -> box v.Int64Val
    | (v:V) when v.__isset.iso8601Val -> box (DateTimeOffset.Parse v.Iso8601Val)
    | (v:V) when v.__isset.strVal -> box v.StrVal
    | _ -> null // TODO: consider adding/handling lists

let internal toFields (deconstr:FieldWriter->obj->unit) tuple =
    let fields = Collections.Generic.List<V>()
    deconstr (toVariant >> fields.Add) tuple
    fields
    
let internal ofFields (constr:FieldReader->unit->'t) (fields:V seq) =
    let xs = fields.GetEnumerator()
    constr (fun t -> xs.MoveNext() |> ignore; ofVariant t xs.Current)
        
let private toCommand log (findConstructor:string->FieldReader->unit->'t) (msg:Messages.StormMsg) : InCommand<'t> =
    let toConf conf = conf |> Seq.map (fun (x:Collections.Generic.KeyValuePair<string,V>) -> x.Key, ofAnyVariant x.Value) |> Map
    let toContext (ctx:Messages.Context) = { ComponentId = ctx.ComponentId; TaskId = ctx.TaskId; Components = ctx.TaskComponents |> Seq.map (|KeyValue|) |> Map }
    match msg.__isset with
    | s when s.ackCmd -> Ack msg.AckCmd.Id
    | s when s.handshake -> Handshake (toConf msg.Handshake.Config, msg.Handshake.PidDir, (toContext msg.Handshake.Context))
    | s when s.hearbeat -> Heartbeat
    | s when s.nackCmd -> Nack msg.NackCmd.Id
    | s when s.nextCmd -> Next
    | s when s.taskIds -> TaskIds (msg.TaskIds.TaskIds |> List.ofSeq)
    | s when s.streamIn ->
        let constr = findConstructor msg.StreamIn.Stream
        InCommand.Tuple ((ofFields constr msg.StreamIn.Tuple)(), msg.StreamIn.Id, msg.StreamIn.Comp, msg.StreamIn.Stream, msg.StreamIn.Task)
    | _ -> failwithf "Unexpected command: %A" msg

let startWith (stdin:#Stream,stdout:#Stream) syncOut (log:Task.Log) :Topology.IO<'t> =
    let protocol = new TBinaryProtocol(new TStreamTransport(stdin,stdout))
    let write (msg:Messages.ShellMsg) =
        log (fun _ -> sprintf "> %A" msg)
        syncOut (fun () -> msg.Write protocol
                           protocol.Transport.Flush())
    let streamRW = TupleSchema.mapSchema<'t>() |> Map.ofArray
   
    let out' (cmd:OutCommand<'t>) = 
        match cmd with
        | Sync -> Messages.ShellMsg(Sync = Messages.SyncReply())
        | Pid pid -> Messages.ShellMsg(Pid = Messages.PidReply(Pid = pid))
        | Fail tid -> Messages.ShellMsg(Fail = Messages.FailReply(Id = tid))
        | Ok tid -> Messages.ShellMsg(Ok = Messages.OkReply(Id = tid))
        | Log (msg,lvl) -> Messages.ShellMsg(Log = Messages.LogCommand(Message = msg, Level = enum (int lvl)))
        | Error (msg,ex) -> Messages.ShellMsg(Log = Messages.LogCommand(Message = (sprintf "%s: %s" msg (Task.traceException ex)), Level = Messages.LogLevel.Error))
        | Emit (t,tid,anchors,stream,task,needTaskIds) -> 
            let (_,d) = streamRW |> Map.find stream
            let cmd = Messages.EmitCommand(Stream = stream, Tuple = (toFields d t))
            if Option.isSome tid then cmd.Id <- tid.Value
            if Option.isSome needTaskIds && needTaskIds.Value then cmd.NeedTaskIds <- needTaskIds.Value
            if not (List.isEmpty anchors) then cmd.Anchors <- Collections.Generic.List(anchors)
            Messages.ShellMsg(Emit = cmd)
        |> write

    let findConstructor stream = 
        streamRW |> Map.find stream |> fst
    let in' ():Async<InCommand<'t>> =
        async {
            let! msg = async {
                    let msg = Messages.StormMsg()
                    msg.Read protocol
                    return msg
            }
            log (fun _ -> sprintf "< %A" msg)
            return toCommand log findConstructor msg
        }

    (in',out')

let start log = startWith (Console.OpenStandardInput(),Console.OpenStandardOutput()) IO.Common.serialOut log
