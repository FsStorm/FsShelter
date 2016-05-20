/// Task execution
module FsShelter.Task

open System.IO
open FsShelter.Multilang
open FsShelter.Topology
open System

/// Logger signature    
type Log = (unit -> string) -> unit
/// Task signature
type Task<'t> = ComponentId -> Runnable<'t>

// diagnostics pid shortcut
let private pid() = System.Diagnostics.Process.GetCurrentProcess().Id

// produce the nested exception's stack trace 
let internal traceException (ex:Exception) =
    let sb = new System.Text.StringBuilder()
    let rec loop (ex:Exception) =
        sb.AppendLine(ex.Message).AppendLine(ex.StackTrace) |> ignore
        if isNull ex.InnerException then
            sb.ToString()
        else
            sb.AppendLine("========") |> ignore
            loop ex.InnerException
    loop ex

// creates an empty file with current pid as the file name
let private createPid pidDir pid = 
    let path = Path.Combine(pidDir, pid.ToString())
    use fs = File.CreateText(path)
    fs.Close()

/// converts topology to a runnable task
let ofTopology (t : Topology<'t>) compId = 
    let anchor = t.Anchors.TryFind
                 >> function
                    | Some toAnchor -> toAnchor 
                    | None -> fun _ -> []
    seq { 
        yield t.Spouts
              |> Map.tryFind compId
              |> Option.map (fun s -> s.MkComp())
        yield t.Bolts
              |> Map.tryFind compId
              |> Option.map (fun s -> s.MkComp anchor)
    }
    |> Seq.choose id
    |> Seq.head
    |> function 
    | FuncRef r -> r
    | _ -> failwithf "Not a runnable component: %s" compId

/// Reads the handshake and runs the specified task with a logger
let runWith (startLog:int->Log) (io : Log -> IO<'t>) (task : Task<'t>) = 
    async { 
        let pid = pid()
        let log = startLog pid
        let (in', out') = io log
        try 
            log(fun _ -> sprintf "started in %s, waiting for handshake..." Environment.CurrentDirectory)
            let! msg = in'()
            let (cfg, compId) = 
                match msg with
                | Handshake(cfg, pidDir, context) -> 
                    pid |> createPid pidDir
                    Pid pid |> out'
                    (cfg, context.ComponentId)
                | _ -> failwithf "Expected handshake, got: %A" msg
            log(fun _ -> sprintf "running %s..." compId)
            return! task compId (in', out') cfg
        with ex -> 
            let msg = traceException ex
            log (fun _ -> msg)
            Log(msg, LogLevel.Error) |> out'
            Threading.Thread.Sleep 1000
            Environment.Exit 1
    }
    |> Async.RunSynchronously

/// Reads the handshake and runs the specified task
let run (io : Log -> IO<'t>) (task : Task<'t>) = runWith (fun _ -> ignore) io task

