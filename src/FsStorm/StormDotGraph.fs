module StormDotGraph

open StormDSL

/// send the topology to a given writer in GraphViz DOT format
let exportToDot writeHeader writeFooter writeSpout writeBolt writeStream (writer : System.IO.TextWriter) 
    (topology : Topology) = 
    writeHeader writer topology
    topology.Spouts |> Seq.iter (writeSpout writer)
    topology.Bolts |> Seq.iter (writeBolt writer)
    topology.Bolts
    |> List.collect (fun b -> (b.Inputs |> List.map (fun (sid, grouping) -> (sid, grouping, b.Id))))
    |> List.iter (writeStream writer)
    writeFooter writer topology

/// opening graph statement
let writeHeader writer (t : Topology) = 
    fprintfn writer "digraph %s {" (t.TopologyName)
    fprintfn writer "\tpage=\"40,60\"; "
    fprintfn writer "\tratio=auto;"
    fprintfn writer "\trankdir=LR;"
    fprintfn writer "\tfontsize=10;"

/// closing graph statement
let writeFooter writer (_ : Topology) = fprintfn writer "}"

/// node statement for a spout
let writeSpout writer (s : SpoutSpec) = fprintfn writer "\t%s [color=blue; label=\"%s\n[%d]\"]" s.Id s.Id s.Parallelism

/// node statement for a bolt
let writeBolt writer (b : BoltSpec) = fprintfn writer "\t%s [color=green; label=\"%s\n[%d]\"]" b.Id b.Id b.Parallelism

/// edge statement for a stream
let writeStream writer (src : StreamId, _ : Grouping, dst) = 
    let (id, stream) = 
        (match src with
         | DefaultStream id -> (id, "")
         | Stream(id, name) -> (id, sprintf "[label=%s]" name))
    fprintfn writer "\t%s -> %s %s" id dst stream

/// put together default implementations to write to STDOUT
let writeToConsole = exportToDot writeHeader writeFooter writeSpout writeBolt writeStream System.Console.Out
