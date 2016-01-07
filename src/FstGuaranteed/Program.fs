module FstGuaranteed.Program

open Storm
open FstGuaranteed.Topology
open System.IO

let exePath = System.Reflection.Assembly.GetEntryAssembly().Location

// management CLI and task execution entry point
[<EntryPoint>]
let main argv = 
    match argv |> List.ofArray with
    | "submit"::address::port::args ->
        Nimbus.withClient address (int port) 
            (fun client ->
                let uploadedFile =
                    Path.GetDirectoryName exePath
                    |> Package.makeJar Package.defaultExtensions
                    |> Nimbus.uploadJar client
                sampleTopology
                |> ThriftModel.ofTopology args (Path.GetFileName exePath)
                |> Nimbus.submit client (Some (dict ["topology.debug",true])) uploadedFile)
    | "kill"::address::[port] ->
        Nimbus.withClient address (int port) 
            (fun client -> Nimbus.kill client sampleTopology.Name)
    | ["graph"] ->
        sampleTopology
        |> DotGraph.writeToConsole
    | _ -> 
        sampleTopology
        |> Task.ofTopology
        |> Task.run JsonIO.start
    0

