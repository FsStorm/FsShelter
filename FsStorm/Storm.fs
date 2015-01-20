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

/// use utf8 only - this is called at initialization
let initIO() =
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

//utility storm output functions
let stormSync() = stormOut """{"command":"sync"}"""
let stormSendPid (pid:int) = stormOut (String.Format("""{{"pid":{0}}}""",pid))
let stormFail (id:string) = stormOut (String.Format("""{{"command":"fail","id":"{0}"}}""", id))
let stormAck  (id:string) = stormOut (String.Format("""{{"command":"ack","id":"{0}"}}""", id))
let stormLog (msg:string) = jval ["command","log"; "msg",msg] |> FsJson.serialize |> stormOut

///log to storm and throw exception 
let stormLogAndThrow<'a> msg (r:'a) =
    async {
        do stormLog msg
        Logging.log "stormfail" msg
        do failwith msg
        return r
    }

///read a message from storm via stdin
let private stormIn()=
    async {
        let! msg  = Console.In.ReadLineAsync() |> Async.AwaitTask
        let! term = Console.In.ReadLineAsync() |> Async.AwaitTask
        if term = "end" then
//            Logging.log "in" msg
            return msg
        elif msg="" || term ="" then
            Logging.log "stormIn" "empty input, exiting..."
            do! componentShuttingDown()
            Environment.Exit(0)
            return ""
        else
            return! stormLogAndThrow "invalid input msg" ""
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
    Logging.log "pidpath" path
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
            Logging.logex "spoutAgent" ex
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
            Logging.logex "spoutAgent" ex
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
            Logging.logex "boltMaster" ex
    }

let msgId (j:Json) = match j?id with JsonString s -> s | _ -> failwith (sprintf "Msg id not found in %A" j)

///a simple helper that can be used with reliable spouts
let housekeeper  (inbox:MailboxProcessor<Json>) =
    let tag = "housekeeper"
    let ids = new System.Collections.Generic.Dictionary<string,Json>()
    let pendingIds = new System.Collections.Generic.Queue<string>()
    async {
        try
            while true do
                let! msg = inbox.Receive()
                let cmd = msg?command.Val
                match cmd with
                | EMIT -> 
                    let mid = msgId msg
                    ids.[mid] <- msg
                    pendingIds.Enqueue mid
                | ACK -> ids.Remove (msgId msg) |> ignore
                | FAIL -> 
                    let mid = msgId msg
                    let msg = ids.[mid]
                    let taskIds = msg?__taskids.Array //assuming taskids are received before fail msg
                    let msg = msg.Remove "__taskids"
                    for t in taskIds do
                        let m = msg?taskid <- t
                        do stormOut (FsJson.serialize m)
                | "" when isArray msg -> //assume task ids
                    let id = pendingIds.Dequeue()
                    let prevMsg = ids.[id]
                    ids.[id] <- (prevMsg?__taskids <- msg)
                | other -> Logging.log tag (sprintf "invalid command for msg %A" msg)
        with ex ->
            do stormLog ex.Message
            Logging.logex tag ex
            }

//creates the default housekeeper for reliable spouts
let createDefaultHousekeeper cfg = 
    let mb = MailboxProcessor.Start housekeeper
    mb.Post