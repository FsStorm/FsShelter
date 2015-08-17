///Support for unit-testing of components
module StormTest

open Storm
open FsJson

/// test runner for reliable spouts
let reliableSpoutRunner reliableEmit (cmds:Json seq) housekeeper fCreateEmitter =
    async {
        try 
            let next = fCreateEmitter (reliableEmit housekeeper)
            for cmd in cmds do
                match cmd?command.Val with
                | NEXT            -> do! next()
                | ACK | FAIL | "" -> housekeeper cmd
                | _ -> failwithf "invalid cmd %A" cmd
        with ex ->
            return! stormLogAndThrow (nestedExceptionTrace ex) ()
    }

/// test runner loop for simple spouts
let simpleSpoutRunner emit (cmds:Json seq) fCreateEmitter =
    async {
        try 
            let next = fCreateEmitter emit
            for cmd in cmds do
                match cmd?command.Val with
                | NEXT            -> do! next()
                | ACK | FAIL | "" -> ()
                | _ -> failwithf "invalid cmd %A" cmd
        with ex ->
            return! stormLogAndThrow (nestedExceptionTrace ex) ()
    }

/// test bolt runner that auto acks received msgs
let autoAckBoltRunner testData stormAck fReaderCreator =
    async {
        try
            let reader = fReaderCreator 
            for jmsg in testData do
                match jmsg,jmsg?stream with
                | _, JsonString "__heartbeat" -> ()
                | x, _ when isArray x         -> () 
                | m, _                        -> 
                    do! reader jmsg 
                    match jmsg?id with
                    | JsonString str -> stormAck str
                    | _ -> ()
         with ex ->
            return! stormLogAndThrow (nestedExceptionTrace ex) ()
    }

/// source the touple - set the comp propery
let src name (msg : Json) = msg?comp <- jval name
/// multilang empty tuple for __heartbeat stream
let heartbeat = tuple [] |> namedStream "__heartbeat"
/// multilang next command
let next = jval [ "command", NEXT ]
