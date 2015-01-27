#load "../Refs/StormSubmit.fsx"

let binDir = "/Users/Fai/Projects/fwaris/FsStorm/FstSimpleTest/bin/Release"
//let binDir = @"C:\Users\Faisal\Downloads\FsStorm\FstSimpleTest\bin\Release"

//StormSubmit.makeJar binDir

StormSubmit.runTopology binDir "localhost" StormSubmit.default_nimbus_port
