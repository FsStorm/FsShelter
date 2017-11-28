namespace FsShelter.IO

open System
open System.Runtime.CompilerServices

[<assembly: InternalsVisibleTo("FsShelter.Tests")>]
do()

module internal Common =
    open System.IO
    open Hopac
    
    let serialOut = 
        let mb = Mailbox()
        job {
            let! (write:unit->unit) = Mailbox.take mb
            write()
        } |> Job.foreverServer |> start
        Mailbox.send mb

    open MBrace.FsPickler
    open FSharp.Reflection

    let private blobSerializer = FsPickler.CreateBinarySerializer()
    let blobSerialize o = 
        use ms = new MemoryStream()
        let t = o.GetType()
        let t' = if FSharpType.IsUnion t && t.BaseType <> typeof<obj> then t.BaseType else t
        blobSerializer.SerializeUntyped(ms, o, FsPickler.GeneratePickler t')
        ms.GetBuffer()

    let blobDeserialize t (bytes:byte[]) = 
        use ms = new MemoryStream(bytes)
        blobSerializer.DeserializeUntyped(ms, FsPickler.GeneratePickler t)
