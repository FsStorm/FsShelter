(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use 
// it to define helpers that you do not want to show in the documentation.
#I "../../bin"
#r "FsShelter.dll"

(**
Implementing custom reliability semantics
========================
The reliable spout implementation for a custom source, like a queue (RabbitMQ, Kafka, etc) needs to obtain the event id from the source and forward Storm's acks and nacks to the source, which could be accomplished in FsShelter with:

 a housekeeper
*)

open FsShelter

/// Maintains reliability semantics of a queueSpout.
/// ack: thread-safe method to ack a message by id.
/// nack: thread-safe method to nack a message by id.
let createQueueHousekeeper ack nack = 
	fun (msg:Json) ->
		match msg?command.Val with
		| "ack" -> ack (int64 (msgId msg))
		| "fail" -> nack (int64 (msgId msg))
		| _ -> ()
	
(**
 and custom runner
*)

/// Partially configures a reliable runner for the queues.
/// ack: thread-safe method to ack a message by id.
/// nack: thread-safe method to nack a message by id.
let createQueueRunner ack nack = Storm.reliableSpoutRunner 
                                        (createQueueHousekeeper (uint64 >> ack) (uint64 >> nack))


(**
 then given an event type

*)
type Event<'m> = { msg:'m; id:uint64 }

(**
 we can declare a spout factory like this:

*)

/// Reads message from a queue into a tuple stream.
/// get: thread-safe event consumer.
/// toTuple: message object to Json tuple converter.
let createQueueSpout get toTuple emit = 
	fun () -> 
		async { 
			match get() with
			| Some evt -> toTuple evt.msg |> emit (int64 evt.id)
			| None -> do ()
		}

(**
 and a queue/consumer interface

*)
/// this is the consumer interface into the queue
/// that returns three functions: get:unit->Event, ack:uint64->unit and nack:unit64->unit
let getConsumer cfg = 
	((fun () -> { msg = 1b; id = 0ul }), //get the next message and its id
	 (fun id -> ()), // forward the ack to the queue
	 (fun id -> ())) // forward the nack to the queue

(**
 we can implement the actual spout like this:

*)

let statusEvents runner getConsumer (cfg : Configuration) =
    let toTuple (id:string,status:byte) = 
        tuple [ id ] 
        |> namedStream (match char status with | '1' -> "online" | _ -> "offline")
    let get, ack, nack = getConsumer cfg
    createQueueSpout get toTuple |> runner ack nack

(**
Passing the createQueueRunner from the topology will tie all the pieces together and the ids, acks and nacks should start flowing from the source and back.

*)
