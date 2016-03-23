module FsShelter.CommonTests

open NUnit.Framework
open Swensen.Unquote
open FsShelter.TestTopology
open FsShelter.Multilang
open System
open System.IO
open TupleSchema

let toDict (s:seq<_*_>) = System.Linq.Enumerable.ToDictionary(s, fst, snd)

let syncOut (w:unit->unit) = w()

let justFields = 
    let today = DateTime.Today
    JustFields(
            62,
            42.0,
            Guid.Empty, 
            Guid.Empty, 
            "abcd", 
            ("xyz " |> String.replicate 5), 
            ("efg " |> String.replicate 10), 
            today, 
            (DateTimeOffset today), 
            (DateTimeOffset today))

let mkStreams() =
    let memin = new MemoryStream()
    let memout = new MemoryStream()
    (memin,memout)

[<Test>]
let ``roundtrips record``() = 
    let r = {x = 1}
    let r' = IO.Common.blobSerialize r
             |> (IO.Common.blobDeserialize typeof<Number> >> unbox)
    r =! r'

[<Test>]
let ``roundtrips list``() = 
    let r = [{x = 1}]
    let r' = IO.Common.blobSerialize r
             |> (IO.Common.blobDeserialize typeof<Number list> >> unbox)
    r =! r'

[<Test>]
let ``roundtrips bcl list``() = 
    let r = Collections.Generic.List([{x = 1}])
    let r' = IO.Common.blobSerialize r
             |> (IO.Common.blobDeserialize typeof<Collections.Generic.List<Number>> >> unbox<Collections.Generic.List<_>>)
    r'.Count =! 1
    r.[0] =! r'.[0]

[<Test>]
let ``roundtrips map``() = 
    let r = Map [1, Some {x = 1}; 2, None]
    let r' = IO.Common.blobSerialize r
             |> (IO.Common.blobDeserialize typeof<Map<int,Number option>> >> unbox)
    r =! r'

[<Test>]
let ``roundtrips dictionary``() = 
    let r = toDict [1, Some {x = 1}; 2, None]
    let r' = IO.Common.blobSerialize r
             |> (IO.Common.blobDeserialize typeof<Collections.Generic.Dictionary<int, Number option>> >> unbox<Collections.Generic.Dictionary<_,_>>)
    r'.Count =! 2
    r.[1] =! r'.[1]
    r.[2] =! r'.[2]

