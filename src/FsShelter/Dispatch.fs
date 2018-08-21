﻿/// Dispatch loops for spouts and bolts
module FsShelter.Dispatch

open System
open FsShelter.Multilang
open System.Threading

let private log out' level msg = Log(msg, level) |> out'

let mkSerialNext next args out toEmit =
    let mutable err = None
    let event = new ManualResetEvent(false)
    let rec loop() =
        async {
            try 
                let! _ = Async.AwaitWaitHandle event
                let t = next args
                event.Reset() |> ignore            
                t |> Option.iter (toEmit >> out)
                Sync |> out
            with 
                ex -> err <- Some ex
            return! loop()
        }
    loop () |> Async.Start
    fun () ->
        match err with
        | Some e -> raise e
        | _ -> event.Set() |> ignore

/// Dispatch spout commands and handle retries
let reliableSpoutLoop mkArgs mkAcker next getStream (in', out') conf = 
    let args = mkArgs (log out') conf
    let ack, nack = mkAcker args
    let callNext = mkSerialNext next args out' (fun (tid, tuple) -> Emit(tuple, Some tid, [], (getStream tuple), None, None))
    while true do
        let msg = in'()
        match msg with
        | Next -> 
            callNext()
        | Ack tid -> 
            ack tid
            Sync |> out'
        | Nack tid ->
            nack tid
            Sync |> out'
        | Activate | Deactivate -> // ignore for now
            Sync |> out'
        | _ -> failwithf "Unexpected command: %A" msg

/// Dispatch commands for spouts that don't provide unique ids to emitted tuples
let unreliableSpoutLoop mkArgs next getStream (in', out') conf = 
    let args = mkArgs (log out') conf
    let callNext = mkSerialNext next args out' (fun tuple -> Emit(tuple, None, [], (getStream tuple), None, None))
    while true do
        let msg = in'()
        match msg with
        | Next -> 
            callNext()
        | Ack _ | Nack _ 
        | Activate | Deactivate -> // ignore for now
            Sync |> out'
        | _ -> failwithf "Unexpected command: %A" msg

/// Dispatch bolt commands and auto ack/nack handled messages
let autoAckBoltLoop mkArgs consume (getAnchors,act,deact) getStream (in', out') conf = 
    let args = mkArgs (log out') conf
    let unanchoredEmit t = Emit(t, None, [], (getStream t), None, None) |> out'
    while true do
        let msg = in'()
        match msg with
        | Activate when Option.isNone act -> ()
        | Deactivate when Option.isNone deact -> ()
        | Activate ->
            try
                consume (args act.Value unanchoredEmit)
            with ex -> 
                Error("autoBoltRunner: ", ex) |> out'
        | Deactivate -> 
            try
                consume (args deact.Value unanchoredEmit)
            with ex -> 
                Error("autoBoltRunner: ", ex) |> out'
        | Heartbeat -> Sync |> out'
        | Tuple(tuple, id, src, stream, task) -> 
            let emit t = Emit(t, None, getAnchors (src,stream) id, (getStream t), None, None) |> out'
            try
                consume (args tuple emit)
                Ok id
            with ex -> 
                Fail id |> out'
                Error("autoBoltRunner: ", ex)
            |> out'
        | _ -> failwithf "Unexpected command: %A" msg

/// Dispatch bolt commands and auto-nack all incoming messages
let autoNackBoltLoop mkArgs consume (in', out') conf = 
    let args = mkArgs (log out') conf
    while true do
        let msg = in'()
        match msg with
        | Activate | Deactivate -> () // ignore for now
        | Heartbeat -> Sync |> out'
        | Tuple(tuple, id, src, stream, task) -> 
            try
                consume (args tuple)
            with ex ->
                Error("autoBoltRunner: ", ex) |> out'
                Fail id |> out'
        | _ -> failwithf "Unexpected command: %A" msg
