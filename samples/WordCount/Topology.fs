module Sample.Topology

open System

// data schema for the topology, every case is a unqiue stream
type Schema = 
    | Sentence of string
    | Word of string
    | WordCount of string*int64

type SchemaWrapper<'a> =
    | [<FsShelter.NestedStream>] Stream of 'a

// sentences spout - feeds messages into the topology
let sentences source = async { return source() |> Sentence |> Stream |> Some }

// split bolt - consumes sentences and emits words
let splitIntoWords (input, emit) = 
    async { 
        match input with
        | Stream (Sentence s) -> 
            s.Split([|' '|],StringSplitOptions.RemoveEmptyEntries) 
            |> Seq.map (Word >> Stream)
            |> Seq.iter emit
        | _ -> failwithf "unexpected input: %A" input
    }

// count words bolt 
let countWords (input, increment, emit) = 
    async { 
        match input with
        | Stream (Word word) -> (WordCount (word,increment word) |> Stream) |> emit
        | _ -> failwithf "unexpected input: %A" input
    }

// log word count - terminating bolt 
let logResult (log, input) = 
    async { 
        match input with
        | Stream (WordCount (word,count)) -> log (sprintf "%s: %d" word count)
        | _ -> failwithf "unexpected input: %A" input
    }

// data source
let source = 
    let rnd = new System.Random()
    let sentences = [ "the cow jumped over the moon"
                      "an apple a day keeps the doctor away"
                      "four score and seven years ago"
                      "snow white and the seven dwarfs"
                      "i am at two with nature" ]

    fun () -> sentences.[ rnd.Next(0, sentences.Length) ]

// increment word count and return new value
let increment =
    let cache = Collections.Concurrent.ConcurrentDictionary<string,int64 ref>()
    fun word -> 
        let c = cache.GetOrAdd(word, ref 0L) 
        Threading.Interlocked.Increment &c.contents

open FsShelter.DSL
#nowarn "25" // for stream grouping expressions

//define the storm topology
let sampleTopology = 
    topology "WordCount" { 
        let sentencesSpout = 
            sentences |> runSpout (fun log cfg -> source)        // make arguments: ignoring Storm logging and cfg, passing our source function
        
        let splitBolt = 
            splitIntoWords
            |> runBolt (fun log cfg tuple emit -> (tuple, emit)) // make arguments: pass incoming tuple and emit function
            |> withParallelism 2
        
        let countBolt = 
            countWords
            |> runBolt (fun log cfg tuple emit -> (tuple, increment, emit))
            |> withParallelism 2
        
        let logBolt = 
            logResult
            |> runBolt (fun log cfg ->                           // make arguments: pass PID-log and incoming tuple 
                            let mylog = Common.Logging.asyncLog (Diagnostics.Process.GetCurrentProcess().Id.ToString()+"_count")
                            fun tuple emit -> (mylog, tuple))
            |> withParallelism 2
        
        yield sentencesSpout --> splitBolt |> Shuffle.on (Sentence >> Stream)               // emit from sentencesSpout to splitBolt on Sentence stream, shuffle among target task instances
        yield splitBolt --> countBolt |> Group.by (function Stream(Word w) -> w)        // emit from splitBolt into countBolt on Word stream, group by word (into the same task instance)
        yield countBolt --> logBolt |> Group.by (function Stream(WordCount (w,_)) -> w) // emit from countBolt into logBolt on WordCount stream, group by word value
    }
