namespace FsShelter

module TraceLog = 
    open System
    open System.IO

    // start simple file logger that writes into a file in ~/logs
    let startLog logFile =
        MailboxProcessor.Start( fun inbox -> 
            async {
                use writer = new StreamWriter(new FileStream(logFile,FileMode.Create,FileAccess.Write, FileShare.Read))
                while true do
                    let! ts,text = inbox.Receive()
                    text() |> (sprintf "%s %s" ts) |> writer.WriteLine
                    writer.Flush()
            })

    let inline private ts() = DateTime.Now.ToString("HH:mm:ss.ff")
    
    // log specified text asynchronously
    let mb = startLog "trace.log"
     
    
    let asyncLog fmt =
        (ts(),fmt) |> mb.Post
