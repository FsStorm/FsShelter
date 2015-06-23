module SampleTopology

//define the storm topology
open StormDSL
open FsJson

//example of using FsStorm DSL for defining topologies
let topology = 
    { TopologyName = "FstSample"
      Spouts = 
          [ { Id = "SimpleSpout"
              Outputs = [ Default [ "number" ] ]
              Spout = Local { Func = SampleComponents.spout Storm.simpleSpoutRunner }
              Config = JsonNull
              Parallelism = 1 } ]
      Bolts = 
          [ { Id = "AddOneBolt"
              Outputs = [ Default [ "number" ] ]
              Inputs = [ DefaultStream "SimpleSpout", Shuffle ]
              Bolt = Local { Func = SampleComponents.addOneBolt Storm.autoAckBoltRunner Logging.log Storm.emit}
              Config = JsonNull
              Parallelism = 2 }
            { Id = "ResultBolt"
              Outputs = []
              Inputs = [ DefaultStream "AddOneBolt", Shuffle ]
              Bolt = Local { Func = SampleComponents.resultBolt Storm.autoAckBoltRunner Logging.log }
              Config = JsonNull
              Parallelism = 2 } ] }
