/// Converters into GraphViz
module FsShelter.DotGraph

open Topology

/// send the topology to a given writer in GraphViz DOT format
let exportToDot (writeHeader,writeFooter,writeSpout,writeBolt,writeStream) (writer : #System.IO.TextWriter) (topology : Topology<_>) = 
    writeHeader writer topology
    topology.Spouts |> Map.iter (writeSpout writer)
    topology.Bolts |> Map.iter (writeBolt writer)
    topology.Streams |> Map.iter (writeStream writer)
    writeFooter writer topology

/// opening graph statement
let writeHeader writer (t : Topology<_>) = 
    fprintfn writer "digraph %s {" (t.Name)
    fprintfn writer "\tpage=\"40,60\"; "
    fprintfn writer "\tratio=auto;"
    fprintfn writer "\trankdir=LR;"
    fprintfn writer "\tfontsize=10;"

/// closing graph statement
let writeFooter writer (_ : Topology<_>) = 
    fprintfn writer "}"

/// node statement for a spout
let writeSpout writer id (s:Spout<_>) = 
    fprintfn writer "\t%s [color=blue; label=\"%s\n[%d]\"]" id id s.Parallelism

/// node statement for a bolt
let writeBolt writer id (b:Bolt<_>) = 
    fprintfn writer "\t%s [color=green; label=\"%s\n[%d]\"]" id id b.Parallelism

/// edge statement for a stream
let writeStream writer id (st:Stream<_>) = 
    let style = function
        | true -> "solid"
        | false -> "dotted"
    fprintfn writer "\t%s -> %s [label=%s; style=%s]" st.Src st.Dst id (style st.Anchoring)

/// put together default implementations to write to STDOUT
let writeToConsole t = 
    t |> exportToDot (writeHeader,writeFooter,writeSpout,writeBolt,writeStream) System.Console.Out
