/// Storm abstractions
module Storm
//handles 'multilang' storm interaction as described here:
//http://storm.apache.org/documentation/Multilang-protocol.html
open System
open System.Text
open FsJson
open System.IO
open System.Threading
open System.Collections.Generic

//some literals used in pattern matching
[<Literal>]
let internal ACK = "ack"
[<Literal>]
let internal FAIL = "fail"
[<Literal>]
let internal NEXT = "next"
[<Literal>]
let internal EMIT = "emit"
[<Literal>]
let internal TUPLE = "tuple"
[<Literal>]
let internal STREAM = "stream"

let mutable private _currentId = 0L
///generate tuple ids (works for single instance spouts, only)
let nextId() = Interlocked.Increment(&_currentId) 

let internal componentShuttingDown() =
   async {
        do! Async.Sleep 200
        Async.CancelDefaultToken()
   }

/// test if running on mono runtime
let isMono() = System.Type.GetType("Mono.Runtime") |> isNull 

// use utf8 only - this is called at initialization
let initIO() = 
    if isMono() then
        () 
        //on osx, setting encoding causes a problem as storm cannot parse output
        //not sure what is the best way of enabling utf8 on osx / unix
        //TODO: for now ignoring this on mono
    else
        Console.InputEncoding <- Encoding.UTF8
        Console.OutputEncoding <- Encoding.UTF8

///send to storm via stdout. 
///Note: data cannot have new lines as these are as delimiters
let private stormOut =
    let mb = MailboxProcessor.Start (fun inbox ->
        async {
            while true do
                let! (str:string) = inbox.Receive()
                Console.Out.WriteLine(str)
                Console.Out.WriteLine("end")
                Console.Out.Flush()
//                Logging.log "out" str
            })
    mb.Post

/// Storm log levels
type LogLevel =
    | Trace = 0
    | Debug = 1
    | Info = 2
    | Warn = 3
    | Error = 4

/// multilang sync message
let stormSync() = stormOut """{"command":"sync"}"""
/// multilang pid message
let stormSendPid (pid:int) = String.Format("""{{"pid":{0}}}""",pid) |> stormOut
/// multilang fail message
let stormFail (id:string) = String.Format("""{{"command":"fail","id":"{0}"}}""", id) |> stormOut
/// multilang ack message
let stormAck  (id:string) = String.Format("""{{"command":"ack","id":"{0}"}}""", id) |> stormOut
/// multilang log message
let stormLog (msg:string) (lvl:LogLevel) = String.Format("""{{"command":"log","msg":"{0}", "level":{1}}}""", msg, (int lvl).ToString()) |> stormOut

///log to storm and throw exception 
let stormLogAndThrow<'a> msg (r:'a) =
    async {
        do stormLog msg LogLevel.Error
        do failwith msg
        return r
    }

let internal nestedExceptionTrace (ex:Exception) =
    let sb = new System.Text.StringBuilder()
    let rec loop (ex:Exception) =
        sb.AppendLine(ex.Message).AppendLine(ex.StackTrace) |> ignore
        if isNull ex.InnerException then
            sb.ToString().Replace(Environment.NewLine,"\\n")
        else
            sb.AppendLine("========") |> ignore
            loop ex.InnerException
    loop ex

///read a message from storm via stdin
let private stormIn()=
    async {
        let! msg  = Console.In.ReadLineAsync() |> Async.AwaitTask
        let! term = Console.In.ReadLineAsync() |> Async.AwaitTask
        if term = "end" then
//            Logging.log "in" msg
            return msg
        elif msg="" || term ="" then
            stormLog "empty input on stdin - component shutting down" LogLevel.Error
            do! componentShuttingDown()
            Environment.Exit(0)
            return ""
        else
            return! stormLogAndThrow "invalid input msg" msg
     }

 ///write Json to stdout with an ID (for reliable spouts)
let reliableEmit housekeeper (id:Int64) (json:Json)  =
    let json = json?id <-  JsonString (id.ToString())
    let json = json?command <- JsonString EMIT
    housekeeper json
    stormOut (FsJson.serialize json)

///write Json to stdout
let emit (json:Json) =
    let json = json?command <- JsonString EMIT
    stormOut (FsJson.serialize json)

///produce a Storm tuple Json object
let tuple (fields:obj seq) = 
    jval [ 
        TUPLE, jval [for f in fields -> jval f] 
        "need_task_ids", jval false // reduce multilang overhead by default
    ]

///anchor to original message(s) - 
///updates anchors field with id or ids, if the original is a collection of messages
let anchor original (msg:Json) = 
    match box original with
    | :? (Json list) as omsgs -> msg?anchors <- jval (omsgs |> List.map(fun orig -> orig?id))
    | :? Json as omsg -> msg?anchors <- jval omsg?id
    | :? string as id ->msg?anchors <- jval id
    | _ -> raise(ArgumentException(sprintf "Don't know how to anchor to: %A" original))

/// specify stream to write the tuple into
let namedStream name (msg:Json) = msg?stream <- jval name

/// join two touples combines fields from both
let tupleJoin (x:Json) (y:Json) =
    Array.append x?tuple.Array y?tuple.Array |> Array.map box |> tuple

/// diagnostics pid shortcut
let private pid() = System.Diagnostics.Process.GetCurrentProcess().Id

///creates an empty file with current pid as the file name
let private createPid pidDir =
    let pid = pid().ToString()
    let path = pidDir + "/" + pid
    use fs = File.CreateText(path)
    fs.Close()

///configuration info - received in the handshake and passed into each component/instance
type Configuration = 
    { 
        PidDir          : string
        TaskId          : string
        Json            : Json          //entire config as json
    }

///read the handshake
let internal readHandshake()=
    async {
        let! msg = stormIn()
        let jmsg = FsJson.parse msg
        match jmsg?pidDir with
        | JsonString s -> 
            return
                {
                    PidDir = s
                    TaskId = jmsg?context?taskid.Val
                    Json = jmsg
                }
        | _ -> return! stormLogAndThrow "expected handshake but pidDir not found" {PidDir=""; TaskId=""; Json=JsonNull}
    }

let internal processPid pidDir =
    async {
            do createPid pidDir
            do stormSendPid (pid())
        }

///runner for reliable spouts
let reliableSpoutRunner housekeeper createEmitter =
    async {
        try 
            let next = createEmitter (reliableEmit housekeeper)
            while true do
                let! msg = stormIn()
                let jmsg = FsJson.parse msg
                let cmd  = jmsg?command.Val
                do! async { 
                    match cmd with
                    | NEXT            -> do! next()
                    | ACK | FAIL | "" -> housekeeper jmsg      //empty is task list ids?
                    | _ -> failwithf "invalid cmd %s" cmd
                    stormSync() 
                }
        with ex ->
            return! stormLogAndThrow (nestedExceptionTrace ex) ()
    }

///runner loop for simple spouts
let simpleSpoutRunner createEmitter =
    async {
        try 
            let next = createEmitter emit
            while true do
                let! msg = stormIn()
                let jmsg = FsJson.parse msg
                let cmd  = jmsg?command.Val
                do! async { 
                    match cmd with
                    | NEXT            -> do! next()
                    | ACK | FAIL | "" -> ()     //ignore other commands
                    | _ -> failwithf "invalid cmd %s" cmd
                    stormSync()
                }
        with ex ->
            return! stormLogAndThrow (nestedExceptionTrace ex) ()
    }

///bolt runner that auto acks received msgs
///For more complex scenarios
///you may have to write your own runners
///See documentation on Storm concepts
///Note: Your implementation should handle "__hearbeat" messages (see code below)
let autoAckBoltRunner reader =
    async {
        while true do
            let! msg = stormIn()
            let jmsg = FsJson.parse msg
            match jmsg,jmsg?stream with
            | _, JsonString "__heartbeat" -> do stormSync()
            | x, _ when isArray x -> () //ignore taskids for now
            | m, _ -> 
                let! res = reader jmsg |> Async.Catch
                match res,jmsg?id with
                | Choice1Of2 _, JsonString str -> stormAck str
                | Choice2Of2 ex, JsonString str -> stormFail str
                                                   stormLog (sprintf "autoBoltRunner: "+ex.Message) LogLevel.Error
                | _ -> ()
    }

/// get tuple id
let msgId (j:Json) = match j?id with JsonString s -> s | _ -> failwith (sprintf "Msg id not found in %A" j)
/// get tuple source
let msgSource (j:Json) = match j?comp with JsonString s -> s | _ -> failwith (sprintf "Msg source not found in %A" j)

/// InboxProcessor-based protocol handler that can be used with reliable spouts to provide processing guarantees
let inboxMsgHandler onEmit onAck onFail onTasks = 
    let tag = "inboxMsgHandler"
    fun  (inbox : MailboxProcessor<Json>) ->
        async { 
            try 
                while true do
                    let! msg = inbox.Receive()
                    let cmd = msg?command.Val
                    match cmd with
                    | EMIT -> onEmit msg
                    | ACK -> onAck msg
                    | FAIL -> onFail msg
                    | "" when isArray msg -> onTasks msg //assume task ids
                    | other -> failwithf "invalid command %A for msg %A" other msg
            with ex ->
                do stormLog (tag + ": " + (nestedExceptionTrace ex)) LogLevel.Error
        }

/// In-memory helper for reliable spouts
let defaultHousekeeper = 
    let ids = new System.Collections.Generic.Dictionary<string, Json>()
    let pendingIds = new System.Collections.Generic.Queue<string>()
    let onEmit msg =
        let mid = msgId msg
        ids.[mid] <- msg
        pendingIds.Enqueue mid
    let onAck msg = 
        ids.Remove(msgId msg) |> ignore
    let onFail msg =
        let mid = msgId msg
        let msg = ids.[mid]
        let taskIds = msg?__taskids.Array //assuming taskids are received before fail msg
        let msg = msg.Remove "__taskids"
        for t in taskIds do
            let m = msg?taskid <- t
            do stormOut (FsJson.serialize m)
    let onTasks msg =
        let id = pendingIds.Dequeue()
        let prevMsg = ids.[id]
        ids.[id] <- (prevMsg?__taskids <- msg)

    let mb = MailboxProcessor.Start (inboxMsgHandler onEmit onAck onFail onTasks)
    mb.Post
