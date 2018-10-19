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
let writeStream writer ((_,sid),_) (st:Stream<_>) = 
    let style = function
        | true -> "solid"
        | false -> "dotted"
    fprintfn writer "\t%s -> %s [label=%s; style=%s]" st.Src st.Dst sid (style st.Anchoring)

/// X11 colours
let streamColours = [| "crimson"; "palevioletred"; "mediumvioletred"; "magenta"; "purple"; "slateblue"; "blue"; "royalblue"; "dodgerblue"; "darkturquoise"; "darkslategray"; "teal"; "springgreen"; "seagreen"; "lime"; "forestgreen"; "olivedrab"; "goldenrod"; "darkgoldenrod"; "orange"; "tan"; "saddlebrown"; "orangered"; "lightcoral"; "indianred"; "red"; "maroon"; "dimgray"; "black" |]

/// lookup a colour based on a stream id
let getColour (colours:string []) =
    // FNV-1 Hash function (32-bit) - see https://en.wikipedia.org/wiki/Fowler%E2%80%93Noll%E2%80%93Vo_hash_function
    let fnv1Hash32 (s:string) =
        let fnvPrime32 = 16777619u
        let fnvOffsetBasis32 = 2166136261u
        let xor (hash:uint32) (b:uint32) =
            let hashLower8Bits = 0x000000FFu &&& hash
            let hashUpper24Bits = 0xFFFFFF00u &&& hash
            let lowerXor = hashLower8Bits ^^^ b
            hashUpper24Bits + lowerXor

        s.ToCharArray()
        |> Array.fold (fun hash c ->
            let data = uint32 c
            let h = hash * fnvPrime32
            xor h data) fnvOffsetBasis32

    fun (s:string) ->
        colours.[int ((fnv1Hash32 s) % (uint32 <| colours.Length))]

/// colourized edge statement for a stream
let writeColourfulStream getColour writer ((_, sid), _) (st:Stream<_>) =
    let style = function
        | true -> "solid"
        | false -> "dashed"
    let colour = getColour sid
    fprintfn writer "\t%s -> %s [label=<<font color=\"%s\">%s</font>>; style=%s; color=%s]" st.Src st.Dst colour sid (style st.Anchoring) colour
    
/// put together default implementations to write to STDOUT
let writeToConsole t = 
    t |> exportToDot (writeHeader,writeFooter,writeSpout,writeBolt,writeStream) System.Console.Out

/// put together default implementations with colour to write to STDOUT
let writeColourizedToConsole t = 
    t |> exportToDot (writeHeader,writeFooter,writeSpout,writeBolt,writeColourfulStream <| getColour streamColours) System.Console.Out
