/// Task execution
module FsShelter.Task

open System.IO
open FsShelter.Multilang
open FsShelter.Topology
open System
open System.Diagnostics

/// Logger signature    
type Log = (unit -> string) -> unit
/// Task signature
type Task<'t> = ComponentId -> Runnable<'t>

// diagnostics pid shortcut
let private pid() = System.Diagnostics.Process.GetCurrentProcess().Id


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
              |> Option.map (fun b -> b.MkComp (anchor,b.Activate,b.Deactivate))
    }
    |> Seq.choose id
    |> Seq.head
    |> function 
    | FuncRef r -> r
    | _ -> failwithf "Not a runnable component: %s" compId

/// Reads the handshake and runs the specified task with a logger
let runWith (startLog : int->Log) (mkIO : Log -> IO<'t>) (task : Task<'t>) = 
    let pid = pid()
    let log = startLog pid
    let (in', out) = mkIO log
    try 
        log(fun _ -> sprintf "started in %s, waiting for handshake..." Environment.CurrentDirectory)
        let msg = in'()
        let (cfg, compId) = 
            match msg with
            | Handshake(cfg, pidDir, context) -> 
                pid |> createPid pidDir
                Pid pid |> out
                (cfg, context.ComponentId)
            | _ -> failwithf "Expected handshake, got: %A" msg
        log(fun _ -> sprintf "running %s..." compId)
        let dispatch = task compId cfg out
        let timedDispatch = 
            if cfg |> Conf.optionOrDefault TOPOLOGY_DEBUG then
                fun msg ->
                    let sw = Stopwatch.StartNew()
                    dispatch msg
                    sw.Stop()
                    log(fun _ -> sprintf "processed in: %4.2fms" sw.Elapsed.TotalMilliseconds)
            else dispatch                        

        while true do
            let msg = in'()
            timedDispatch msg
    with ex -> 
        let msg = Exception.toString ex
        log (fun _ -> msg)
        Log(msg, LogLevel.Error) |> out
        Threading.Thread.Sleep 1000
        Environment.Exit 1

/// Reads the handshake and runs the specified task
let run (mkIO : Log -> IO<'t>) (task : Task<'t>) = runWith (fun _ -> ignore) mkIO task

