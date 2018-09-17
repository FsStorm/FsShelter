(**
Self-hosting
-------
We call running a FsShelter topology entirely inside a .NET process "self-hosting".
The feature simulates Storm cluster and can be used for testing or production deployments where scale-out capability of Storm is not required.
The 2.0 release of FsShelter reimplements this feature using the same technology Storm does - Disruptor queues, greatly improving the performance and further reducing overhead.

Limitations:

- only FsShelter components can be self-hosted, no shell or java;
- tuple timeouts are not enforced;
- only does in-proc message passing - can't scale out.

Benefits:

- Self-hosted F# components perform on par with native JVM components under Storm, while taking as little as 30MB and ~20 threads.
- no Storm, zookeeper or even JRE are required, while delivering the same processing guarantees.

Start small then scale out to run on a cluster, with no changes to your code! Use the same building blocks that run in the cloud... anywhere! 

For details refer to the Guaranteed sample, which demonstrates how to run the topology either way.


Diagnostics
--------
This release greately improves diagnostics when self-hosting a topology:

- `TOPOLOGY_DEBUG` flag can be set on individual components to selectively analyse the traffic.
- settting the debug flag globally will also trace the system and acker components.
- setting the flag will also trace the time spend in the body of your function.


Relevant Configuration options
--------

* `withParallelism` has the same effect as in Storm.
* `TOPOLOGY_MULTILANG_SERIALIZER` has no effect, as self-hosting does not serialize.
* `TOPOLOGY_MAX_SPOUT_PENDING` has the same effect as in Storm.
* `TOPOLOGY_MESSAGE_TIMEOUT_SECS` determines how long the runtime will wait before starting the flow of tuples and after shutdown command.
* `TOPOLOGY_EXECUTOR_RECEIVE_BUFFER_SIZE` determins the size of the ring-buffers for your components. Higher backpressure water-mark, may need higher ring-buffer size to avoid deadlock.
* `TOPOLOGY_ACKER_EXECUTORS` number has the same effect as in Storm - increasing this number may improve guaranteed throughput at the cost of increased resource consumption.

*)
