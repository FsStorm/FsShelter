module FstSimpleTest
//1: submits the topology to storm
//2: runs the spout or the bolt depending on how called

let exePath = System.Reflection.Assembly.GetEntryAssembly().Location

[<EntryPoint>]
let main argv = 
    let logBase = if Storm.isMono() then "/Users/Fai/Logs/" else @"c:/ws/temp/fst/"
    Logging.log_path <- logBase + Logging.pid
    if Storm.isMono() then
        let exeName = System.IO.Path.GetFileName(exePath)
        StormProcessing.run "mono" [exeName] SimpleTestTopology.topology argv
    else
        StormProcessing.run exePath [] SimpleTestTopology.topology argv

