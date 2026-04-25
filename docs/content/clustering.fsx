(**
Clustering
-------
FsShelter topologies can run under cluster supervision for automatic master/standby failover — no external coordinator (ZooKeeper, etcd, etc.) required. Add a few configuration options to your existing topology and swap `Hosting.run` for `Supervisor.runCluster`.

Only one peer in the cluster — the **master** — runs the topology at any time. Standby peers gossip among themselves and monitor liveness. If the master goes down, the remaining peers elect a new master and activate the topology there.

**Key properties:**

- **No external dependencies** — peers communicate directly over TCP; no coordinator service needed.
- **Deterministic election** — Rendezvous (HRW) hashing; every peer computes the same result independently.
- **Transparent fallthrough** — when `CLUSTER_SEEDS` is empty, `Supervisor.runCluster` delegates to `Hosting.runWith` for single-process parity. Same topology code, zero cluster overhead.
- **Same topology code** — sync, async, reliable, unreliable — all component types work under cluster supervision without changes.


Quick start
--------------------
Configure your topology with cluster options and use `Supervisor.runCluster` instead of `Hosting.run`:

    open FsShelter.Cluster

    let shutdown =
        myTopology
        |> withConf [ CLUSTER_LISTEN "0.0.0.0:6700"
                      CLUSTER_SEEDS "node1:6700,node2:6700,node3:6700"
                      CLUSTER_QUORUM 2 ]
        |> Supervisor.runCluster (fun _ _ -> ignore)

    match shutdown with
    | Ok stop -> stop()  // graceful shutdown
    | Error msg -> eprintfn "Cluster startup failed: %s" msg

`runCluster` returns `Result<unit -> unit, string>` — startup failures (bad config, transport bind, peer-id I/O) are returned as `Error` rather than thrown.


How it works
--------------------
The supervisor is a pure state machine (`Supervisor.step`) driven by external events. The impure orchestration (`runClusterWith`) wires together the TCP transport, periodic heartbeats, membership tracking, and election recompute.

### Supervisor states

```mermaid
stateDiagram-v2
    [*] --> Standby
    Standby --> Activating : ElectionUpdate(self=master)
    Activating --> Master : StabilizeElapsed (window passed)
    Activating --> Standby : ElectionUpdate(self≠master)
    Master --> Standby : ElectionUpdate(self≠master)
    Master --> Stopping : Shutdown
    Standby --> Stopping : Shutdown
    Activating --> Stopping : Shutdown
    Stopping --> [*]
```

| State | What's happening |
|-------|-----------------|
| **Standby** | Not the elected master. Only gossip and transport machinery are running. |
| **Activating** | Just won the election. Debouncing for `CLUSTER_STABILIZE_MS` before starting the topology — this prevents rapid start/stop if membership is still settling. |
| **Master** | Elected master and the topology is running. |
| **Stopping** | Shutdown was requested. Terminal state — no further transitions. |

The transition from **Activating → Standby** (lost mastership before stabilize window expires) is key: the topology was never started, so no `StopTopology` effect is emitted.


Failure detection (SWIM)
--------------------
Peers detect failures using a lightweight gossip protocol inspired by [SWIM](https://www.cs.cornell.edu/projects/Quicksilver/public_pdfs/SWIM.pdf):

1. Each peer sends **heartbeat pings** at `CLUSTER_HEARTBEAT_MS` intervals to all known peers, piggy-backing membership digests (peer state + incarnation numbers).
2. If a peer stops responding, it transitions from **Alive → Suspect** after `CLUSTER_SUSPECT_TIMEOUT_MS` with no heartbeat received.
3. A suspected peer can **refute** by responding with a newer incarnation — transitioning back to **Alive**.
4. If no refutation arrives, the peer transitions from **Suspect → Dead** and is evicted.

### Member states

```mermaid
stateDiagram-v2
    [*] --> Alive
    Alive --> Suspect : No heartbeat for SuspectTimeoutMs
    Suspect --> Alive : Newer incarnation received (refute)
    Suspect --> Dead : SuspectTimeoutMs elapsed without refutation
    Dead --> [*] : Evicted
```

**Quorum:** Only **Alive** and **Suspect** peers count toward quorum. The topology activates only when the cluster has at least `CLUSTER_QUORUM` live members AND the local peer is the elected master.

**Epochs:** Membership changes (joins, deaths) bump the cluster epoch. Only epoch changes trigger election recompute — suspect flaps do not.


Leader election (HRW)
--------------------
FsShelter uses **Rendezvous Hashing** (Highest Random Weight) for deterministic, decentralized leader election.

### How it works

Each peer independently computes a score for every live peer:

    score(peer) = SHA-256(topologyName | epoch_BE8 | peer_GUID) → first 8 bytes as uint64

The peer with the highest score wins. Ties are broken by `PeerId` ordinal comparison.

### Key properties

- **Deterministic** — all peers compute the same result independently; no voting or coordination messages needed.
- **Epoch stickiness** — folding the epoch into the hash input makes mastership stable: a higher-scored peer rejoining without a membership change cannot steal the role until the live set shifts (which bumps the epoch).
- **Minimal disruption** — when a peer joins or leaves, on average only 1/N of the election results change.

### Quorum requirement

The topology only activates when `MemberView.hasQuorum` is satisfied. A cluster of 3 peers with `CLUSTER_QUORUM 2` can tolerate 1 failure. A cluster of 5 with `CLUSTER_QUORUM 3` can tolerate 2.


Stabilization window
--------------------
After winning an election, the master waits `CLUSTER_STABILIZE_MS` (default: 2000ms) before activating the topology. This **debounce window** prevents rapid start/stop when membership is still settling (e.g., peers starting up in quick succession).

If mastership is lost during the stabilization window, the peer returns to Standby without ever starting the topology — avoiding unnecessary initialization and teardown.


Configuration reference
--------------------

| Option | Default | Effect |
|--------|---------|--------|
| `CLUSTER_SEEDS` | (none) | Comma-separated `host:port` list of initial peers. **Required for clustering.** Empty = single-process fallthrough. |
| `CLUSTER_LISTEN` | (none) | `host:port` endpoint to bind for peer communication. **Required for clustering.** |
| `CLUSTER_QUORUM` | (none) | Minimum live peers required before topology activation. Typically `⌊N/2⌋ + 1`. |
| `CLUSTER_HEARTBEAT_MS` | 1000 | Heartbeat ping interval in milliseconds. |
| `CLUSTER_SUSPECT_TIMEOUT_MS` | 5000 | Time without heartbeat before marking a peer as Suspect, then Dead. |
| `CLUSTER_STABILIZE_MS` | 2000 | Post-election debounce window in milliseconds. |
| `CLUSTER_STATE_DIR` | `"."` | Directory for persistent state (`peer-id.dat`, `peers.dat`). |
| `CLUSTER_SEND_QUEUE_BOUND` | 1024 | Per-peer outbound message queue limit. |
| `CLUSTER_MAX_FRAME_BYTES` | 262144 (256KB) | Maximum single frame size on the wire. |

Example with all options:

    myTopology
    |> withConf [ CLUSTER_LISTEN "0.0.0.0:6700"
                  CLUSTER_SEEDS "node1:6700,node2:6700,node3:6700"
                  CLUSTER_QUORUM 2
                  CLUSTER_HEARTBEAT_MS 1500
                  CLUSTER_SUSPECT_TIMEOUT_MS 7500
                  CLUSTER_STABILIZE_MS 3000
                  CLUSTER_STATE_DIR "/var/lib/myapp/cluster"
                  CLUSTER_SEND_QUEUE_BOUND 2048
                  CLUSTER_MAX_FRAME_BYTES (512 * 1024) ]


Graceful shutdown
--------------------
When the shutdown function is called:

1. A **Leaving** message is sent to all known peers — they can immediately mark this peer as Dead and re-elect without waiting for the suspect timeout.
2. The heartbeat timer is disposed.
3. A `Shutdown` input is posted to the supervisor mailbox.
4. If the peer is the current master, the topology is stopped (using the standard shutdown sequence: stop spouts → drain window → stop bolts → stop ackers).
5. The TCP transport is stopped.


Single-process fallthrough
--------------------
If `CLUSTER_SEEDS` is empty (and `CLUSTER_LISTEN` is not set), `Supervisor.runCluster` delegates to `Hosting.runWith` — your topology runs in single-process mode with zero cluster overhead. This means you can use `Supervisor.runCluster` as your only entry point and control the mode via configuration:

    // Development: no cluster config → single-process
    myTopology |> Supervisor.runCluster log

    // Production: add cluster config → supervised failover
    myTopology
    |> withConf [ CLUSTER_LISTEN "0.0.0.0:6700"
                  CLUSTER_SEEDS "node1:6700,node2:6700,node3:6700"
                  CLUSTER_QUORUM 2 ]
    |> Supervisor.runCluster log


Advanced: custom dependencies
--------------------
For testing or alternate hosting scenarios, `Supervisor.runClusterWith` accepts a `ClusterDeps<'t>` record that provides all external capabilities:

    type ClusterDeps<'t> =
        { NowMs: unit -> int64                                          // clock
          LoadOrCreatePeerId: string -> Result<PeerId, StoreError>      // persistent identity
          LoadPeersCache: string -> Result<PeerEndpoint list, StoreError>
          SavePeersCache: string -> PeerEndpoint list -> Result<unit, StoreError>
          CreateTransport: ... -> PeerTransport                         // TCP transport
          RunTopology: (int -> Log) -> Topology<'t> -> (unit -> unit) } // topology host

Production defaults are provided by `Supervisor.defaultDeps`, which uses the system clock, on-disk stores, real TCP transport, and `Hosting.runWith` for topology hosting. Override individual fields for integration testing or custom environments.


See also
--------------------

- [Running Topologies](self-hosting.html) — Entry points, configuration, diagnostics for single-process hosting
- [Architecture](architecture.html) — Internal runtime structure: tasks, executors, channels, lifecycle
- [Message Flow](message-flow.html) — End-to-end processing scenarios with sequence diagrams

*)
