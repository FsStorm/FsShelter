#load "../Refs/StormSubmit.fsx"

let binDir = "/Users/Fai/Projects/fwaris/FsStorm/FstSample/bin/Release"
//let binDir = @"C:\Users\Faisal\Downloads\FsStorm\FstSample\bin\Release"

//StormSubmit.makeJar binDir

StormSubmit.runTopology binDir "localhost" StormSubmit.default_nimbus_port
