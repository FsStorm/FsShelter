namespace FsShelter.IO

open System
open System.Reflection
open System.Runtime.CompilerServices

[<assembly: InternalsVisibleTo("FsShelter.Tests")>]
do()

module internal Common =
    open System.IO
    
    let serialOut = 
        let mb = MailboxProcessor.Start (fun inbox ->
            async {
                while true do
                    let! (write:unit->unit) = inbox.Receive()
                    write()
            })
        mb.Post

    open Nessos.FsPickler

    let private blobSerializer = FsPickler.CreateBinarySerializer()
    let blobSerialize o = 
        use ms = new MemoryStream()
        blobSerializer.SerializeUntyped(ms, o, (FsPickler.GeneratePickler (o.GetType())))
        ms.GetBuffer()

    let blobDeserialize t (bytes:byte[]) = 
        use ms = new MemoryStream(bytes)
        blobSerializer.DeserializeUntyped(ms, (FsPickler.GeneratePickler t))
