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
        |> withConf [ ConfOption.TOPOLOGY_MULTILANG_SERIALIZER "com.prolucid.protoshell.ProtoSerializer" // custom Multilang serializer (has to be in Storm's classpath)
                      ConfOption.TOPOLOGY_DEBUG false] // setting topology.debug true tells Storm to log messages to and from this component in its worker logs

    match argv |> List.ofArray with
    | "submit"::address::[port] -> 
        Startup.submit topology exePath (Startup.mkArgs []) address (int port) 
    | ["submit-local"] -> 
        Startup.submit topology exePath (Startup.mkArgs []) "localhost" Nimbus.DefaultPort
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
        //|> Task.runWith (string >> Logging.callbackLog)  ProtoIO.start // log the traffic on this side of IPC
    0

