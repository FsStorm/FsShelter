/// Async dispatch functions for spouts and bolts
module FsShelter.DispatchAsync

open System.Threading.Tasks
open FsShelter.Multilang

let private log out level msg = Log(msg, level) |> out

/// Dispatch spout commands and handle acknowledgements (async next)
let reliableSpout mkArgs mkAcker deactivate next getStream conf out = 
    let mutable current : InCommand<'t> -> Task<unit> = fun _ -> task { return () }
    let rec inactive msg =
        task {
            match msg with
            | Activate ->
                current <- 
                    let args = mkArgs (log out) conf
                    let ack, nack = mkAcker args
                    fun msg ->
                        task {
                            match msg with
                            | Next -> 
                                let! result = next args
                                result
                                |> Option.iter (fun (tid, tuple) -> Emit(tuple, Some tid, [], getStream tuple, None, None) |> out)
                            | Ack tid -> 
                                ack tid
                            | Nack tid ->
                                nack tid
                            | Activate ->
                                log out LogLevel.Error ("The spout is already active")
                            | Deactivate ->
                                deactivate args
                                current <- inactive
                            | msg -> failwithf "Unexpected command: %+A" msg
                        }
            | msg ->
                log out LogLevel.Error (sprintf "Unsupported command for an inactive spout: %+A" msg)
        }
    current <- inactive        
    fun msg ->
        task {
            do! current msg
            Sync |> out
        }

/// Dispatch commands for spouts that don't provide unique ids (async next)
let unreliableSpout mkArgs deactivate next getStream conf out = 
    let mutable current : InCommand<'t> -> Task<unit> = fun _ -> task { return () }
    let rec inactive msg =
        task {
            match msg with
            | Activate ->
                current <- 
                    let args = mkArgs (log out) conf
                    fun msg ->
                        task {
                            match msg with
                            | Next -> 
                                let! result = next args
                                result
                                |> Option.iter (fun tuple -> Emit(tuple, None, [], getStream tuple, None, None) |> out)
                            | Ack _ | Nack _ ->
                                ()
                            | Activate ->
                                log out LogLevel.Error ("The spout is already active")
                            | Deactivate ->
                                deactivate args
                                current <- inactive
                            | msg -> failwithf "Unexpected command: %+A" msg
                        }
            | msg ->
                log out LogLevel.Error (sprintf "Unsupported command for an inactive spout: %+A" msg)
        }
    current <- inactive        
    fun msg ->
        task {
            do! current msg
            Sync |> out
        }

/// Dispatch bolt commands and auto ack/nack handled messages (async consume)
let autoAckBolt mkArgs consume (getAnchors,act,deact) getStream conf out = 
    let args = mkArgs (log out) conf
    let unanchoredEmit t = Emit(t, None, [], getStream t, None, None) |> out
    fun msg ->
        task {
            match msg with
            | Activate when Option.isNone act -> ()
            | Deactivate when Option.isNone deact -> ()
            | Activate ->
                try
                    do! consume (args act.Value unanchoredEmit)
                with ex -> 
                    Error("autoAckBolt was unable to Activate: ", ex) |> out
            | Deactivate -> 
                try
                    do! consume (args deact.Value unanchoredEmit)
                with ex -> 
                    Error("autoAckBolt was unable to Deactivate: ", ex) |> out
            | Heartbeat -> Sync |> out
            | Tuple(tuple, id, src, stream, task) -> 
                let emit t = Emit(t, None, getAnchors (src,stream) id, getStream t, None, None) |> out
                try
                    do! consume (args tuple emit)
                    Ok id |> out
                with ex -> 
                    Fail id |> out
                    Error(sprintf "autoAckBolt was unable to handle %A: " tuple, ex) |> out
            | msg -> failwithf "Unexpected command: %A" msg
        }

/// Dispatch bolt commands and auto-nack all incoming messages (async consume)
let autoNackBolt mkArgs consume conf out = 
    let args = mkArgs (log out) conf
    fun msg ->
        task {
            match msg with
            | Activate | Deactivate -> ()
            | Heartbeat -> Sync |> out
            | Tuple(tuple, id, src, stream, task) -> 
                try
                    do! consume (args tuple)
                with ex ->
                    Error(sprintf "autoNackBolt was unable to handle %A: " tuple, ex) |> out
                Fail id |> out
            | msg -> failwithf "Unexpected command: %A" msg
        }
