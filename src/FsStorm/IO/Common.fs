namespace Storm.IO

module internal Common =
    
    let syncOut (out:'o) = 
        let mb = MailboxProcessor.Start (fun inbox ->
            async {
                while true do
                    let! (writer:'o->unit) = inbox.Receive()
                    writer out
            })
        mb.Post
    
