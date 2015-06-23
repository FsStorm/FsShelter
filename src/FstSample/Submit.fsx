#I "../../Refs"
#load "StormSubmit.fsx"

let binDir = "build"

StormSubmit.runTopology binDir "localhost" StormSubmit.default_nimbus_port
