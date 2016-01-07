module Storm.ProtoIO
//
//open Multilang
//open System
//open System.Text
//open System.IO
//open ProtoShell
//open TupleSchema
//
//let private write (text:string) (_,textWriter:TextWriter) =
//    textWriter.WriteLine(text)
//    textWriter.WriteLine("end")
//    textWriter.Flush()
//
//let private toAnchors = function
//    | [] -> "[]"
//    | anchors -> anchors |> JsonConvert.SerializeObject
//
//let private toId = function
//    | Some tid -> tid
//    | _ -> ""
//
//let private toTaskIdsNeed = function
//    | Some need -> string need
//    | _ -> "false"
//
//let private toTuple (deconstr:FieldWriter->obj->unit) tuple =
//    use sw = new StringWriter()
//    use w = new JsonTextWriter(sw)
//    w.WriteStartArray()
//    deconstr (fun v -> w.WriteValue(v)) tuple
//    w.WriteEndArray()
//    w.Close()
//    sw.ToString()
//
//let private readProp (r:JsonTextReader) name proj =
//    match r.TokenType,(string r.Value) with
//    | JsonToken.PropertyName,n when name = n -> 
//        let v = Some (proj r)
//        r.Read() |> ignore
//        v
//    | _ -> None
//
//let private readObjEnd (r:JsonTextReader) v = 
//    while JsonToken.EndObject <> r.TokenType && r.Read() do () // skip to the end
//    r.Read() |> ignore
//    v
//
//let private readString (r:JsonTextReader) = 
//    r.ReadAsString()
//    
//let private (|Init|_|) (r:JsonTextReader) =
//    let readConf() = 
//        match r.Read(),r.TokenType with
//        | true, JsonToken.StartObject ->
//            seq {
//                while r.Read() && JsonToken.EndObject <> r.TokenType do
//                    let name = (string r.Value)
//                    r.Read() |> ignore
//                    yield name,r.Value
//            } |> Map.ofSeq |> Some
//        | _ -> None
//        
//    let readPid() = readProp r "pidDir" readString
//        
//    let readTaskMap() =
//        match r.Read(), r.TokenType, (string r.Value) with
//        | true, JsonToken.PropertyName, "task->component" ->
//            match r.Read(),r.TokenType with
//            | true, JsonToken.StartObject ->
//                seq {
//                    while r.Read() && JsonToken.EndObject <> r.TokenType do
//                        let taskId= r.Value |> (string >> Int64.Parse)
//                        yield taskId,r.ReadAsString()
//                } |> Map.ofSeq |> Some
//            | _ -> None
//        | _ -> None
//
//    let readContext() = 
//        match r.Read(), r.TokenType, (string r.Value) with
//        | true, JsonToken.PropertyName, "context" ->
//            match r.Read(),r.TokenType with
//            | true, JsonToken.StartObject ->
//                let taskMap = readTaskMap() |> Option.map (readObjEnd r) |> Option.get
//                let taskId = readProp r "taskid" (fun r -> r.ReadAsDouble().Value |> int64) |> Option.get
//                Some {
//                    ComponentId= taskMap |> Map.find taskId
//                    TaskId=taskId
//                    Components=taskMap
//                }
//            | _ -> None
//        | _ -> None
//        
//    match r.TokenType,(string r.Value) with
//    | JsonToken.PropertyName, "pidDir" ->
//        let pidDir = readPid() |> Option.get
//        let ctx = readContext() |> Option.map (readObjEnd r) |> Option.get
//        let conf = readConf() |> Option.get
//        Some (Handshake(conf, pidDir, ctx))
//    | _ -> None
//
//let private (|Command|_|) (r:JsonTextReader) =
//    let tupleId = readProp r "id" readString
//    
//    let cmd tupleId =
//        match r.TokenType,(string r.Value) with
//        | JsonToken.PropertyName,"command" -> 
//            match r.ReadAsString() with
//            | "next" -> Some (Next)
//            | "ack" -> Some (Ack tupleId)
//            | "fail" -> Some (Nack tupleId)
//            | _ -> None
//        | _ -> None
//    tupleId |> Option.bind cmd
//
//let private (|StreamIn|_|) findConstructor (r:JsonTextReader) =
//    let comp = readProp r "comp" readString
//    
//    let tuple = function
//        | Choice1Of2 h -> Some h
//        | Choice2Of2(tupleId,comp,streamId,task) -> 
//            match r.Read(),r.TokenType,(string r.Value) with
//            | true,JsonToken.PropertyName, "tuple" ->
//                match r.Read(),r.TokenType with
//                | true,JsonToken.StartArray ->
//                    let t = findConstructor streamId <| (fun _ -> r.Read() |> ignore; r.Value)
//                    Some (InCommand.Tuple(t(),tupleId,comp,streamId,task))
//                | _ -> None
//            | _ -> None
//    
//    let tupleId comp = readProp r "id" (fun r -> Choice2Of2 (comp,r.ReadAsString()))
//
//    let stream = function
//        | Choice1Of2 cmd -> Some (Choice1Of2 cmd)
//        | Choice2Of2 (tupleId,comp) -> readProp r "stream" (fun r -> Choice2Of2 (tupleId,comp,r.ReadAsString()))
//
//    let maybeHeartbeat = function
//        | Choice1Of2 cmd -> Some (Choice1Of2 cmd)
//        | Choice2Of2 (tupleId,comp,str) ->
//            match str with
//            | "__heartbeat" -> Some (Choice1Of2 Heartbeat)
//            | _ -> Some (Choice2Of2(tupleId,comp,str))
//    
//    let task = function
//        | Choice1Of2 cmd -> Some (Choice1Of2 cmd)
//        | Choice2Of2(tupleId,comp,streamId) -> 
//            readProp r "task" (fun r -> Choice2Of2(tupleId,comp,streamId,r.ReadAsInt32().Value))
//    
//    comp 
//    |> Option.bind tupleId
//    |> Option.bind stream 
//    |> Option.bind maybeHeartbeat 
//    |> Option.bind task 
//    |> Option.bind tuple
//
//let private toCommand (findConstructor:string->FieldReader->unit->'t) str : InCommand<'t> =
//    use sr = new StringReader(str)
//    use r = new JsonTextReader(sr)
//    match r.Read(),r.TokenType with
//    | true,JsonToken.StartObject -> 
//        match r.Read(),r with 
//        | true, Init h -> h
//        | true, StreamIn findConstructor data -> data
//        | _ -> failwithf "Unexpected token: %A:%A" r.TokenType r.Value
//    | _ -> failwithf "Not a Json object or unexpected end: %A:%A" r.TokenType r.Value
//
//let start tag :Topology.IO<'t> =
//    if isMono() then
//        () //on osx/linux under mono, set env LANG=en_US.UTF-8
//    else
//        try
//            Console.InputEncoding <- Encoding.UTF8
//            Console.OutputEncoding <- Encoding.UTF8
//        with _ -> ()
//
//    let streamRW = TupleSchema.mapSchema<'t>() |> Map.ofArray
//   
//    let out' cmd = 
//        match cmd with
//        | Sync -> """{"command":"sync"}"""
//        | Pid pid -> sprintf """{{"pid":%d}}""" pid
//        | Fail tid -> sprintf """{{"command":"fail","id":"%s"}}""" tid
//        | Ok tid -> sprintf """{{"command":"ack","id":"%s"}}""" tid
//        | Log (msg,lvl) -> sprintf """{{"command":"log","msg":"%s:%s", "level":%d}}""" tag msg (int lvl)
//        | Emit (t,tid,anchors,stream,needTaskIds) -> 
//            let (_,d) = streamRW |> Map.find stream
//            sprintf 
//                """{{"command":"emit","id":"%s","tuple":"%s","anchors":"%s","stream":"%s","need_task_ids":%s}}""" 
//                (toId tid) (toTuple d t) (toAnchors anchors) stream (toTaskIdsNeed needTaskIds)
//        |> write |> IO.Common.sync_out
//
//    let in' ():Async<InCommand<'t>> =
//        let findConstructor stream = streamRW |> Map.find stream |> fst
//        async {
//            let! msg  = Console.In.ReadLineAsync() |> Async.AwaitTask
//            let! term = Console.In.ReadLineAsync() |> Async.AwaitTask
//            return match msg,term with
//                   | msg,"end" when not <| String.IsNullOrEmpty msg -> toCommand findConstructor msg
//                   | _ -> failwithf "Unexpected input msg/term: %s/%s" msg term
//         }
//
//    (in',out')
