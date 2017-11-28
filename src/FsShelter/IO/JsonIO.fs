/// Json multilang IO
module FsShelter.JsonIO

open Multilang
open System
open System.Text
open System.IO
open Newtonsoft.Json
open TupleSchema
open Newtonsoft.Json.Linq
open Hopac

[<Literal>]
let internal END = "\nend\n"

let private toLog (msg:string) (lvl:int) =
    use sw = new StringWriter()
    use w = new JsonTextWriter(sw)
    w.WriteStartObject()
    w.WritePropertyName("command")
    w.WriteValue("log")
    w.WritePropertyName("msg")
    w.WriteValue(msg)
    w.WritePropertyName("level")
    w.WriteValue(lvl)
    w.WriteEndObject()
    w.Close()
    sw.ToString()

let private toEmit streamRW t (tid:string option,anchors:string list,stream:string,task:int option, needTaskIds:bool option) =
    let writeAnchors (w:JsonTextWriter) = 
        if not (List.isEmpty anchors) then
            w.WritePropertyName("anchors")
            w.WriteStartArray()
            anchors |> List.iter w.WriteValue
            w.WriteEndArray()

    let writeId (w:JsonTextWriter) =
        match tid with
        | Some tid -> 
            w.WritePropertyName("id")
            w.WriteValue(tid)
        | _ -> ()

    let writeTask (w:JsonTextWriter) =
        match task with
        | Some task -> 
            w.WritePropertyName("task")
            w.WriteValue(task)
        | _ -> ()

    let writeTaskIdsNeed (w:JsonTextWriter)= 
        w.WritePropertyName("need_task_ids")
        w.WriteValue(needTaskIds.IsSome && needTaskIds.Value)

    let writeTuple (deconstr:FieldWriter->obj->unit) (w:JsonTextWriter) =
        w.WritePropertyName("tuple")
        w.WriteStartArray()
        deconstr (JsonConvert.SerializeObject >> w.WriteRawValue) t
        w.WriteEndArray()

    let (_,d) = streamRW |> Map.find stream
    use sw = new StringWriter()
    use w = new JsonTextWriter(sw)
    w.WriteStartObject()
    w.WritePropertyName("command")
    w.WriteValue("emit")
    writeId w
    writeTuple d w
    writeAnchors w
    writeTask w
    w.WritePropertyName("stream")
    w.WriteValue(stream)
    writeTaskIdsNeed w
    w.WriteEndObject()
    w.Close()
    sw.ToString()

let private (|Init|_|) (o:JObject) =
    let readConf (conf:JToken) = 
        let r = conf.CreateReader()
        r.Read() |> ignore
        seq {
            while r.Read() && JsonToken.EndObject <> r.TokenType do
                let name = (string r.Value)
                r.Read() |> ignore
                yield name,r.Value
        } |> Map.ofSeq
        
    let readTaskMap (ctx:JToken) =
        let r = ctx.["task->component"].CreateReader()
        r.Read() |> ignore
        seq {
            while r.Read() && JsonToken.EndObject <> r.TokenType do
                let taskId= r.Value |> (string >> Int32.Parse)
                yield taskId,r.ReadAsString()
        } |> Map.ofSeq

    let readContext (ctx:JToken) = 
        let taskMap = readTaskMap ctx
        let taskId = ctx.["taskid"].ToObject()
        {
            ComponentId= taskMap |> Map.find taskId
            TaskId=taskId
            Components=taskMap
        }
        
    match o.HasValues,o.Property("pidDir") |> Option.ofObj with
    | true, Some p ->
        let pidDir = p.ToObject()
        let ctx = readContext (o.["context"])
        let conf = readConf(o.["conf"])
        Some (Handshake(conf, pidDir, ctx))
    | _ -> None

let private (|Control|_|) (o:JObject) =
    match o.HasValues,o.Property("command") |> Option.ofObj with
    | true, Some p ->
        match p.ToObject() with
        | "next" -> Some (Next)
        | "ack" -> Some (Ack (o.["id"].ToObject()))
        | "fail" -> Some (Nack (o.["id"].ToObject()))
        | "activate" -> Some(Activate)
        | "deactivate" -> Some(Deactivate)
        | _ -> None
    | _ -> None

let private (|Stream|_|) findConstructor (o:JObject) =
    match o.HasValues,o.Property("stream") |> Option.ofObj with
    | true, Some p ->
        match p.ToObject() with
        | "__heartbeat" -> Some (Heartbeat)
        | streamId -> 
             let xs = o.["tuple"].Children().GetEnumerator()
             let constr = findConstructor streamId <| fun t -> xs.MoveNext() |> ignore; xs.Current.ToObject(t)
             let comp = if isNull o.["comp"] then "" else o.["comp"].ToObject()
             Some (InCommand.Tuple(constr(), o.["id"].ToObject(), comp, streamId, o.["task"].ToObject()))
    | _ -> None

let private toCommand (findConstructor:string->FieldReader->unit->'t) str : InCommand<'t> =
    let jobj = JObject.Parse str
    match jobj with
    | Init cmd -> cmd
    | Control cmd -> cmd
    | Stream findConstructor cmd -> cmd
    | _ -> failwithf "Unable to parse: %s" str

let private isMono() = not <| isNull (System.Type.GetType("Mono.Runtime"))

/// Start IO parsers given specified TextReaders, output sychrnonization and logger
let startWith (stdin:TextReader,stdout:TextWriter) syncOut (log:Task.Log) :Topology.IO<'t> =
    let write (text:string) =
        log (fun _ -> "> "+text)
        syncOut (fun () -> stdout.Write(text.Replace("\n","\\n"))
                           stdout.Write(END)
                           stdout.Flush())

    if isMono() then
        () //on osx/linux under mono, set env LANG=en_US.UTF-8
    else
        if not Console.IsInputRedirected then
            Console.InputEncoding <- Encoding.UTF8
        if not Console.IsOutputRedirected then
            Console.OutputEncoding <- Encoding.UTF8

    let streamRW = TupleSchema.mapSchema<'t>() |> Map.ofArray
   
    let out' cmd = 
        match cmd with
        | Sync -> """{"command":"sync"}"""
        | Pid pid -> sprintf """{"pid":%d}""" pid
        | Fail tid -> sprintf """{"command":"fail","id":"%s"}""" tid
        | Ok tid -> sprintf """{"command":"ack","id":"%s"}""" tid
        | Log (msg,lvl) -> toLog msg (int lvl)
        | Error (msg,ex) -> toLog (sprintf "%s: %s" msg (Exception.toString ex)) (int LogLevel.Error)
        | Emit (t,tid,anchors,stream, task, needTaskIds) -> toEmit streamRW t (tid, anchors, stream, task, needTaskIds)
        |> write

    let findConstructor stream = 
        streamRW |> Map.find stream |> fst
    let in' ():Job<InCommand<'t>> =
        job {
            let! msg  = stdin.ReadLineAsync |> Job.fromTask
            let! term = stdin.ReadLineAsync |> Job.fromTask
            log (fun _ -> "< "+msg+term)
            return match msg,term with
                   | msg,"end" when not <| String.IsNullOrEmpty msg -> toCommand findConstructor msg
                   | _ -> failwithf "Unexpected input msg/term: %s/%s" msg term
         }

    (in',out')

/// Start IO over STDIN/STDOUT and serialized sychrnonization using specifed logger
let start log :Topology.IO<'t> = startWith (Console.In,Console.Out) IO.Common.serialOut log
