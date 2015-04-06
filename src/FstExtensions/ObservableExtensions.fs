module Observable
open System

let createObservableAgent<'T> (token:System.Threading.CancellationToken) =
    let finished = ref false
    let subscribers = ref (Map.empty : Map<int, IObserver<'T>>)

    let inline publish msg = 
        !subscribers 
        |> Seq.iter (fun (KeyValue(_, sub)) ->
            try
                    sub.OnNext(msg)
            with ex -> 
                System.Diagnostics.Debug.Write(ex))

    let completed() = 
        lock subscribers (fun () ->
        finished := true
        !subscribers |> Seq.iter (fun (KeyValue(_, sub)) -> sub.OnCompleted())
        subscribers := Map.empty)

    token.Register(fun () -> completed()) |> ignore //callback for when token is cancelled
            
    let count = ref 0
    let agent =
        MailboxProcessor.Start
            ((fun inbox ->
                async {
                    while true do
                        let! msg = inbox.Receive()
                        publish msg} ),
                token)
    let obs = 
        { new IObservable<'T> with 
            member this.Subscribe(obs) =
                let key1 =
                    lock subscribers (fun () ->
                        if !finished then failwith "Observable has already completed"
                        let key1 = !count
                        count := !count + 1
                        subscribers := subscribers.Value.Add(key1, obs)
                        key1)
                { new IDisposable with  
                    member this.Dispose() = 
                        lock subscribers (fun () -> 
                            subscribers := subscribers.Value.Remove(key1)) } }
    obs,agent.Post

