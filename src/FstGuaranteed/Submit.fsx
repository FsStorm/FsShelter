#I "../../Refs"
#load "StormSubmit.fsx"

let binDir = "src/FstGuaranteed/bin/Debug"

StormSubmit.runTopology binDir "localhost" StormSubmit.default_nimbus_port
