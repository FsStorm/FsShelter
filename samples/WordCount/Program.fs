module Sample.Program

open FsShelter
open Sample.Topology
open Common

let exePath = System.Reflection.Assembly.GetEntryAssembly().Location

// management CLI and task execution entry point
[<EntryPoint>]
let main argv = 
    let cfg = ["topology.multilang.serializer", box "com.prolucid.protoshell.ProtoSerializer" // custom Multilang serializer (has to be in Storm's classpath)
               "topology.debug", box false] |> dict // setting topology.debug true tells Storm to log messages to and from this component in its worker logs

    match argv |> List.ofArray with
    | "submit"::address::[port] -> 
        Startup.submit sampleTopology exePath (Startup.mkWindowsArgs []) cfg address (int port) 
    | "submit-mono"::address::[port] -> 
        Startup.submit sampleTopology exePath (Startup.mkMonoArgs []) cfg address (int port)
    | ["submit-local"] -> 
        let mkArgs = if isNull (System.Type.GetType "Mono.Runtime") then Startup.mkWindowsArgs
                     else Startup.mkMonoArgs
        Startup.submit sampleTopology exePath (mkArgs []) cfg "localhost" Nimbus.DefaultPort
    | "kill"::address::[port] ->
        Nimbus.withClient address (int port) 
            (fun client -> Nimbus.kill client sampleTopology.Name)
    | ["graph"] ->
        sampleTopology
        |> DotGraph.writeToConsole
    | _ -> 
        sampleTopology
        |> Task.ofTopology
        |> Task.run ProtoIO.start // JsonIO.start | ProtoIO.start | ThriftIO.start
//        |> Task.runWith (string >> Logging.callbackLog)  ProtoIO.start // log the traffic on this side of IPC
    0

