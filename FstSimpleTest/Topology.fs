module SimpleTestTopology
//define the storm topology

open StormDSL
open FsJson

//example of using FsStorm DSL for defining topologies
let topology =
    {
        TopologyName = "FstSimpleTest"

        Spouts =
            [
                {
                    Id          = "SimpleSpout"
                    Outputs     = [Default ["number"]]
                    Spout       = Local { Func = SimpleTestComponents.spout}
                    Config      = JsonNull
                    Parallelism = 1
                }
            ]
         
        Bolts = 
            [
                {

                    Id          = "SimpleBolt"
                    Outputs     = []
                    Inputs      = [DefaultStream "SimpleSpout", Shuffle]
                    Bolt        = Local { Func = SimpleTestComponents.bolt }
                    Config      = JsonNull
                    Parallelism = 2
                    
                }
            ]
       }