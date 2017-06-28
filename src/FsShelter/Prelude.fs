[<AutoOpen>]
module FsShelter.Prelude

let internal memoize f =
    let dict = System.Collections.Concurrent.ConcurrentDictionary<_,_>()
    fun x -> dict.GetOrAdd(x, lazy (f x)).Force()

module internal Map =
    let join map =
        Map.fold (fun acc key value -> Map.add key value acc) map

    let choose f =
        Map.fold (fun acc key value -> f key value |> function Some v -> acc |> Map.add key v | _ -> acc) Map.empty

    let toValueArray map =
        Map.toSeq map |> Seq.map (fun (_,value) -> value) |> Seq.toArray

    let groupBy f =
        Map.toSeq 
        >> Seq.groupBy f 
        >> Seq.map (fun (k,xs) -> k,xs |> Seq.map fst |> Seq.cache) 
        >> Map.ofSeq

    let chooseBy f =
        Map.toSeq 
        >> Seq.map f 
        >> Seq.groupBy (function Some (k,v) -> Some k | _ -> None) 
        >> Seq.choose (function (Some k,xs) -> Some (k, xs |> Seq.choose (Option.map snd) |> Seq.cache) | _ -> None) 
        >> Map.ofSeq

    let selectBy f =
        Map.toSeq 
        >> Seq.map f 
        >> Seq.groupBy (fun (k,v) -> k) 
        >> Seq.map (fun (k,xs) -> (k, xs |> Seq.map snd |> Seq.cache)) 
        >> Map.ofSeq

module internal Async =
    let map cont async1 =
        async {
            let! r = async1
            return cont r
        }

module internal Seq =
    let  toDict (s:seq<_*_>) = System.Linq.Enumerable.ToDictionary(s, fst, snd)

    let apply v =
        Seq.iter (fun f -> f v)

module internal Exception =
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