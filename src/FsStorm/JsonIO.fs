module Storm.JsonIO

open Multilang

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
