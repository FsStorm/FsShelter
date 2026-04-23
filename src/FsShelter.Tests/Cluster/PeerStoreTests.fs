module FsShelter.Cluster.PeerStoreTests

open System
open System.IO
open NUnit.Framework
open Swensen.Unquote
open FsShelter.Cluster

let private tempDir () =
    let p = Path.Combine(Path.GetTempPath(), "fsshelter-cluster-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory p |> ignore
    p


module PeerIdStore =

    [<Test>]
    let ``loadOrCreate persists across calls``() =
        let dir = tempDir()
        try
            let first =
                match PeerIdStore.loadOrCreate dir with
                | Ok id -> id
                | Error e -> Assert.Fail (sprintf "%A" e); failwith "unreachable"
            let second =
                match PeerIdStore.loadOrCreate dir with
                | Ok id -> id
                | Error e -> Assert.Fail (sprintf "%A" e); failwith "unreachable"
            first =! second
        finally
            try Directory.Delete(dir, true) with _ -> ()

    [<Test>]
    let ``loadOrCreate reports corruption``() =
        let dir = tempDir()
        try
            File.WriteAllText(Path.Combine(dir, "peer-id.dat"), "not-a-guid")
            match PeerIdStore.loadOrCreate dir with
            | Error (CorruptedFile _) -> ()
            | other -> Assert.Fail (sprintf "expected CorruptedFile, got %A" other)
        finally
            try Directory.Delete(dir, true) with _ -> ()


module PeersCache =

    [<Test>]
    let ``save and load round-trip``() =
        let dir = tempDir()
        try
            let endpoints = [ { Host = "a"; Port = 1 }; { Host = "b"; Port = 2 } ]
            match PeersCache.save dir endpoints with
            | Ok () -> ()
            | Error e -> Assert.Fail (sprintf "%A" e)
            match PeersCache.load dir with
            | Ok loaded -> loaded =! endpoints
            | Error e -> Assert.Fail (sprintf "%A" e)
        finally
            try Directory.Delete(dir, true) with _ -> ()

    [<Test>]
    let ``load on missing file returns empty``() =
        let dir = tempDir()
        try
            match PeersCache.load dir with
            | Ok [] -> ()
            | other -> Assert.Fail (sprintf "expected empty, got %A" other)
        finally
            try Directory.Delete(dir, true) with _ -> ()
