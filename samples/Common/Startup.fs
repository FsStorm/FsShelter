namespace Common

module Task =
    let wait (t: System.Threading.Tasks.Task<_>) = t.Result

module Startup =
    open FsShelter
    open System.IO

    let submit (topology:Topology.Topology<'t>) (exePath: string) mkCmdArgs address port = 
        let includes = 
            [ Includes.buildOutput Includes.defaultExtensions
              Includes.jarContents "paket-files/run/github.com/protoshell-1.1.0-SNAPSHOT-jar-with-dependencies.jar" ]
            |> Includes.aggregate
        Nimbus.withClient address port
            (fun client ->
                task {
                    let! uploadedFile =
                        let binDir = Path.GetDirectoryName exePath
                        Package.makeJar includes binDir (Path.Combine(binDir, topology.Name) + ".jar")
                        |> Nimbus.uploadJar client
                    do! (mkCmdArgs exePath,topology)
                        ||> ThriftModel.ofTopology 
                        |> Nimbus.submit client topology.Conf uploadedFile
                })
        |> Task.wait

