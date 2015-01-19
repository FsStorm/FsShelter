module SimpleTestComponents
//defines the spout and bolt 

open FsJson
open Storm

let rnd = new System.Random() // used for generating random messages

//spout - produces messages
let spout cfg = 

    //define the function that will return the emitter function
    //cfg: the configution passed in by storm
    //fStormEmit: a function that emits message to storm
    let fCreateEmitter (cfg:Configuration) fStormEmit =
  
        let producer() = 
            async{ 
                do fStormEmit (jval [Storm.TUPLE,[rnd.Next(0,100)]]) 
                do Storm.stormSync()
                }
        producer
      
    //run the spout
    Storm.simpleSpoutRunner cfg fCreateEmitter
     

//bolt - consumes messages
let bolt cfg =

    //define the function that will return the consumer function
    let fCreateReader (cfg:Configuration) =

        //accept messages function
        let consumer msg = async { Logging.log "msg" (sprintf "%A" msg) }
        consumer

    //run spout
    Storm.autoAckBoltRunner cfg fCreateReader
