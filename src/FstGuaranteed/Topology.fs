module GuaranteedSampleTopology

//define the storm topology
open StormDSL
open FsJson

//example of using FsStorm DSL for defining topologies
let topology = 
    { TopologyName = "FstGuaranteed"
      Spouts = 
          [ { Id = "ReliableSpout"
              Outputs = [ Default [ "number" ] ]
              Spout = Local { Func = SampleComponents.spout (Storm.reliableSpoutRunner Storm.defaultHousekeeper) Storm.stormLog } // logs to Storm log
              Config = jval [ "topology.max.spout.pending", jval 123 ]
              Parallelism = 1 } ]
      Bolts = 
          [ { Id = "AddOneBolt"
              Outputs = [ Named("even", [ "number" ])
                          Named("odd", [ "number" ]) ]
              Inputs = [ DefaultStream "ReliableSpout", Shuffle ]
              Bolt = Local { Func = SampleComponents.addOneBolt Storm.autoAckBoltRunner Storm.stormLog Storm.emit } // logs to Storm log
              Config = JsonNull
              Parallelism = 2 }
            { Id = "EvenResultBolt"
              Outputs = []
              Inputs = [ Stream("AddOneBolt","even"), Shuffle ]
              Bolt = Local { Func = SampleComponents.resultBolt Storm.autoAckBoltRunner Logging.log } // logs to custom (FsLogging) pid-based log file
              Config = jval ["desc", "even"] // pass custom config property to the component
              Parallelism = 1 }
            { Id = "OddResultBolt"
              Outputs = []
              Inputs = [ Stream("AddOneBolt","odd"), Shuffle ]
              Bolt = Local { Func = SampleComponents.resultBolt Storm.autoAckBoltRunner Logging.log } // logs to custom (FsLogging) pid-based log file
              Config = jval ["desc", "odd"] // pass custom config property to the component
              Parallelism = 1 } ] }
