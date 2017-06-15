[<AutoOpen>]
module FsShelter.Prelude

module internal Map =
    let join map =
        Map.fold (fun acc key value -> Map.add key value acc) map

    let choose f =
        Map.fold (fun acc key value -> f key value |> function Some v -> acc |> Map.add key v | _ -> acc) Map.empty

    let toValueArray map =
        Map.toSeq map |> Seq.map (fun (_,value) -> value) |> Seq.toArray

    let groupBy f =
        Map.toSeq >> Seq.groupBy f >> Seq.map (fun (k,xs) -> k,xs |> Seq.map fst |> Seq.cache) >> Map.ofSeq

module internal Async =
    let map cont async1 =
        async {
            let! r = async1
            return cont r
        }

module internal Seq =
    let  toDict (s:seq<_*_>) = System.Linq.Enumerable.ToDictionary(s, fst, snd)
