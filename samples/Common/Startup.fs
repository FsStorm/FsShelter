module Common.Startup

open FsShelter
open System.IO

let submit (topology:Topology.Topology<'t>) exePath mkCmdArgs conf address port = 
    Nimbus.withClient address port
        (fun client ->
            let uploadedFile =
                let binDir = Path.GetDirectoryName exePath
                Package.makeJar Package.defaultExtensions binDir (Path.Combine(binDir, topology.Name) + ".jar")
                |> Nimbus.uploadJar client
            (mkCmdArgs exePath,topology)
            ||> ThriftModel.ofTopology 
            |> Nimbus.submit client (Some conf) uploadedFile)

