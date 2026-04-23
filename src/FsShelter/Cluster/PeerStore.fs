namespace FsShelter.Cluster

open System
open System.IO

type StoreError =
    | IoFailed of path: string * exn: exn
    | CorruptedFile of path: string * reason: string


[<RequireQualifiedAccess>]
module PeerIdStore =

    [<Literal>]
    let private FileName = "peer-id.dat"

    let private path (stateDir: string) = Path.Combine(stateDir, FileName)

    let private tryIo (p: string) (action: unit -> 'a) : Result<'a, StoreError> =
        try Ok (action ())
        with ex -> Error (IoFailed (p, ex))

    /// Read the persisted `peer-id.dat`, or generate + persist a fresh GUID.
    let loadOrCreate (stateDir: string) : Result<PeerId, StoreError> =
        let p = path stateDir
        if File.Exists p then
            tryIo p (fun () -> File.ReadAllText(p).Trim())
            |> Result.bind (fun text ->
                match PeerId.ofString text with
                | Ok id -> Ok id
                | Error e -> Error (CorruptedFile (p, e)))
        else
            let fresh = PeerId.create ()
            tryIo p (fun () ->
                Directory.CreateDirectory stateDir |> ignore
                File.WriteAllText(p, PeerId.toString fresh))
            |> Result.map (fun () -> fresh)


[<RequireQualifiedAccess>]
module PeersCache =

    [<Literal>]
    let private FileName = "peers.dat"

    let private path (stateDir: string) = Path.Combine(stateDir, FileName)

    let private tryIo (p: string) (action: unit -> 'a) : Result<'a, StoreError> =
        try Ok (action ())
        with ex -> Error (IoFailed (p, ex))

    /// Load `peers.dat`; empty on first boot.
    let load (stateDir: string) : Result<PeerEndpoint list, StoreError> =
        let p = path stateDir
        if not (File.Exists p) then
            Ok []
        else
            tryIo p (fun () -> File.ReadAllLines p)
            |> Result.bind (fun lines ->
                lines
                |> Array.map (fun l -> l.Trim())
                |> Array.filter (fun l -> l.Length > 0 && not (l.StartsWith '#'))
                |> Array.fold
                    (fun acc line ->
                        match acc with
                        | Error _ -> acc
                        | Ok eps ->
                            match PeerEndpoint.ofString line with
                            | Ok ep -> Ok (ep :: eps)
                            | Error e -> Error (CorruptedFile (p, e)))
                    (Ok [])
                |> Result.map List.rev)

    /// Overwrite `peers.dat`. Caller batches writes on membership change.
    let save (stateDir: string) (endpoints: PeerEndpoint list) : Result<unit, StoreError> =
        let p = path stateDir
        tryIo p (fun () ->
            Directory.CreateDirectory stateDir |> ignore
            let lines =
                endpoints
                |> List.map PeerEndpoint.toString
                |> List.toArray
            File.WriteAllLines(p, lines))
