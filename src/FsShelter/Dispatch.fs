/// Dispatch loops for spouts and bolts
module FsShelter.Dispatch

open System
open FsShelter.Multilang
open System.Threading
open Hopac
open Hopac.Infixes

let private log out' level msg = Log(msg, level) |> out'

let mkSerialNext (next:_->#Job<_>) args (out:_->Job<_>) toEmit =
    let ch = Ch<unit>()
    ch ^=> fun _ -> job {
        do! next args >>= (function Some t -> toEmit t |> out | _ -> Job.result ())
        do! Sync |> out
    }
    <|> Alt.never ()
    |> Job.foreverServer |> run
    Ch.give ch

/// Dispatch spout commands and handle retries
let reliableSpoutLoop mkArgs mkAcker next getStream (in':unit->Job<_>, out':_->Job<_>) conf = 
    let args = mkArgs (log out') conf
    let ack, nack = mkAcker args
    let callNext = mkSerialNext next args out' (fun (tid, tuple) -> Emit(tuple, Some tid, [], (getStream tuple), None, None))
    job { 
        let! msg = in'()
        match msg with
        | Next -> 
            do! callNext()
        | Ack tid -> 
            ack tid
            do! Sync |> out'
        | Nack tid ->
            nack tid
            do! Sync |> out'
        | Activate | Deactivate -> // ignore for now
            do! Sync |> out'
        | _ -> failwithf "Unexpected command: %A" msg
    } |> Job.foreverServer

/// Dispatch commands for spouts that don't provide unique ids to emitted tuples
let unreliableSpoutLoop mkArgs next getStream (in':unit->Job<_>, out':_->Job<_>) conf = 
    let args = mkArgs (log out') conf
    let callNext = mkSerialNext next args out' (fun tuple -> Emit(tuple, None, [], (getStream tuple), None, None))
    job { 
        let! msg = in'()
        match msg with
        | Next -> 
            do! callNext()
        | Ack _ | Nack _ 
        | Activate | Deactivate -> // ignore for now
            do! Sync |> out'
        | _ -> failwithf "Unexpected command: %A" msg
    } |> Job.foreverServer

/// Dispatch bolt commands and auto ack/nack handled messages
let autoAckBoltLoop mkArgs consume (getAnchors,act,deact) getStream (in':unit->Job<_>, out':_->Job<_>) conf = 
    let args = mkArgs (log out') conf
    let unanchoredEmit t = Emit(t, None, [], (getStream t), None, None) |> out'
    job { 
        let! msg = in'()
        match msg with
        | Activate when Option.isNone act -> ()
        | Deactivate when Option.isNone deact -> ()
        | Activate ->
            let! res = consume (args act.Value unanchoredEmit) |> Job.catch
            match res with
            | Choice1Of2 _ -> ()
            | Choice2Of2 ex -> 
                do! Error("autoBoltRunner: ", ex) |> out'
        | Deactivate -> 
            let! res = consume (args deact.Value unanchoredEmit) |> Job.catch
            match res with
            | Choice1Of2 _ -> ()
            | Choice2Of2 ex -> 
                do! Error("autoBoltRunner: ", ex) |> out'
        | Heartbeat -> 
            do! Sync |> out'
        | Tuple(tuple, id, src, stream, task) -> 
            let emit t = Emit(t, None, getAnchors (src,stream) id, (getStream t), None, None) |> out'
            let! res = consume (args tuple emit) |> Job.catch
            match res with
            | Choice1Of2 _ -> 
                do! Ok id |> out'
            | Choice2Of2 ex -> 
                do! Fail id |> out'
                do! Error("autoBoltRunner: ", ex) |> out'
        | _ -> failwithf "Unexpected command: %A" msg
    } |> Job.foreverServer

/// Dispatch bolt commands and auto-nack all incoming messages
let autoNackBoltLoop mkArgs consume (in':unit->Job<_>, out':_->Job<_>) conf = 
    let args = mkArgs (log out') conf
    job { 
        let! msg = in'()
        match msg with
        | Activate | Deactivate -> () // ignore for now
        | Heartbeat -> 
            do! Sync |> out'
        | Tuple(tuple, id, src, stream, task) -> 
            let! res = consume (args tuple) |> Job.catch
            match res with
            | Choice1Of2 _ -> ()
            | Choice2Of2 ex -> 
                do! Error("autoBoltRunner: ", ex) |> out'
            do! Fail id |> out'
        | _ -> failwithf "Unexpected command: %A" msg
    } |> Job.foreverServer
