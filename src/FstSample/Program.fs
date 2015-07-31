module FstSample
//1: submits the topology to storm
//2: runs the spout or the bolt depending on how called

let exePath = System.Reflection.Assembly.GetEntryAssembly().Location

[<EntryPoint>]
let main argv = 
    let logBase = if Storm.isMono() then "~/Logs/" else @"c:/temp/fst/"
    Logging.log_path <- logBase + Logging.pid
    if Storm.isMono() then
        let exeName = System.IO.Path.GetFileName(exePath)
        StormProcessing.run "mono" [exeName] SampleTopology.topology argv
    else
        StormProcessing.run exePath [] SampleTopology.topology argv

