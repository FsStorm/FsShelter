module Storm.Task

open System.IO
open Storm.Multilang
open Storm.Topology

/// diagnostics pid shortcut
let internal pid() = System.Diagnostics.Process.GetCurrentProcess().Id

///creates an empty file with current pid as the file name
let internal createPid pidDir pid = 
    let path = Path.Combine(pidDir, pid.ToString())
    use fs = File.CreateText(path)
    fs.Close()

let ofTopology (t : Topology<'t>) compId = 
    let anchor = fun sid -> t.Anchors.[sid]
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

let run (io : string -> IO<'t>) (task : ComponentId -> Runnable<'t>) = 
    async { 
        let pid = pid()
        let (in', out') = io (string pid)
        try 
            let! msg = in'()
            let (cfg, compId) = 
                match msg with
                | Handshake(cfg, pidDir, context) -> 
                    pid |> createPid pidDir
                    Pid pid |> out'
                    (cfg, context.ComponentId)
                | _ -> failwithf "Expected handshake, got: %A" msg
            Log((sprintf "running %s..." compId), LogLevel.Info) |> out'
            return! task compId (io compId) cfg
        with ex -> 
            //better to exit process if something goes wrong 
            //at this point
            logOfException ex |> out'
            System.Environment.Exit(1)
    }
    |> Async.RunSynchronously
