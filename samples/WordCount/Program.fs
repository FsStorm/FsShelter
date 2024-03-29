﻿module Sample.Program

open FsShelter
open Sample.Topology
open Common

let exePath = System.Reflection.Assembly.GetEntryAssembly().Location

// management CLI and task execution entry point
[<EntryPoint>]
let main argv = 
    let topology = 
        reliableTopology // | unreliableTopology
        |> withConf [ TOPOLOGY_MULTILANG_SERIALIZER "com.FsStorm.protoshell.ProtoSerializer" // custom Multilang serializer (`Startup.submit` will try to include it for deployment)
                      TOPOLOGY_MAX_SPOUT_PENDING 123
                      TOPOLOGY_DEBUG false] // setting topology.debug true tells FsShelter and Storm to log messages to and from this component in respective logs

    match argv |> List.ofArray with
    | "submit"::address::[port] -> 
        Startup.submit topology exePath (Startup.mkArgs []) address (int port) 
    | ["submit-local"] -> 
        Startup.submit topology exePath (Startup.mkArgs []) "localhost" Nimbus.DefaultPort
    | "kill"::address::[port] ->
        Nimbus.withClient address (int port) (fun client -> Nimbus.kill client topology.Name) |> Task.wait
    | ["graph"] ->
        topology
        |> DotGraph.writeToConsole
    | ["colour-graph"] ->
        topology
        |> DotGraph.writeColourizedToConsole
    | ["custom-colour-graph"] ->
        let customColours = [| "purple"; "dodgerblue"; "springgreen"; "olivedrab"; "orange"; "orangered"; "maroon"; "black" |]
        topology
        |> DotGraph.exportToDot (DotGraph.writeHeader, DotGraph.writeFooter, DotGraph.writeSpout, DotGraph.writeBolt, DotGraph.writeColourfulStream <| DotGraph.getColour customColours) System.Console.Out
    | _ -> 
        topology
        |> Task.ofTopology
        |> Task.run ProtoIO.start // JsonIO.start | ProtoIO.start
        //|> Task.runWith (string >> Logging.callbackLog)  ProtoIO.start // log the traffic on this side of IPC
    0

