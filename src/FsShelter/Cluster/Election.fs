namespace FsShelter.Cluster

// HRW (Highest Random Weight, a.k.a. Rendezvous Hashing) leader election.
//
// Background: Thaler & Ravishankar, "A Name-Based Mapping Scheme for
// Rendezvous" (1996, Univ. of Michigan TR CSE-TR-316-96).
//   https://www.eecs.umich.edu/techreports/cse/96/CSE-TR-316-96.pdf
//
// For each topology name we score every live peer with
// `H(topologyName, epoch, peerId)` and pick the argmax. This is
// deterministic across peers (no votes, no coordinator) and minimally
// disruptive: only the master changes when membership changes.
//
// Epoch is folded into the hash so mastership is *sticky* — a higher-ranked
// peer rejoining without a membership change cannot steal the role until
// the live set actually shifts (which is the only thing that bumps Epoch).

open System
open System.Security.Cryptography
open System.Text

/// Result of an election round. `None` master means insufficient information
/// (empty live set); the Supervisor stays in Standby.
type ElectionResult =
    { Master: PeerId voption
      Epoch: Epoch
      LiveCount: int }

[<RequireQualifiedAccess>]
module Election =

    /// HRW score for a single peer. SHA-256 of (topologyName | epoch_be8 | peer_guid)
    /// truncated to its first 8 bytes interpreted big-endian as a uint64.
    /// Tie-breaks (astronomically unlikely with SHA-256) fall back to PeerId
    /// ordinal so the result remains total.
    let private score (topologyName: string) (Epoch epoch) (PeerId peer) : uint64 =
        let nameBytes = Encoding.UTF8.GetBytes topologyName
        let epochBytes = Array.zeroCreate<byte> 8
        epochBytes.[0] <- byte (epoch >>> 56)
        epochBytes.[1] <- byte (epoch >>> 48)
        epochBytes.[2] <- byte (epoch >>> 40)
        epochBytes.[3] <- byte (epoch >>> 32)
        epochBytes.[4] <- byte (epoch >>> 24)
        epochBytes.[5] <- byte (epoch >>> 16)
        epochBytes.[6] <- byte (epoch >>> 8)
        epochBytes.[7] <- byte epoch
        let peerBytes = peer.ToByteArray()
        let buf = Array.zeroCreate<byte> (nameBytes.Length + epochBytes.Length + peerBytes.Length)
        Buffer.BlockCopy(nameBytes, 0, buf, 0, nameBytes.Length)
        Buffer.BlockCopy(epochBytes, 0, buf, nameBytes.Length, epochBytes.Length)
        Buffer.BlockCopy(peerBytes, 0, buf, nameBytes.Length + epochBytes.Length, peerBytes.Length)
        let hash = SHA256.HashData buf
        ((uint64 hash.[0]) <<< 56)
        ||| ((uint64 hash.[1]) <<< 48)
        ||| ((uint64 hash.[2]) <<< 40)
        ||| ((uint64 hash.[3]) <<< 32)
        ||| ((uint64 hash.[4]) <<< 24)
        ||| ((uint64 hash.[5]) <<< 16)
        ||| ((uint64 hash.[6]) <<< 8)
        ||| (uint64 hash.[7])

    /// Compute the master peer for `topologyName` over the live set in `view`,
    /// using `view.Epoch` to make selection epoch-sticky.
    let elect (topologyName: string) (view: MemberView) : ElectionResult =
        let liveMembers = MemberView.live view
        let master =
            liveMembers
            |> List.fold
                (fun acc m ->
                    let s = score topologyName view.Epoch m.Id
                    match acc with
                    | ValueNone -> ValueSome (m.Id, s)
                    | ValueSome (_, sBest) when s > sBest -> ValueSome (m.Id, s)
                    | ValueSome (idBest, sBest) when s = sBest && m.Id < idBest ->
                        // Deterministic tie-break by PeerId ordinal.
                        ValueSome (m.Id, s)
                    | _ -> acc)
                ValueNone
            |> ValueOption.map fst
        { Master = master
          Epoch = view.Epoch
          LiveCount = List.length liveMembers }

    /// True when the local peer is the elected master for `topologyName`.
    let selfIsMaster (topologyName: string) (view: MemberView) : bool =
        match (elect topologyName view).Master with
        | ValueSome m -> m = view.Self
        | ValueNone -> false
