module SampleComponents

//defines the spout and bolt 
open FsJson
open Storm
open System

let rnd = new System.Random() // used for generating random messages

///spout - produces messages
///cfg: the configution passed in by storm
let spout runner log (cfg:Configuration) = 
    let count = ref 0L
    log "spout:" LogLevel.Debug
    //define the next function
    //emit: a function that emits message to storm
    let next emit = fun () -> async { tuple [ rnd.Next(0, 100) ] |> emit (Threading.Interlocked.Increment &count.contents) }
    //run the spout
    next |> runner

///bolt - consumes and emits messages
///cfg: the configution passed in by storm
let addOneBolt runner log emit cfg = 
    //define the consumer function 
    log "bolt:" LogLevel.Debug
    let add (msg : Json) =
        async { 
//            log "msg" (sprintf "%A" msg)
            let x = msg?tuple.[0].ValI + 1
            tuple [ x ]
            |> namedStream (match x % 2 with | 0 -> "even" | _ -> "odd")
            |> anchor msg
            |> emit // anchor to ensure the entire tuple tree is processed before the spout is ack'ed
        }
    //run the bolt
    add |> runner

///bolt - consumes messages
///cfg: the configution passed in by storm
let resultBolt runner log (cfg:Configuration) = 
    let desc = cfg.Json?conf?desc.Val // the value passed in with the submitted topology Config
    //define the function that will return the consumer 
    let logResult (msg : Json) = async { log desc (sprintf "%A" msg?tuple.[0].ValI) }
    //run the bolt
    logResult |> runner
