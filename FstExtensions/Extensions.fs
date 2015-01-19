module FstExtensions
open System.Threading.Tasks

let AsyncAwaitVoidTask (task : Task) =
    Async.AwaitIAsyncResult(task) |> Async.Ignore

let inline awaitPlainTask (task: Task) = 
    // rethrow exception from preceding task if it fauled
    let continuation (t : Task) : unit =
        match t.IsFaulted with
        | true -> raise t.Exception
        | arg -> ()
    task.ContinueWith continuation |> Async.AwaitTask
 
let inline startAsPlainTask (work : Async<unit>) = Task.Factory.StartNew(fun () -> work |> Async.RunSynchronously)

type Agent<'a> = MailboxProcessor<'a>
type RC<'a>    = AsyncReplyChannel<'a>

type System.Net.HttpWebRequest with
    member x.AsyncGetRequestStream() = Async.FromBeginEnd(x.BeginGetRequestStream,x.EndGetRequestStream)

module Seq =
    let chunk n xs = seq {
        let i = ref 0
        let arr = ref <| Array.create n (Unchecked.defaultof<'a>)
        for x in xs do
            if !i = n then 
                yield !arr
                arr := Array.create n (Unchecked.defaultof<'a>)
                i := 0 
            (!arr).[!i] <- x
            i := !i + 1
        if !i <> 0 then
            yield (!arr).[0..!i-1] }