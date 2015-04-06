module Logging
open System
open System.IO
open System.Web

let private rollover_time = TimeSpan.FromHours(1.).TotalMilliseconds |> int
let private max_logs_size_mb = 10L
let private log_ext = ".log"
let pid = System.Diagnostics.Process.GetCurrentProcess().Id.ToString()
let mutable log_path = "./logs/" + pid
//    let path =
//        try 
//            VirtualPathUtility.ToAbsolute("~/App_Data/") 
//        with ex -> 
//            @"c:/ws/temp/logs/"
//    path

let private logFileName() = sprintf "%s/log_%s%s" log_path (DateTime.Now.ToString("yyyyMMddHHmm")) log_ext

let private newLogFileStream() = 
    let logPath = log_path
    if Directory.Exists(logPath) |> not then 
        Directory.CreateDirectory(logPath) |> ignore
    let str = new StreamWriter(File.Open(logFileName(), FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read))
    str.AutoFlush <- true
    str

let private logToFile file msg =
    let str =
        match !file with
        | None -> 
            let str = newLogFileStream()
            file := Some str
            str
        | Some str -> str
    str.WriteLine(msg:string)


let private closeLog file =
    match !file with
    | None -> ()
    | Some (str:StreamWriter) -> str.Flush(); str.Close()
    file := None

let private rollover file =
    closeLog file
    file := newLogFileStream() |> Some

let cleanupOldLogs() =
    let files = 
        let log_path = log_path
        Directory.GetFiles(log_path)
        |> Seq.filter (fun f -> Path.GetExtension(f) = log_ext)
        |> Seq.map    (fun f -> FileInfo(f))
        |> Seq.sortBy (fun fi -> - fi.CreationTime.ToFileTime())
    if Seq.isEmpty files |> not then
        files 
        |> Seq.skip 1    //ignore the latest log file
        |> Seq.scan      (fun (acc,f) fi  -> (acc + fi.Length, Some(fi.FullName))) (0L,None) //accumulate file sizes
        |> Seq.skipWhile (fun (acc,_) -> acc < max_logs_size_mb * 1000000L)
        |> Seq.choose snd
        |> Seq.iter File.Delete

type private LogMsg =  Log of string | CloseLog of AsyncReplyChannel<unit> | Rollover

let private rolloverAgent (inbox:MailboxProcessor<LogMsg>) = 
    async{
        while true do
            do! Async.Sleep rollover_time
            inbox.Post Rollover
        }

let private logProcessor (inbox:MailboxProcessor<LogMsg>)  =
    let file = ref None
    async {
        while true do 
            try
                let! msg = inbox.Receive()
                match msg with
                | Log s     -> logToFile file s
                | Rollover -> 
                    rollover file
                    cleanupOldLogs()
                | CloseLog rc -> 
                    closeLog file
                    rc.Reply()
            with ex -> Console.WriteLine ex.Message
            }

let private logAgent = 
    let mb = MailboxProcessor.Start logProcessor
    rolloverAgent mb |> Async.Start
    mb

//API
let log (tag:string) (desc:string) = 
    let msg = sprintf "%A %s : %s" DateTime.Now tag desc 
    logAgent.Post (Log msg)   


let logex (tag:string) (ex:Exception) = 
    log tag ex.Message
    let msg = sprintf "%s" ex.StackTrace
    logAgent.Post (Log msg)   


let terminateLog() = logAgent.PostAndReply(fun rc -> CloseLog rc)


