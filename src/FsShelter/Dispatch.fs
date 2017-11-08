/// Dispatch loops for spouts and bolts
module FsShelter.Dispatch

open System
open FsShelter.Multilang

let private log out' level msg = Log(msg, level) |> out'

let mkSerialNext next args out toEmit =
    let mutable err = None
    let mb = MailboxProcessor.Start(fun inbox ->
                let rec loop() =
                    async {
                        let! _ = inbox.Receive()
                        let! t = next args
                        t |> Option.map toEmit |> Option.iter out
                        Sync |> out
                        return! loop()
                    }
                loop ())
    mb.Error.Add(fun e -> err <- Some e)
    fun () ->
        match err with
        | Some e -> raise e
        | _ -> mb.Post()

/// Dispatch spout commands and handle retries
let reliableSpoutLoop mkArgs mkAcker next getStream (in', out') conf = 
    async { 
        let args = mkArgs (log out') conf
        let ack, nack = mkAcker args
        let callNext = mkSerialNext next args out' (fun (tid, tuple) -> Emit(tuple, Some tid, [], (getStream tuple), None, None))
        while true do
            let! msg = in'()
            match msg with
            | Next -> 
                callNext()
            | Ack tid -> 
                ack tid
            | Nack tid ->
                nack tid
            | Activate | Deactivate -> // ignore for now
                Sync |> out'
            | _ -> failwithf "Unexpected command: %A" msg
    }

/// Dispatch commands for spouts that don't provide unique ids to emitted tuples
let unreliableSpoutLoop mkArgs next getStream (in', out') conf = 
    async { 
        let args = mkArgs (log out') conf
        let callNext = mkSerialNext next args out' (fun tuple -> Emit(tuple, None, [], (getStream tuple), None, None))
        while true do
            let! msg = in'()
            match msg with
            | Next -> 
                callNext()
            | Ack _ 
            | Nack _ -> ()
            | Activate | Deactivate -> // ignore for now
                Sync |> out'
            | _ -> failwithf "Unexpected command: %A" msg
    }

/// Dispatch bolt commands and auto ack/nack handled messages
let autoAckBoltLoop mkArgs consume (getAnchors,act,deact) getStream (in', out') conf = 
    async { 
        let args = mkArgs (log out') conf
        let unanchoredEmit t = Emit(t, None, [], (getStream t), None, None) |> out'
        while true do
            let! msg = in'()
            match msg with
            | Activate when Option.isNone act -> ()
            | Deactivate when Option.isNone deact -> ()
            | Activate ->
                let! res = consume (args act.Value unanchoredEmit) |> Async.Catch
                match res with
                | Choice1Of2 _ -> ()
                | Choice2Of2 ex -> 
                    Error("autoBoltRunner: ", ex) |> out'
            | Deactivate -> 
                let! res = consume (args deact.Value unanchoredEmit) |> Async.Catch
                match res with
                | Choice1Of2 _ -> ()
                | Choice2Of2 ex -> 
                    Error("autoBoltRunner: ", ex) |> out'
            | Heartbeat -> Sync |> out'
            | Tuple(tuple, id, src, stream, task) -> 
                let emit t = Emit(t, None, getAnchors (src,stream) id, (getStream t), None, None) |> out'
                let! res = consume (args tuple emit) |> Async.Catch
                match res with
                | Choice1Of2 _ -> Ok id
                | Choice2Of2 ex -> 
                    Fail id |> out'
                    Error("autoBoltRunner: ", ex)
                |> out'
            | _ -> failwithf "Unexpected command: %A" msg
    }

/// Dispatch bolt commands and auto-nack all incoming messages
let autoNackBoltLoop mkArgs consume (in', out') conf = 
    async { 
        let args = mkArgs (log out') conf
        while true do
            let! msg = in'()
            match msg with
            | Activate | Deactivate -> () // ignore for now
            | Heartbeat -> Sync |> out'
            | Tuple(tuple, id, src, stream, task) -> 
                let! res = consume (args tuple) |> Async.Catch
                match res with
                | Choice1Of2 _ -> ()
                | Choice2Of2 ex -> Error("autoBoltRunner: ", ex) |> out'
                Fail id |> out'
            | _ -> failwithf "Unexpected command: %A" msg
    }
