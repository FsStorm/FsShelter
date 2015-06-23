module SampleComponents

//defines the spout and bolt 
open FsJson
open Storm

let rnd = new System.Random() // used for generating random messages

//spout - produces messages
let spout runner cfg = 
    //define the function that will return the emitter function
    //cfg: the configution passed in by storm
    //emit: a function that emits message to storm
    let createEmitter (cfg : Configuration) emit = 
        fun () ->
            async { 
                tuple [rnd.Next(0, 100) ] |> emit
            }

    //run the spout
    runner cfg createEmitter

//bolt - consumes and emits messages
let addOneBolt runner log emit cfg  = 
    //define the function that will return the consumer function
    let createAdder (cfg : Configuration) = 
        //accept messages function
        fun (msg:Json) -> 
            async { 
                log "msg" (sprintf "%A" msg)
                tuple [msg?tuple.[0].ValI + 1] |> emit
            }
    //run spout
    runner cfg createAdder

//bolt - consumes messages
let resultBolt runner log cfg = 
    //define the function that will return the consumer function
    let createReader (cfg : Configuration) = 
        //accept messages function
        fun (msg:Json) -> 
            async { log "x" (sprintf "%A" msg?tuple.[0].ValI) }
    //run spout
    runner cfg createReader
