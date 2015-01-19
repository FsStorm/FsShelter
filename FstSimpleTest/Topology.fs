module SimpleTestTopology
//define the storm topology

open StormDSL
open FsJson

//let baseLogDir = "/Users/Fai/Logs/"
let baseLogDir = @"c:/ws/temp/fst/"

//example of using FsStorm DSL for defining topologies
let topology =
    {
        TopologyName = "FstSimpleTest"

        Spouts =
            [
                {
                    Id          = "SimpleSpout"
                    Outputs     = [Default ["number"]]
                    Spout       = Local 
                                    {
                                        Name = "SimpleSpout"
                                        Func = SimpleTestComponents.spout
                                     }
                    Config      = jval [Storm.FSLOGDIR, baseLogDir] //directory for the logs of this component
                    Parallelism = 1
                }
            ]
         
        Bolts = 
            [
                {

                    Id          = "SimpleBolt"
                    Outputs     = []
                    Inputs      = [DefaultStream "SimpleSpout", Shuffle]
                    Bolt        = Local 
                                    {
                                        Name = "SimpleBolt"
                                        Func = SimpleTestComponents.spout
                                     }
                    Config      = jval [Storm.FSLOGDIR, baseLogDir] //directory for the logs of this component
                    Parallelism = 2
                    
                }
            ]
       }