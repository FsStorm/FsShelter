/// Protobuf IO implementation
module FsShelter.ProtoIO

open Multilang
open System
open Google.Protobuf
open Google.Protobuf.WellKnownTypes
open Prolucid.ProtoShell
open TupleSchema
open System.IO

type internal V = Messages.Variant
type internal VL = WellKnownTypes.Value
type internal VLKind = VL.KindOneofCase
type internal VKind = V.KindOneofCase
let internal vNone = V(NoneVal = WellKnownTypes.NullValue.NULL_VALUE)

let private typeMap = 
    [
        typeof<string>, 
            ((fun (v:obj) -> V(StrVal = (unbox v))), fun (v:V) -> box v.StrVal)
        typeof<int>, 
            ((fun (v:obj) -> V(Int32Val = (unbox v))), fun (v:V) -> box v.Int32Val)
        typeof<int64>, 
            ((fun (v:obj) -> V(Int64Val = (unbox v))), fun (v:V) -> box v.Int64Val)
        typeof<int16>, 
            ((fun (v:obj) -> V(Int32Val = (unbox v))), fun (v:V) -> box (int16 v.Int32Val))
        typeof<bool>, 
            ((fun (v:obj) -> V(BoolVal = (unbox v))), fun (v:V) -> box v.BoolVal)
        typeof<byte>, 
            ((fun (v:obj) -> V(Int32Val = int (unbox<byte> v))), fun (v:V) -> box (byte v.Int32Val))
        typeof<Guid>, 
            ((fun (v:obj) -> V(BytesVal = (unbox<Guid> v).ToByteString())), fun (v:V) -> box (v.BytesVal.ToGuid()))
        typeof<double>, 
            ((fun (v:obj) -> V(DoubleVal = (unbox v))), fun (v:V) -> box v.DoubleVal)
        typeof<single>, 
            ((fun (v:obj) -> V(DoubleVal = (float (unbox<single> v)))), fun (v:V) -> box (single v.DoubleVal))
        typeof<DateTime>, 
            ((fun (v:obj) -> V(TimestampVal = Timestamp.FromDateTime((unbox<DateTime> v).ToUniversalTime()))), fun (v:V) -> box (v.TimestampVal.ToDateTime()))
        typeof<DateTimeOffset>, 
            ((fun (v:obj) -> V(TimestampVal = Timestamp.FromDateTimeOffset(unbox<DateTimeOffset> v))), fun (v:V) -> box (v.TimestampVal.ToDateTimeOffset()))

        typeof<string option>, 
            ((fun (o:obj) -> match unbox o with | Some v -> V(StrVal = v) | _ -> vNone), 
             fun (v:V) -> box <| match v.KindCase with | VKind.StrVal -> Some v.StrVal | _ -> None)
        typeof<int option>, 
            ((fun (o:obj) -> match unbox o with | Some v -> V(Int32Val = v) | _ -> vNone), 
             fun (v:V) -> box <| match v.KindCase with | VKind.Int32Val -> Some v.Int32Val | _ -> None)
        typeof<int64 option>, 
            ((fun (o:obj) -> match unbox o with | Some v -> V(Int64Val = v) | _ -> vNone), 
             fun (v:V) -> box <| match v.KindCase with | VKind.Int64Val -> Some v.Int64Val | _ -> None)
        typeof<int16 option>, 
            ((fun (o:obj) -> match unbox<int16 option> o with | Some v -> V(Int32Val = int v) | _ -> vNone), 
             fun (v:V) -> box <| match v.KindCase with | VKind.Int32Val -> Some (int16 v.Int32Val) | _ -> None)
        typeof<bool option>, 
            ((fun (o:obj) -> match unbox o with | Some v -> V(BoolVal = v) | _ -> vNone), 
             fun (v:V) -> box <| match v.KindCase with | VKind.BoolVal -> Some v.BoolVal | _ -> None)
        typeof<byte option>, 
            ((fun (o:obj) -> match unbox<byte option> o with | Some v -> V(Int32Val = (int v)) | _ -> vNone), 
             fun (v:V) -> box <| match v.KindCase with | VKind.Int32Val -> Some (byte v.Int32Val) | _ -> None)
        typeof<Guid option>, 
            ((fun (o:obj) -> match unbox<Guid option> o with | Some v -> V(BytesVal = v.ToByteString()) | _ -> vNone), 
             fun (v:V) -> box <| match v.KindCase with | VKind.BytesVal -> Some (v.BytesVal.ToGuid()) | _ -> None)
        typeof<double option>, 
            ((fun (o:obj) -> match unbox o with | Some v -> V(DoubleVal = v) | _ -> vNone), 
             fun (v:V) -> box <| match v.KindCase with | VKind.DoubleVal -> Some v.DoubleVal | _ -> None)
        typeof<single option>, 
            ((fun (o:obj) -> match unbox<single option> o with | Some v -> V(DoubleVal = (double v)) | _ -> vNone), 
             fun (v:V) -> box <| match v.KindCase with | VKind.DoubleVal -> Some (single v.DoubleVal) | _ -> None)
        typeof<DateTime option>, 
            ((fun (o:obj) -> match unbox<DateTime option> o with | Some v -> V(TimestampVal = Timestamp.FromDateTime (v.ToUniversalTime())) | _ -> vNone), 
             fun (v:V) -> box <| match v.KindCase with | VKind.TimestampVal -> Some (v.TimestampVal.ToDateTime()) | _ -> None)
        typeof<DateTimeOffset option>, 
            ((fun (o:obj) -> match unbox<DateTimeOffset option> o with | Some v -> V(TimestampVal = Timestamp.FromDateTimeOffset v) | _ -> vNone), 
             fun (v:V) -> box <| match v.KindCase with | VKind.TimestampVal -> Some (v.TimestampVal.ToDateTimeOffset()) | _ -> None)
    ] |> dict
    
let private toVariant = function
    | null -> vNone
    | v -> let t = v.GetType()
           match typeMap.TryGetValue t with 
           | true, (f,_) -> f v
           | _ -> V(BytesVal = ByteString.CopyFrom(IO.Common.blobSerialize v)) // TODO: optimize
    
let private ofVariant t (v:V) =
    match typeMap.TryGetValue t with 
    | true, (_,f) -> f v
    | _ when v.KindCase = VKind.BytesVal -> (IO.Common.blobDeserialize t (v.BytesVal.ToByteArray())) // TODO: optimize
    | _ -> null

let rec private ofValue = function
    | (v:VL) when v.KindCase = VLKind.BoolValue -> box v.BoolValue
    | (v:VL) when v.KindCase = VLKind.NumberValue -> box v.NumberValue
    | (v:VL) when v.KindCase = VLKind.StringValue -> box v.StringValue
    | (v:VL) when v.KindCase = VLKind.ListValue -> box (v.ListValue.Values |> Seq.map ofValue |> Seq.toList)
    | (v:VL) when v.KindCase = VLKind.NullValue -> null
    | v -> failwithf "Unexpected Value: %A" v

let internal toFields (deconstr:FieldWriter->obj->unit) tuple =
    let fields = Collections.Generic.List<V>()
    deconstr (toVariant >> fields.Add) tuple
    fields
    
let internal ofFields (constr:FieldReader->unit->'t) (fields:V seq) =
    let xs = fields.GetEnumerator()
    constr (fun t -> xs.MoveNext() |> ignore; ofVariant t xs.Current)
        
let private toCommand log (findConstructor:string->FieldReader->unit->'t) (msg:Messages.StormMsg) : InCommand<'t> =
    let toConf conf = conf |> Seq.map (fun (x:Collections.Generic.KeyValuePair<string,VL>) -> x.Key, ofValue x.Value) |> Map
    let toContext (ctx:Messages.Context) = { ComponentId = ctx.ComponentId; TaskId = ctx.TaskId; Components = ctx.TaskComponents |> Seq.map (|KeyValue|) |> Map }
    match msg.MsgCase with
    | Messages.StormMsg.MsgOneofCase.AckCmd -> Ack msg.AckCmd.Id
    | Messages.StormMsg.MsgOneofCase.Handshake -> Handshake (toConf msg.Handshake.Config, msg.Handshake.PidDir, (toContext msg.Handshake.Context))
    | Messages.StormMsg.MsgOneofCase.Hearbeat -> Heartbeat
    | Messages.StormMsg.MsgOneofCase.NackCmd -> Nack msg.NackCmd.Id
    | Messages.StormMsg.MsgOneofCase.NextCmd -> Next
    | Messages.StormMsg.MsgOneofCase.TaskIds -> TaskIds (msg.TaskIds.TaskIds |> List.ofSeq)
    | Messages.StormMsg.MsgOneofCase.StreamIn ->
        let constr = findConstructor msg.StreamIn.Stream
        InCommand.Tuple ((ofFields constr msg.StreamIn.Tuple)(), msg.StreamIn.Id, msg.StreamIn.Comp, msg.StreamIn.Stream, msg.StreamIn.Task)
    | _ -> failwithf "Unexpected command: %A" msg

let startWith (stdin:#Stream,stdout:#Stream) syncOut (log:Task.Log) :Topology.IO<'t> =
    let write (msg:Messages.ShellMsg) =
        log (fun _ -> sprintf "> %A" msg)
        syncOut (fun () -> msg.WriteDelimitedTo stdout
                           stdout.Flush())

    let streamRW = TupleSchema.mapSchema<'t>() |> Map.ofArray
   
    let out' (cmd:OutCommand<'t>) = 
        match cmd with
        | Sync -> Messages.ShellMsg(Sync = Messages.SyncReply())
        | Pid pid -> Messages.ShellMsg(Pid = Messages.PidReply(Pid = pid))
        | Fail tid -> Messages.ShellMsg(Fail = Messages.FailReply(Id = tid))
        | Ok tid -> Messages.ShellMsg(Ok = Messages.OkReply(Id = tid))
        | Log (msg,lvl) -> Messages.ShellMsg(Log = Messages.LogCommand(Text = msg, Level = enum (int lvl)))
        | Error (msg,ex) -> Messages.ShellMsg(Log = Messages.LogCommand(Text = (sprintf "%s: %s" msg (Task.traceException ex)), Level = Messages.LogCommand.Types.LogLevel.Error))
        | Emit (t,tid,anchors,stream,task,needTaskIds) -> 
            let (_,d) = streamRW |> Map.find stream
            let cmd = Messages.EmitCommand(Stream = stream)
            cmd.Tuple.Add (toFields d t)
            if Option.isSome tid then cmd.Id <- tid.Value
            if Option.isSome needTaskIds && needTaskIds.Value then cmd.NeedTaskIds <- needTaskIds.Value
            if not (List.isEmpty anchors) then cmd.Anchors.Add (anchors)
            Messages.ShellMsg(Emit = cmd)
        |> write

    let findConstructor stream = 
        streamRW |> Map.find stream |> fst

    let in' ():Async<InCommand<'t>> =
        async {
            let! msg = async {
                return Messages.StormMsg.Parser.ParseDelimitedFrom stdin
            }
            log (fun _ -> sprintf "< %A" msg)
            return toCommand log findConstructor msg
        }

    (in',out')

/// Start IO over STDIN/STDOUT and serialized sychrnonization using specifed logger
let start log = startWith (Console.OpenStandardInput(),Console.OpenStandardOutput()) IO.Common.serialOut log

