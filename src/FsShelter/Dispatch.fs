﻿/// Dispatch loops for spouts and bolts
module FsShelter.Dispatch

open System
open FsShelter.Multilang

let private log out' level msg = Log(msg, level) |> out'

/// Dispatch spout commands and handle retries
let reliableSpoutLoop mkArgs mkAcker next getStream (in', out') conf = 
    async { 
        let args = mkArgs (log out') conf
        let ack, nack = mkAcker args
        while true do
            let! msg = in'()
            match msg with
            | Next -> 
                let! v = next args
                match v with
                | Some(tid, tuple) -> 
                    Emit(tuple, Some tid, [], (getStream tuple), None, None) |> out' 
                | _ -> ()
                Sync |> out'
            | Ack tid -> 
                ack tid
                Sync |> out'
            | Nack tid ->
                nack tid
                Sync |> out'
            | Activate | Deactivate -> // ignore for now
                Sync |> out'
            | _ -> failwithf "Unexpected command: %A" msg
    }

/// Dispatch commands for spouts that don't provide unique ids to emitted tuples
let unreliableSpoutLoop mkArgs next getStream (in', out') conf = 
    async { 
        let args = mkArgs (log out') conf
        while true do
            let! msg = in'()
            match msg with
            | Next -> 
                let! v = next args
                match v with
                | Some tuple -> 
                    Emit(tuple, None, [], (getStream tuple), None, None) |> out' 
                | _ -> ()
                Sync |> out'
            | Ack _ 
            | Nack _ 
            | Activate | Deactivate -> // ignore for now
                Sync |> out'
            | _ -> failwithf "Unexpected command: %A" msg
    }

/// Dispatch bolt commands and auto ack/nack handled messages
let autoAckBoltLoop mkArgs consume (getAnchors,act,deact) getStream (in', out') conf = 
    async { 
        let args = mkArgs (log out') conf
        while true do
            let! msg = in'()
            match msg with
            | Activate when Option.isNone act -> ()
            | Deactivate when Option.isNone deact -> ()
            | Activate ->
                let! res = consume (args act.Value ignore) |> Async.Catch
                match res with
                | Choice1Of2 _ -> ()
                | Choice2Of2 ex -> 
                    Error("autoBoltRunner: ", ex) |> out'
            | Deactivate -> 
                let! res = consume (args deact.Value ignore) |> Async.Catch
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