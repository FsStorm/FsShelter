[<AutoOpen>]
module internal FsShelter.Prelude

let memoize f =
    let dict = System.Collections.Generic.Dictionary<_,_>()
    fun x ->
        match dict.TryGetValue(x) with
        | true, v -> v
        | _ -> let v = f x in dict.[x] <- v; v

module Map =
    let join map =
        Map.fold (fun acc key value -> Map.add key value acc) map

    let groupBy toGroup toValue =
        Map.toSeq 
        >> Seq.groupBy toGroup 
        >> Seq.map (fun (k,xs) -> k,xs |> Seq.map toValue |> Seq.cache) 
        >> Map.ofSeq

module Async =
    let map cont async1 =
        async {
            let! r = async1
            return cont r
        }

module Seq =
    let  toDict (s:seq<_*_>) = System.Linq.Enumerable.ToDictionary(s, fst, snd)

    let apply v =
        Seq.iter (fun f -> f v)

module Exception =
    open System
    // produce the nested exception's stack trace 
    let toString (ex:Exception) =
        let sb = System.Text.StringBuilder()
        let rec loop (ex:Exception) =
            sb.AppendLine(ex.Message).AppendLine(ex.StackTrace) |> ignore
            if isNull ex.InnerException then
                sb.ToString()
            else
                sb.AppendLine("========") |> ignore
                loop ex.InnerException
        loop ex


[<assembly: System.Runtime.CompilerServices.InternalsVisibleTo("FsShelter.Tests")>]
[<assembly: System.Runtime.CompilerServices.InternalsVisibleTo("FsShelter.Disruptor")>]
do ()
