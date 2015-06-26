module SampleComponents

//defines the spout and bolt 
open FsJson
open Storm

let rnd = new System.Random() // used for generating random messages

///spout - produces messages
///cfg: the configution passed in by storm
let spout runner cfg = 
    //define the function that will return the emitter function
    //emit: a function that emits message to storm
    let createEmitter emit = fun () -> async { tuple [ rnd.Next(0, 100) ] |> emit }
    //run the spout
    createEmitter |> runner

///bolt - consumes and emits messages
///cfg: the configution passed in by storm
let addOneBolt runner log emit cfg = 
    //define the function that will return the consumer function
    let createAdder = 
        //accept messages function
        fun (msg : Json) -> 
            async { 
                log "msg" (sprintf "%A" msg)
                tuple [ msg?tuple.[0].ValI + 1 ] |> emit
            }
    //run spout
    createAdder |> runner

///bolt - consumes messages
///cfg: the configution passed in by storm
let resultBolt runner log cfg = 
    //define the function that will return the consumer function
    let createReader = 
        //accept messages function
        fun (msg : Json) -> async { log "x" (sprintf "%A" msg?tuple.[0].ValI) }
    //run spout
    createReader |> runner
