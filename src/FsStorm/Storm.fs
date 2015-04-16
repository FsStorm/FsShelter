module Storm
//handles 'multilang' storm interaction as described here:
//http://storm.apache.org/documentation/Multilang-protocol.html
open System
open System.Text
open FsJson
open System.IO
open System.Threading
open FstExtensions

//some literals used in pattern matching
[<Literal>]
let ACK = "ack"
[<Literal>]
let FAIL = "fail"
[<Literal>]
let NEXT = "next"
[<Literal>]
let EMIT = "emit"
[<Literal>]
let TUPLE = "tuple"


let mutable private _currentId = 0L
let nextId() = Interlocked.Increment(&_currentId) //a spout may use this to generate ids (works for single instance spouts, only)

let componentShuttingDown() =
   async {
        do! Async.Sleep 200
        Async.CancelDefaultToken()
   }

let isMono() = System.Type.GetType("Mono.Runtime") <> null

/// use utf8 only - this is called at initialization
let initIO() = 
    if isMono() then
        () 
        //on osx, setting encoding causes a problem as storm cannot parse output
        //not sure what is the best way of enabling utf8 on osx / unix
        //TODO: for now ignoring this on mono
    else
        Console.InputEncoding <- Encoding.UTF8
        Console.OutputEncoding <- Encoding.UTF8

///send to storm via stdout. Note: data cannot have new lines as these
///are as delimiters
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

//utility storm output functions
let stormSync() = stormOut """{"command":"sync"}"""
let stormSendPid (pid:int) = stormOut (String.Format("""{{"pid":{0}}}""",pid))
let stormFail (id:string) = stormOut (String.Format("""{{"command":"fail","id":"{0}"}}""", id))
let stormAck  (id:string) = stormOut (String.Format("""{{"command":"ack","id":"{0}"}}""", id))
let stormLog (msg:string) (lvl:LogLevel) = jval ["command","log"; "msg",msg; "level",(int lvl).ToString()] |> FsJson.serialize |> stormOut

///log to storm and throw exception 
let stormLogAndThrow<'a> msg (r:'a) =
    async {
        do stormLog msg LogLevel.Error
        do failwith msg
        return r
    }

let nestedExceptionTrace (ex:Exception) =
    let sb = new System.Text.StringBuilder()
    let rec loop (ex:Exception) =
        sb.AppendLine(ex.Message).AppendLine(ex.StackTrace) |> ignore
        if ex.InnerException <> null then
            sb.AppendLine("========") |> ignore
            loop ex.InnerException
        else
            sb.ToString()
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

 //message emitter for reliable spouts
let reliableEmit housekeeper (id:Int64) (json:Json)  =
    let json = json?id <-  JsonString (id.ToString())
    let json = json?command <- JsonString EMIT
    housekeeper json
    stormOut (FsJson.serialize json)

//emitter for bolts and simple spouts
let emit (json:Json) =
    let json = json?command <- JsonString EMIT
    stormOut (FsJson.serialize json)

let pid() = System.Diagnostics.Process.GetCurrentProcess().Id

///creates an empty file with current pid as the file name
let createPid pidDir =
    let pid = pid().ToString()
    let path = pidDir + "/" + pid
    use fs = File.CreateText(path)
    fs.Close()

///configuration info - received in the handshake
type Configuration = 
    { 
        PidDir          : string
        TaskId          : string
        Json            : Json          //entire config as json
    }

///read the handshake
let readHandshake()=
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

let processPid pidDir =
    async {
            do createPid pidDir
            do stormSendPid (pid())
        }

let isArray = function JsonArray _ -> true | _ -> false

///runner for reliable spouts
let reliableSpoutRunner cfg fCreateHousekeeper fCreateEmitter =
    async {
        try 
            let housekeeper = fCreateHousekeeper cfg
            let emitter = fCreateEmitter cfg
            let next = emitter (reliableEmit housekeeper)
            while true do
                let! msg = stormIn()
                let jmsg = FsJson.parse msg
                let cmd  = jmsg?command.Val
                match cmd with
                | NEXT              -> do! next()
                | ACK | FAIL | "" -> housekeeper jmsg      //empty is task list ids?
                | _ -> failwithf "invalid cmd %s" cmd
        with ex ->
            return! stormLogAndThrow (nestedExceptionTrace ex) ()
    }

///runner loop for simple spouts
let simpleSpoutRunner cfg fCreateEmitter =
    async {
        try 
            let emitter = fCreateEmitter cfg
            let next = emitter emit
            while true do
                let! msg = stormIn()
                let jmsg = FsJson.parse msg
                let cmd  = jmsg?command.Val
                match cmd with
                | NEXT              -> do! next()
                | ACK | FAIL | "" -> ()     //ignore other commands
                | _ -> failwithf "invalid cmd %s" cmd
        with ex ->
            return! stormLogAndThrow (nestedExceptionTrace ex) ()
    }

///bolt runner that auto acks received msgs
///For more complex scenarios
///you may have to write your own runners
///See documentation on Storm concepts
///Note: Your implementation should handle "__hearbeat" messages (see code below)
let autoAckBoltRunner cfg fReaderCreator =
    async {
        try
            let reader = fReaderCreator cfg
            while true do
                let! msg = stormIn()
                let jmsg = FsJson.parse msg
                match jmsg,jmsg?stream with
                | _, JsonString "__heartbeat" -> do stormSync()
                | x, _ when isArray x -> () //ignore taskids for now
                | m, _ -> 
                    do! reader jmsg
                    match jmsg?id with
                    | JsonString str -> do stormAck str
                    | _ -> ()
         with ex ->
            return! stormLogAndThrow (nestedExceptionTrace ex) ()
    }

let msgId (j:Json) = match j?id with JsonString s -> s | _ -> failwith (sprintf "Msg id not found in %A" j)

///a simple helper that can be used with reliable spouts
let getHousekeeper onEmit onAck onFail onTasks = 
    let tag = "housekeeper"
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

//creates the default housekeeper for reliable spouts
let createDefaultHousekeeper cfg = 
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

    let mb = MailboxProcessor.Start (getHousekeeper onEmit onAck onFail onTasks)
    mb.Post
