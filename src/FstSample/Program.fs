module FstSample.Program

open Storm
open FstSample.Topology
open System.IO
open System

let exePath = System.Reflection.Assembly.GetEntryAssembly().Location

// management CLI and task execution entry point
[<EntryPoint>]
let main argv = 
    match argv |> List.ofArray with
    | "submit"::address::port::args ->
        let cfg = ["topology.multilang.serializer",box "com.prolucid.protoshell.ProtoSerializer"
                   "topology.debug",box false] |> dict
        Nimbus.withClient address (int port) 
            (fun client ->
                let uploadedFile =
                    Path.GetDirectoryName exePath
                    |> Package.makeJar Package.defaultExtensions
                    |> Nimbus.uploadJar client
                sampleTopology
                |> ThriftModel.ofTopology args (Path.GetFileName exePath)
                |> Nimbus.submit client (Some cfg) uploadedFile) //(Some cfg)
    | "kill"::address::[port] ->
        Nimbus.withClient address (int port) 
            (fun client -> Nimbus.kill client sampleTopology.Name)
    | ["graph"] ->
        sampleTopology
        |> DotGraph.writeToConsole
    | _ -> 
        sampleTopology
        |> Task.ofTopology
        |> Task.run ProtoIO.start
    0