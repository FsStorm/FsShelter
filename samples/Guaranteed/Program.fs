﻿module Guaranteed.Program

open System.Threading
open FsShelter
open Guaranteed.Topology
open Common

let exePath = System.Reflection.Assembly.GetEntryAssembly().Location

// management CLI and task execution entry point
[<EntryPoint>]
let main argv = 
    let topology = 
        sampleTopology
        |> withConf [ TOPOLOGY_MULTILANG_SERIALIZER "com.FsStorm.protoshell.ProtoSerializer"
                      TOPOLOGY_ACKER_EXECUTORS 2
                      TOPOLOGY_MAX_SPOUT_PENDING 123
                      TOPOLOGY_DEBUG false]

    match argv |> List.ofArray with
    | "submit"::address::[port] -> 
        Startup.submit topology exePath (Startup.mkArgs []) address (int port)
    | ["submit-local"] -> 
        Startup.submit topology exePath (Startup.mkArgs []) "localhost" Nimbus.DefaultPort
    | "kill"::address::[port] ->
        Nimbus.withClient address (int port) (fun client -> Nimbus.kill client sampleTopology.Name) |> Task.wait
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
    | ["self-host"] ->
        let stop = 
            topology
            |> Hosting.run
        //    |> Hosting.runWith (sprintf "self-%d-%d" (System.Diagnostics.Process.GetCurrentProcess().Id) >> Logging.callbackLog)
        let log = Logging.asyncLog "hosting.log"
        printf "Running the topology, press ENTER to stop..."
        let sw = System.Diagnostics.Stopwatch.StartNew()
        System.Console.ReadLine() |> ignore
        stop()
        sw.Stop()
        printf "Stopped, getting counts...\n"
        let (count,_) = Topology.source.PostAndReply Get
        printf "Count: %s, %d/s\n" count (1000L*(int64 count)/sw.ElapsedMilliseconds)
    | _ -> 
        topology
        |> Task.ofTopology
        |> Task.run ProtoIO.start
        // |> Task.runWith (string >> Logging.callbackLog)  ProtoIO.start // start using a traffic logger 
    0

