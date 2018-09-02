/// Dispatch functions for spouts and bolts
module FsShelter.Dispatch

open System
open FsShelter.Multilang

let private log out level msg = Log(msg, level) |> out

/// Dispatch spout commands and handle retries
let reliableSpout mkArgs mkAcker next getStream conf out = 
    let args = mkArgs (log out) conf
    let ack, nack = mkAcker args
    function
    | Next -> 
        next args
        |> Option.iter (fun (tid, tuple) -> Emit(tuple, Some tid, [], (getStream tuple), None, None) |> out)
    | Ack tid -> 
        ack tid
        Sync |> out
    | Nack tid ->
        nack tid
        Sync |> out
    | Activate | Deactivate -> // ignore for now
        Sync |> out
    | msg -> failwithf "Unexpected command: %A" msg

/// Dispatch commands for spouts that don't provide unique ids to emitted tuples
let unreliableSpout mkArgs next getStream conf out = 
    let args = mkArgs (log out) conf
    function
    | Next -> 
        next args
        |> Option.iter (fun tuple -> Emit(tuple, None, [], (getStream tuple), None, None) |> out)
    | Ack _ | Nack _ 
    | Activate | Deactivate -> // ignore for now
        Sync |> out
    | msg -> failwithf "Unexpected command: %A" msg

/// Dispatch bolt commands and auto ack/nack handled messages
let autoAckBolt mkArgs consume (getAnchors,act,deact) getStream conf out = 
    let args = mkArgs (log out) conf
    let unanchoredEmit t = Emit(t, None, [], (getStream t), None, None) |> out
    function
    | Activate when Option.isNone act -> ()
    | Deactivate when Option.isNone deact -> ()
    | Activate ->
        try
            consume (args act.Value unanchoredEmit)
        with ex -> 
            Error("autoBoltRunner: ", ex) |> out
    | Deactivate -> 
        try
            consume (args deact.Value unanchoredEmit)
        with ex -> 
            Error("autoBoltRunner: ", ex) |> out
    | Heartbeat -> Sync |> out
    | Tuple(tuple, id, src, stream, task) -> 
        let emit t = Emit(t, None, getAnchors (src,stream) id, (getStream t), None, None) |> out
        try
            consume (args tuple emit)
            Ok id
        with ex -> 
            Fail id |> out
            Error("autoBoltRunner: ", ex)
        |> out
    | msg -> failwithf "Unexpected command: %A" msg

/// Dispatch bolt commands and auto-nack all incoming messages
let autoNackBolt mkArgs consume conf out = 
    let args = mkArgs (log out) conf
    function
    | Activate | Deactivate -> () // ignore for now
    | Heartbeat -> Sync |> out
    | Tuple(tuple, id, src, stream, task) -> 
        try
            consume (args tuple)
        with ex ->
            Error("autoBoltRunner: ", ex) |> out
            Fail id |> out
    | msg -> failwithf "Unexpected command: %A" msg
