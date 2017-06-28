module Sample.Program

open FsShelter
open Sample.Topology
open Common

let exePath = System.Reflection.Assembly.GetEntryAssembly().Location

// management CLI and task execution entry point
[<EntryPoint>]
let main argv = 
    let topology = 
        sampleTopology
        |> withConf [ Conf.TOPOLOGY_MULTILANG_SERIALIZER, box "com.prolucid.protoshell.ProtoSerializer" // custom Multilang serializer (has to be in Storm's classpath)
                      Conf.TOPOLOGY_DEBUG, box false] // setting topology.debug true tells Storm to log messages to and from this component in its worker logs

    match argv |> List.ofArray with
    | "submit"::address::[port] -> 
        Startup.submit topology exePath (Startup.mkWindowsArgs []) address (int port) 
    | "submit-mono"::address::[port] -> 
        Startup.submit topology exePath (Startup.mkMonoArgs []) address (int port)
    | ["submit-local"] -> 
        let mkArgs = if isNull (System.Type.GetType "Mono.Runtime") then Startup.mkWindowsArgs
                     else Startup.mkMonoArgs
        Startup.submit topology exePath (mkArgs []) "localhost" Nimbus.DefaultPort
    | "kill"::address::[port] ->
        Nimbus.withClient address (int port) 
            (fun client -> Nimbus.kill client topology.Name)
    | ["graph"] ->
        topology
        |> DotGraph.writeToConsole
    | _ -> 
        topology
        |> Task.ofTopology
        |> Task.run ProtoIO.start // JsonIO.start | ProtoIO.start
//        |> Task.runWith (string >> Logging.callbackLog)  ProtoIO.start // log the traffic on this side of IPC
    0

