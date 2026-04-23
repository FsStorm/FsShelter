module FsShelter.Cluster.TransportTests

open System
open System.Threading
open NUnit.Framework
open Swensen.Unquote
open FsShelter.Cluster


module Wire =

    let private roundTrip (env: WireEnvelope) =
        let bytes = Wire.encode env
        match Wire.decode bytes with
        | Ok decoded -> decoded =! env
        | Error e -> Assert.Fail (sprintf "decode failed: %A" e)

    let private samplePeer () = PeerId.create()

    let private sampleEndpoint () =
        { Host = "peer-" + Guid.NewGuid().ToString("N").Substring(0,6); Port = 7001 }

    let private sampleDigest () : MemberDigest =
        { Id = samplePeer()
          Endpoint = sampleEndpoint()
          Incarnation = Incarnation 42UL
          State = Alive }

    [<Test>]
    let ``Hello round-trips``() =
        roundTrip (Hello (samplePeer(), sampleEndpoint(), Epoch 7UL))

    [<Test>]
    let ``Welcome round-trips with empty digests``() =
        roundTrip (Welcome (samplePeer(), [], Epoch 3UL))

    [<Test>]
    let ``Ping round-trips with multiple digests``() =
        let digests = [ sampleDigest(); { sampleDigest() with State = Suspect }; { sampleDigest() with State = Dead } ]
        roundTrip (Ping (samplePeer(), Epoch 11UL, digests))

    [<Test>]
    let ``Ack round-trips``() =
        roundTrip (Ack (samplePeer(), Epoch 0UL, [ sampleDigest() ]))

    [<Test>]
    let ``PingReq round-trips``() =
        roundTrip (PingReq (samplePeer(), samplePeer(), 123u))

    [<Test>]
    let ``PingReqAck round-trips``() =
        roundTrip (PingReqAck (samplePeer(), samplePeer(), 456u))

    [<Test>]
    let ``Leaving round-trips``() =
        roundTrip (Leaving (samplePeer(), Epoch 99UL))

    [<Test>]
    let ``decode reports malformed on garbage``() =
        match Wire.decode [| 0xFFuy; 0x00uy; 0x00uy |] with
        | Error (MalformedFrame _) -> ()
        | other -> Assert.Fail (sprintf "expected MalformedFrame, got %A" other)


module Framing =

    [<Test>]
    let ``tryWriteFrameAsync and tryReadFrameAsync round-trip payload``() =
        task {
            use stream = new System.IO.MemoryStream()
            let payload = [| 0x01uy; 0x02uy; 0x03uy; 0x04uy |]

            match! Framing.tryWriteFrameAsync 1024 stream payload CancellationToken.None with
            | Error e -> Assert.Fail (sprintf "write failed: %A" e)
            | Ok () -> ()

            stream.Position <- 0L

            match! Framing.tryReadFrameAsync 1024 stream CancellationToken.None with
            | Ok (ValueSome bytes) -> bytes =! payload
            | Ok ValueNone -> Assert.Fail "expected payload, got EOF"
            | Error e -> Assert.Fail (sprintf "read failed: %A" e)
        }

    [<Test>]
    let ``tryReadFrameAsync reports oversized frame``() =
        task {
            let bytes = [| 0x00uy; 0x00uy; 0x04uy; 0x00uy |]
            use stream = new System.IO.MemoryStream(bytes)

            match! Framing.tryReadFrameAsync 16 stream CancellationToken.None with
            | Error (FrameTooLarge 1024) -> ()
            | other -> Assert.Fail (sprintf "expected FrameTooLarge 1024, got %A" other)
        }

    [<Test>]
    let ``tryReadFrameAsync lets cancellation escape``() =
        use stream = new System.IO.MemoryStream([| 0x00uy; 0x00uy; 0x00uy; 0x04uy |])
        use cts = new System.Threading.CancellationTokenSource()
        cts.Cancel()

        Assert.ThrowsAsync<System.OperationCanceledException>(fun () ->
            Framing.tryReadFrameAsync 16 stream cts.Token :> System.Threading.Tasks.Task)
        |> ignore


module PeerTransport =

    let private silentLog (_: FsShelter.LogLevel) (_: unit -> string) = ()

    let private freePort () =
        let l = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0)
        l.Start()
        let port = (l.LocalEndpoint :?> System.Net.IPEndPoint).Port
        l.Stop()
        port

    [<Test>]
    let ``enqueueBounded drops oldest when full``() =
        let buffer = System.Collections.Generic.Queue<byte[]>()

        PeerTransport.enqueueBounded 2 buffer [| 0x01uy |]
        PeerTransport.enqueueBounded 2 buffer [| 0x02uy |]
        PeerTransport.enqueueBounded 2 buffer [| 0x03uy |]

        let first = buffer.Dequeue()
        let second = buffer.Dequeue()
        first =! [| 0x02uy |]
        second =! [| 0x03uy |]

    [<Test>]
    let ``loopback delivers Ping``() =
        let portA = freePort()
        let portB = freePort()
        let epA = { Host = "127.0.0.1"; Port = portA }
        let epB = { Host = "127.0.0.1"; Port = portB }
        let a = PeerTransport.create silentLog (PeerId.create()) epA 64 (64 * 1024)
        let b = PeerTransport.create silentLog (PeerId.create()) epB 64 (64 * 1024)
        try
            match a.Start() with Ok () -> () | Error e -> Assert.Fail (sprintf "%A" e)
            match b.Start() with Ok () -> () | Error e -> Assert.Fail (sprintf "%A" e)

            let received = System.Collections.Concurrent.ConcurrentQueue<WireEnvelope>()
            let _ = b.Subscribe(fun ev ->
                match ev with
                | Received (_, env) -> received.Enqueue env
                | PeerDropped _ -> ())

            let senderId = PeerId.create()
            let ping = Ping (senderId, Epoch 1UL, [])
            a.Send epB ping

            let sw = System.Diagnostics.Stopwatch.StartNew()
            let mutable got = None
            while got.IsNone && sw.ElapsedMilliseconds < 2000L do
                let mutable e = Unchecked.defaultof<WireEnvelope>
                if received.TryDequeue(&e) then got <- Some e
                else System.Threading.Thread.Sleep 20

            match got with
            | Some (Ping (s, ep, [])) when s = senderId && ep = Epoch 1UL -> ()
            | other -> Assert.Fail (sprintf "expected Ping, got %A" other)
        finally
            a.Stop()
            b.Stop()

    [<Test>]
    let ``stop does not notify PeerDropped for local shutdown``() =
        let portA = freePort()
        let portB = freePort()
        let epA = { Host = "127.0.0.1"; Port = portA }
        let epB = { Host = "127.0.0.1"; Port = portB }
        let a = PeerTransport.create silentLog (PeerId.create()) epA 64 (64 * 1024)
        let b = PeerTransport.create silentLog (PeerId.create()) epB 64 (64 * 1024)
        try
            match a.Start() with Ok () -> () | Error e -> Assert.Fail (sprintf "%A" e)
            match b.Start() with Ok () -> () | Error e -> Assert.Fail (sprintf "%A" e)

            let events = System.Collections.Concurrent.ConcurrentQueue<InboundEvent>()
            let _subscription = b.Subscribe(fun ev -> events.Enqueue ev)

            a.Send epB (Ping (PeerId.create(), Epoch 1UL, []))

            let sw = System.Diagnostics.Stopwatch.StartNew()
            let mutable sawPing = false
            while not sawPing && sw.ElapsedMilliseconds < 2000L do
                let snapshot = events.ToArray()
                sawPing <-
                    snapshot
                    |> Array.exists (function | Received (_, Ping _) -> true | _ -> false)
                if not sawPing then System.Threading.Thread.Sleep 20

            if not sawPing then
                Assert.Fail "expected initial Ping before shutdown"

            while not events.IsEmpty do
                let mutable ignored = Unchecked.defaultof<InboundEvent>
                events.TryDequeue(&ignored) |> ignore

            b.Stop()
            System.Threading.Thread.Sleep 200

            let dropped =
                events.ToArray()
                |> Array.exists (function | PeerDropped _ -> true | _ -> false)

            if dropped then
                Assert.Fail (sprintf "expected local shutdown to be quiet, got %A" (events.ToArray()))
        finally
            a.Stop()
            b.Stop()
