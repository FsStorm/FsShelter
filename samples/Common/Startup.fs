module Common.Startup

open FsShelter
open System.IO

let submit (topology:Topology.Topology<'t>) exePath mkCmdArgs conf address port = 
    let includes = [ Includes.buildOutput Includes.defaultExtensions
                     Includes.jarContents "paket-files/run/github.com/protoshell-0.0.1-SNAPSHOT-jar-with-dependencies.jar" 
//                     Includes.jarContents "paket-files/run/github.com/thriftshell-0.0.1-SNAPSHOT-jar-with-dependencies.jar" // TODO: uncomment if ThriftShell serializer is used
                   ] |> Includes.aggregate
    Nimbus.withClient address port
        (fun client ->
            let uploadedFile =
                let binDir = Path.GetDirectoryName exePath
                Package.makeJar includes binDir (Path.Combine(binDir, topology.Name) + ".jar")
                |> Nimbus.uploadJar client
            (mkCmdArgs exePath,topology)
            ||> ThriftModel.ofTopology 
            |> Nimbus.submit client (Some conf) uploadedFile)

