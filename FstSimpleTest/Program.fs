module FstSimpleTest
//1: submits the topology to storm
//2: runs the spout or the bolt depending on how called

let exePath = System.Reflection.Assembly.GetEntryAssembly().Location

let isMono() = System.Type.GetType("Mono.Runtime") <> null

[<EntryPoint>]
let main argv = 
    if isMono() then
        let exeName = System.IO.Path.GetFileName(exePath)
        StormProcessing.run "mono" [exeName] SimpleTestTopology.topology argv
    else
        StormProcessing.run exePath [] SimpleTestTopology.topology argv

