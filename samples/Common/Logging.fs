namespace Common

module Logging = 
    open System
    open System.IO

    // start simple file logger that writes into a file in ~/logs
    let startLog name =
        let logFile = Path.Combine((Environment.GetFolderPath Environment.SpecialFolder.UserProfile), sprintf "logs/%s.log" name)
        Directory.CreateDirectory(Path.GetDirectoryName logFile) |> ignore
        let writer = new StreamWriter(new FileStream(logFile,FileMode.Create,FileAccess.Write, FileShare.Read))
        MailboxProcessor.Start( fun inbox -> 
            async {
                while true do
                    let! ts,text = inbox.Receive()
                    text() |> (sprintf "%s %s" ts) |> writer.WriteLine
                    writer.Flush()
            })

    let inline private ts() = DateTime.Now.ToString("HH:mm:ss.ff")
    
    // log results of the passed function, calling it asynchronously
    let callbackLog name =
        let mb = startLog name
        fun (mkEntry:unit->string) -> (ts(),mkEntry) |> mb.Post 

    // log specified text asynchronously
    let asyncLog (name:string) = 
        let mb = startLog name
        fun text -> (ts(),(fun () -> text)) |> mb.Post 
    
