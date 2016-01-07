namespace Storm.IO

module internal Common =
    open System
    open System.IO
    open Storm.TupleSchema

    let sync_out = 
        let mb = MailboxProcessor.Start (fun inbox ->
            async {
                let out = Console.OpenStandardOutput()
                while true do
                    let! (writer:Stream*TextWriter->unit) = inbox.Receive()
                    writer (out,Console.Out)
            })
        mb.Post
        