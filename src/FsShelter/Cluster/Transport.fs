namespace FsShelter.Cluster

open System
open System.IO
open System.Net
open System.Net.Sockets
open System.Text
open System.Threading
open System.Threading.Tasks
open System.Collections.Concurrent
open System.Collections.Generic

type LogLevel = FsShelter.LogLevel


/// Wire-level control-plane envelope. Closed DU, custom binary codec.
type WireEnvelope =
    | Hello of sender: PeerId * endpoint: PeerEndpoint * epoch: Epoch
    | Welcome of receiver: PeerId * digests: MemberDigest list * epoch: Epoch
    | Ping of sender: PeerId * epoch: Epoch * digests: MemberDigest list
    | Ack of sender: PeerId * epoch: Epoch * digests: MemberDigest list
    | PingReq of sender: PeerId * target: PeerId * nonce: uint32
    | PingReqAck of sender: PeerId * target: PeerId * nonce: uint32
    | Leaving of sender: PeerId * epoch: Epoch


type TransportError =
    | BindFailed of endpoint: PeerEndpoint * exn: exn
    | ConnectFailed of endpoint: PeerEndpoint * exn: exn
    | FrameTooLarge of length: int
    | MalformedFrame of reason: string
    | Disconnected of endpoint: PeerEndpoint


type InboundEvent =
    | Received of from: PeerEndpoint * envelope: WireEnvelope
    | PeerDropped of endpoint: PeerEndpoint * error: TransportError


/// Codec — length-prefixed u32 big-endian framing around a custom binary body.
[<RequireQualifiedAccess>]
module internal Wire =

    // Tag bytes for the top-level envelope.
    [<Literal>]
    let private TagHello = 1uy
    [<Literal>]
    let private TagWelcome = 2uy
    [<Literal>]
    let private TagPing = 3uy
    [<Literal>]
    let private TagAck = 4uy
    [<Literal>]
    let private TagPingReq = 5uy
    [<Literal>]
    let private TagPingReqAck = 6uy
    [<Literal>]
    let private TagLeaving = 7uy

    [<Literal>]
    let private StAlive = 0uy
    [<Literal>]
    let private StSuspect = 1uy
    [<Literal>]
    let private StDead = 2uy

    let private writeU16 (w: BinaryWriter) (v: uint16) =
        w.Write (byte (v >>> 8))
        w.Write (byte v)

    let private writeU32 (w: BinaryWriter) (v: uint32) =
        w.Write (byte (v >>> 24))
        w.Write (byte (v >>> 16))
        w.Write (byte (v >>> 8))
        w.Write (byte v)

    let private writeU64 (w: BinaryWriter) (v: uint64) =
        w.Write (byte (v >>> 56))
        w.Write (byte (v >>> 48))
        w.Write (byte (v >>> 40))
        w.Write (byte (v >>> 32))
        w.Write (byte (v >>> 24))
        w.Write (byte (v >>> 16))
        w.Write (byte (v >>> 8))
        w.Write (byte v)

    let private readU16 (r: BinaryReader) =
        let b0 = uint16 (r.ReadByte())
        let b1 = uint16 (r.ReadByte())
        (b0 <<< 8) ||| b1

    let private readU32 (r: BinaryReader) =
        let b0 = uint32 (r.ReadByte())
        let b1 = uint32 (r.ReadByte())
        let b2 = uint32 (r.ReadByte())
        let b3 = uint32 (r.ReadByte())
        (b0 <<< 24) ||| (b1 <<< 16) ||| (b2 <<< 8) ||| b3

    let private readU64 (r: BinaryReader) =
        let b0 = uint64 (r.ReadByte())
        let b1 = uint64 (r.ReadByte())
        let b2 = uint64 (r.ReadByte())
        let b3 = uint64 (r.ReadByte())
        let b4 = uint64 (r.ReadByte())
        let b5 = uint64 (r.ReadByte())
        let b6 = uint64 (r.ReadByte())
        let b7 = uint64 (r.ReadByte())
        (b0 <<< 56) ||| (b1 <<< 48) ||| (b2 <<< 40) ||| (b3 <<< 32)
        ||| (b4 <<< 24) ||| (b5 <<< 16) ||| (b6 <<< 8) ||| b7

    let private writePeerId (w: BinaryWriter) (PeerId g) =
        w.Write (g.ToByteArray())

    let private readPeerId (r: BinaryReader) =
        PeerId (Guid (r.ReadBytes 16))

    let private writeString (w: BinaryWriter) (s: string) =
        let bytes = Encoding.UTF8.GetBytes s
        if bytes.Length > int UInt16.MaxValue then
            invalidArg "s" (sprintf "string too long for wire: %d bytes" bytes.Length)
        writeU16 w (uint16 bytes.Length)
        w.Write bytes

    let private readString (r: BinaryReader) =
        let n = int (readU16 r)
        Encoding.UTF8.GetString (r.ReadBytes n)

    let private writeEndpoint (w: BinaryWriter) (ep: PeerEndpoint) =
        writeString w ep.Host
        w.Write (int32 ep.Port)

    let private readEndpoint (r: BinaryReader) : PeerEndpoint =
        let host = readString r
        let port = r.ReadInt32()
        { Host = host; Port = port }

    let private writeState (w: BinaryWriter) (s: MemberState) =
        match s with
        | Alive -> w.Write StAlive
        | Suspect -> w.Write StSuspect
        | Dead -> w.Write StDead

    let private readState (r: BinaryReader) : MemberState =
        match r.ReadByte() with
        | b when b = StAlive -> Alive
        | b when b = StSuspect -> Suspect
        | b when b = StDead -> Dead
        | b -> raise (InvalidDataException (sprintf "unknown MemberState tag: %d" (int b)))

    let private writeDigest (w: BinaryWriter) (d: MemberDigest) =
        writePeerId w d.Id
        writeEndpoint w d.Endpoint
        writeU64 w (Incarnation.value d.Incarnation)
        writeState w d.State

    let private readDigest (r: BinaryReader) : MemberDigest =
        let id = readPeerId r
        let ep = readEndpoint r
        let inc = Incarnation (readU64 r)
        let st = readState r
        { Id = id; Endpoint = ep; Incarnation = inc; State = st }

    let private writeDigests (w: BinaryWriter) (ds: MemberDigest list) =
        writeU32 w (uint32 (List.length ds))
        for d in ds do writeDigest w d

    let private readDigests (r: BinaryReader) : MemberDigest list =
        let n = int (readU32 r)
        [ for _ in 1 .. n -> readDigest r ]

    let encode (envelope: WireEnvelope) : byte[] =
        use ms = new MemoryStream()
        use w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen = true)
        match envelope with
        | Hello (sender, ep, epoch) ->
            w.Write TagHello
            writePeerId w sender
            writeEndpoint w ep
            writeU64 w (Epoch.value epoch)
        | Welcome (receiver, digests, epoch) ->
            w.Write TagWelcome
            writePeerId w receiver
            writeU64 w (Epoch.value epoch)
            writeDigests w digests
        | Ping (sender, epoch, digests) ->
            w.Write TagPing
            writePeerId w sender
            writeU64 w (Epoch.value epoch)
            writeDigests w digests
        | Ack (sender, epoch, digests) ->
            w.Write TagAck
            writePeerId w sender
            writeU64 w (Epoch.value epoch)
            writeDigests w digests
        | PingReq (sender, target, nonce) ->
            w.Write TagPingReq
            writePeerId w sender
            writePeerId w target
            writeU32 w nonce
        | PingReqAck (sender, target, nonce) ->
            w.Write TagPingReqAck
            writePeerId w sender
            writePeerId w target
            writeU32 w nonce
        | Leaving (sender, epoch) ->
            w.Write TagLeaving
            writePeerId w sender
            writeU64 w (Epoch.value epoch)
        w.Flush()
        ms.ToArray()

    let decode (payload: byte[]) : Result<WireEnvelope, TransportError> =
        try
            use ms = new MemoryStream(payload)
            use r = new BinaryReader(ms, Encoding.UTF8)
            let tag = r.ReadByte()
            match tag with
            | b when b = TagHello ->
                let sender = readPeerId r
                let ep = readEndpoint r
                let epoch = Epoch (readU64 r)
                Ok (Hello (sender, ep, epoch))
            | b when b = TagWelcome ->
                let receiver = readPeerId r
                let epoch = Epoch (readU64 r)
                let digests = readDigests r
                Ok (Welcome (receiver, digests, epoch))
            | b when b = TagPing ->
                let sender = readPeerId r
                let epoch = Epoch (readU64 r)
                let digests = readDigests r
                Ok (Ping (sender, epoch, digests))
            | b when b = TagAck ->
                let sender = readPeerId r
                let epoch = Epoch (readU64 r)
                let digests = readDigests r
                Ok (Ack (sender, epoch, digests))
            | b when b = TagPingReq ->
                let sender = readPeerId r
                let target = readPeerId r
                let nonce = readU32 r
                Ok (PingReq (sender, target, nonce))
            | b when b = TagPingReqAck ->
                let sender = readPeerId r
                let target = readPeerId r
                let nonce = readU32 r
                Ok (PingReqAck (sender, target, nonce))
            | b when b = TagLeaving ->
                let sender = readPeerId r
                let epoch = Epoch (readU64 r)
                Ok (Leaving (sender, epoch))
            | other ->
                Error (MalformedFrame (sprintf "unknown envelope tag: %d" (int other)))
        with ex ->
            // Catches BCL-level truncation / decode errors (EndOfStreamException,
            // ArgumentException from Guid parsing, InvalidDataException from
            // readState on unknown state tags, etc.).
            Error (MalformedFrame ex.Message)


[<RequireQualifiedAccess>]
module internal Framing =

    let private prefixOfLength (len: uint32) =
        [|
            byte (len >>> 24)
            byte (len >>> 16)
            byte (len >>> 8)
            byte len
        |]

    let tryWriteFrameAsync
            (maxFrame: int)
            (stream: Stream)
            (payload: byte[])
            (ct: CancellationToken) =
        task {
            if payload.Length > maxFrame then
                return Error (FrameTooLarge payload.Length)
            else
                try
                    let len = uint32 payload.Length
                    let prefix = prefixOfLength len
                    do! stream.WriteAsync(prefix, 0, 4, ct)
                    do! stream.WriteAsync(payload, 0, payload.Length, ct)
                    do! stream.FlushAsync(ct)
                    return Ok ()
                with
                | :? OperationCanceledException -> return raise (OperationCanceledException ct)
                | ex ->
                    return Error (MalformedFrame ex.Message)
        }

    let private readExactlyAsync (stream: Stream) (count: int) (ct: CancellationToken) =
        task {
            let buf = Array.zeroCreate count
            let mutable off = 0
            let mutable eof = false
            while not eof && off < count do
                let! n = stream.ReadAsync(buf, off, count - off, ct)
                if n <= 0 then eof <- true
                else off <- off + n
            return if eof then None else Some buf
        }

    let tryReadFrameAsync
            (maxFrame: int)
            (stream: Stream)
            (ct: CancellationToken) =
        task {
            try
                let! prefix = readExactlyAsync stream 4 ct
                match prefix with
                | None -> return Ok ValueNone
                | Some prefix ->
                    let len =
                        (uint32 prefix.[0] <<< 24)
                        ||| (uint32 prefix.[1] <<< 16)
                        ||| (uint32 prefix.[2] <<< 8)
                        ||| (uint32 prefix.[3])
                    let len = int len
                    if len < 0 || len > maxFrame then
                        return Error (FrameTooLarge len)
                    else
                        let! payload = readExactlyAsync stream len ct
                        match payload with
                        | None -> return Error (MalformedFrame "truncated frame body")
                        | Some payload -> return Ok (ValueSome payload)
            with
            | :? OperationCanceledException -> return raise (OperationCanceledException ct)
            | ex ->
                return Error (MalformedFrame ex.Message)
        }


/// Effectful transport surface. One instance per process.
type PeerTransport =
    /// Bind the listener and kick off receive coordination. Per-peer sender
    /// runtimes connect lazily. Fails fast only on bind errors; per-peer
    /// connect errors
    /// surface via `InboundEvent.PeerDropped`.
    abstract Start : unit -> Result<unit, TransportError>
    /// Fire-and-forget. Queues onto that peer's bounded sender buffer.
    /// Never blocks on TCP. Drops oldest if the sender queue is full
    /// (control plane is idempotent — gossip will re-converge).
    abstract Send : PeerEndpoint -> WireEnvelope -> unit
    /// Broadcast to a set of endpoints.
    abstract Broadcast : PeerEndpoint seq -> WireEnvelope -> unit
    /// Subscribe to inbound frames and connection events. Returns an
    /// unsubscribe thunk. Multiple subscribers allowed.
    abstract Subscribe : (InboundEvent -> unit) -> (unit -> unit)
    /// Cleanly stop listener + all per-peer sender runtimes.
    abstract Stop : unit -> unit


[<RequireQualifiedAccess>]
module PeerTransport =

    type private SenderConnection =
        | Disconnected
        | Connected of TcpClient * NetworkStream

    type private SenderMsg =
        | Enqueue of byte[]
        | ConnectionOpened of TcpClient * NetworkStream
        | ConnectionFailed of exn
        | RetryConnect
        | WriteFinished of Result<unit, TransportError>
        | StopSender of AsyncReplyChannel<unit>

    type private Sender =
        { Endpoint: PeerEndpoint
          Agent: MailboxProcessor<SenderMsg> }

    type private SenderState =
        { Buffer: Queue<byte[]>
          Connection: SenderConnection
          BackoffMs: int
          ConnectPending: bool
          RetryPending: bool
          WritePending: bool
          Stopping: bool }

    type private ReceiverSession =
        { Client: TcpClient
          Stream: NetworkStream
          Remote: PeerEndpoint }

    type private ReceiverMsg =
        | StartAccepting
        | Accepted of TcpClient
        | AcceptFailed of exn
        | FrameReceived of TcpClient * WireEnvelope
        | ReceiveClosed of TcpClient
        | ReceiveFailed of TcpClient * TransportError
        | StopReceiver of AsyncReplyChannel<unit>

    type private ReceiverState =
        { Sessions: Dictionary<TcpClient, ReceiverSession>
          AcceptPending: bool
          Stopping: bool }

    type private TransportState =
        { Self: PeerId
          Listen: PeerEndpoint
          MaxFrame: int
          QueueBound: int
          Log: LogLevel -> (unit -> string) -> unit
          Listener: TcpListener
          Subs: ConcurrentDictionary<int, InboundEvent -> unit>
          NextSubId: ref<int>
          Senders: ConcurrentDictionary<PeerEndpoint, Sender>
          mutable Started: bool
          mutable Stopped: bool }

    let private notify (st: TransportState) (ev: InboundEvent) =
        for KeyValue(_, handler) in st.Subs do
            try handler ev
            with ex ->
                st.Log LogLevel.Warn (fun _ -> sprintf "subscriber threw: %s" ex.Message)

    let internal enqueueBounded (queueBound: int) (buffer: Queue<byte[]>) (payload: byte[]) =
        if queueBound <= 0 then
            invalidArg "queueBound" "queue bound must be positive"

        if buffer.Count = queueBound then
            buffer.Dequeue() |> ignore

        buffer.Enqueue payload

    let private postSafely (inbox: MailboxProcessor<'msg>) (msg: 'msg) =
        try inbox.Post msg
        with :? InvalidOperationException -> ()

    let private cancelSource (cts: CancellationTokenSource) =
        try cts.Cancel()
        with :? ObjectDisposedException -> ()

    let private disposeStream (stream: NetworkStream) =
        try stream.Dispose()
        with
        | :? ObjectDisposedException -> ()
        | :? IOException -> ()

    let private closeClient (client: TcpClient) =
        try client.Close()
        with
        | :? ObjectDisposedException -> ()
        | :? SocketException -> ()

    let private stopListener (listener: TcpListener) =
        try listener.Stop()
        with
        | :? ObjectDisposedException -> ()
        | :? SocketException -> ()

    let private closeSenderConnection (connection: SenderConnection) =
        match connection with
        | Disconnected -> ()
        | Connected (client, stream) ->
            disposeStream stream
            closeClient client

    let private closeReceiverSession (session: ReceiverSession) =
        disposeStream session.Stream
        closeClient session.Client

    let private remoteOfClient (client: TcpClient) =
        try
            match client.Client.RemoteEndPoint with
            | :? IPEndPoint as endpoint ->
                { Host = endpoint.Address.ToString()
                  Port = endpoint.Port }
            | _ -> { Host = "?"; Port = 0 }
        with
        | :? ObjectDisposedException -> { Host = "?"; Port = 0 }
        | :? SocketException -> { Host = "?"; Port = 0 }

    let private startSenderConnect
            (endpoint: PeerEndpoint)
            (token: CancellationToken)
            (inbox: MailboxProcessor<SenderMsg>) =
        async {
            let mutable client : TcpClient = Unchecked.defaultof<_>
            try
                client <- new TcpClient()
                client.NoDelay <- true
                do!
                    client.ConnectAsync(endpoint.Host, endpoint.Port, token).AsTask()
                    |> Async.AwaitTask
                let stream = client.GetStream()
                postSafely inbox (ConnectionOpened (client, stream))
            with
            | :? OperationCanceledException ->
                if not (isNull client) then closeClient client
            | ex ->
                if not (isNull client) then closeClient client
                postSafely inbox (ConnectionFailed ex)
        }
        |> Async.Start

    let private startSenderRetry
            (delayMs: int)
            (token: CancellationToken)
            (inbox: MailboxProcessor<SenderMsg>) =
        async {
            try
                do! Task.Delay(delayMs, token) |> Async.AwaitTask
                postSafely inbox RetryConnect
            with :? OperationCanceledException -> ()
        }
        |> Async.Start

    let private startSenderWrite
            (maxFrame: int)
            (stream: NetworkStream)
            (payload: byte[])
            (token: CancellationToken)
            (inbox: MailboxProcessor<SenderMsg>) =
        async {
            try
                let! result =
                    Framing.tryWriteFrameAsync maxFrame stream payload token
                    |> Async.AwaitTask

                postSafely inbox (WriteFinished result)
            with :? OperationCanceledException -> ()
        }
        |> Async.Start

    let private createSenderAgent (st: TransportState) (endpoint: PeerEndpoint) =
        let cts = new CancellationTokenSource()

        let initialState =
            { Buffer = Queue<byte[]>()
              Connection = Disconnected
              BackoffMs = 200
              ConnectPending = false
              RetryPending = false
              WritePending = false
              Stopping = false }

        MailboxProcessor<SenderMsg>.Start(fun inbox ->
            let token = cts.Token

            let schedule (state: SenderState) =
                if state.Stopping then
                    state
                else
                    match state.Connection with
                    | Disconnected when state.Buffer.Count > 0 && not state.ConnectPending && not state.RetryPending ->
                        startSenderConnect endpoint token inbox
                        { state with ConnectPending = true }
                    | Connected (_, stream) when state.Buffer.Count > 0 && not state.WritePending ->
                        let payload = state.Buffer.Dequeue()
                        startSenderWrite st.MaxFrame stream payload token inbox
                        { state with WritePending = true }
                    | _ -> state

            let rec loop (state: SenderState) =
                async {
                    let! msg = inbox.Receive()

                    let nextState =
                        match msg with
                        | Enqueue payload when state.Stopping -> state
                        | Enqueue payload ->
                            enqueueBounded st.QueueBound state.Buffer payload
                            state
                        | ConnectionOpened (client, stream) ->
                            closeSenderConnection state.Connection

                            { state with
                                Connection = Connected (client, stream)
                                BackoffMs = 200
                                ConnectPending = false
                                RetryPending = false }
                        | ConnectionFailed ex ->
                            notify st (PeerDropped (endpoint, ConnectFailed (endpoint, ex)))

                            if not state.Stopping then
                                startSenderRetry state.BackoffMs token inbox

                            { state with
                                Connection = Disconnected
                                ConnectPending = false
                                RetryPending = not state.Stopping
                                BackoffMs = min (state.BackoffMs * 2) 10000 }
                        | RetryConnect ->
                            { state with RetryPending = false }
                        | WriteFinished (Ok ()) ->
                            { state with WritePending = false }
                        | WriteFinished (Error err) ->
                            closeSenderConnection state.Connection
                            notify st (PeerDropped (endpoint, err))

                            { state with
                                Connection = Disconnected
                                WritePending = false }
                        | StopSender reply ->
                            cancelSource cts
                            closeSenderConnection state.Connection
                            state.Buffer.Clear()
                            reply.Reply()

                            { state with
                                Connection = Disconnected
                                ConnectPending = false
                                RetryPending = false
                                WritePending = false
                                Stopping = true }

                    let scheduledState = schedule nextState

                    if scheduledState.Stopping then
                        return ()
                    else
                        return! loop scheduledState
                }

            loop initialState)

    let private getOrCreateSender (st: TransportState) (ep: PeerEndpoint) : Sender =
        let factory =
            Func<PeerEndpoint, Sender>(fun endpoint ->
                { Endpoint = endpoint
                  Agent = createSenderAgent st endpoint })

        st.Senders.GetOrAdd(ep, factory)

    let private startAccept
            (listener: TcpListener)
            (token: CancellationToken)
            (inbox: MailboxProcessor<ReceiverMsg>) =
        async {
            try
                let! client =
                    listener.AcceptTcpClientAsync(token).AsTask()
                    |> Async.AwaitTask

                postSafely inbox (Accepted client)
            with
            | :? OperationCanceledException -> ()
            | :? ObjectDisposedException when token.IsCancellationRequested -> ()
            | ex ->
                postSafely inbox (AcceptFailed ex)
        }
        |> Async.Start

    let private startReceive
            (maxFrame: int)
            (token: CancellationToken)
            (inbox: MailboxProcessor<ReceiverMsg>)
            (session: ReceiverSession) =
        async {
            try
                let! frame =
                    Framing.tryReadFrameAsync maxFrame session.Stream token
                    |> Async.AwaitTask

                match frame with
                | Ok ValueNone ->
                    postSafely inbox (ReceiveClosed session.Client)
                | Ok (ValueSome payload) ->
                    match Wire.decode payload with
                    | Ok envelope ->
                        postSafely inbox (FrameReceived (session.Client, envelope))
                    | Error err ->
                        postSafely inbox (ReceiveFailed (session.Client, err))
                | Error err ->
                    postSafely inbox (ReceiveFailed (session.Client, err))
            with
            | :? OperationCanceledException -> ()
            | ex ->
                postSafely inbox (ReceiveFailed (session.Client, MalformedFrame ex.Message))
        }
        |> Async.Start

    let private createReceiverAgent (st: TransportState) =
        let cts = new CancellationTokenSource()

        let initialState =
            { Sessions = Dictionary<TcpClient, ReceiverSession>()
              AcceptPending = false
              Stopping = false }

        MailboxProcessor<ReceiverMsg>.Start(fun inbox ->
            let token = cts.Token

            let ensureAccepting (state: ReceiverState) =
                if state.Stopping || state.AcceptPending then
                    state
                else
                    startAccept st.Listener token inbox
                    { state with AcceptPending = true }

            let detachSession (state: ReceiverState) (client: TcpClient) =
                match state.Sessions.TryGetValue client with
                | true, session ->
                    state.Sessions.Remove client |> ignore
                    Some session
                | false, _ -> None

            let rec loop (state: ReceiverState) =
                async {
                    let! msg = inbox.Receive()

                    let nextState =
                        match msg with
                        | StartAccepting -> state
                        | Accepted client ->
                            let remote = remoteOfClient client
                            let acceptedState = { state with AcceptPending = false }

                            try
                                client.NoDelay <- true

                                let session =
                                    { Client = client
                                      Stream = client.GetStream()
                                      Remote = remote }

                                acceptedState.Sessions.[client] <- session
                                startReceive st.MaxFrame token inbox session
                                acceptedState
                            with
                            | :? ObjectDisposedException as ex ->
                                closeClient client
                                notify st (PeerDropped (remote, MalformedFrame ex.Message))
                                acceptedState
                            | :? InvalidOperationException as ex ->
                                closeClient client
                                notify st (PeerDropped (remote, MalformedFrame ex.Message))
                                acceptedState
                            | :? IOException as ex ->
                                closeClient client
                                notify st (PeerDropped (remote, MalformedFrame ex.Message))
                                acceptedState
                        | AcceptFailed ex ->
                            let failedState = { state with AcceptPending = false }

                            if not failedState.Stopping then
                                st.Log LogLevel.Warn (fun _ -> sprintf "accept failed: %s" ex.Message)

                            failedState
                        | FrameReceived (client, envelope) ->
                            match state.Sessions.TryGetValue client with
                            | true, session ->
                                notify st (Received (session.Remote, envelope))
                                startReceive st.MaxFrame token inbox session
                            | false, _ -> ()

                            state
                        | ReceiveClosed client ->
                            match detachSession state client with
                            | Some session -> closeReceiverSession session
                            | None -> ()

                            state
                        | ReceiveFailed (client, err) ->
                            match detachSession state client with
                            | Some session ->
                                closeReceiverSession session
                                notify st (PeerDropped (session.Remote, err))
                            | None -> ()

                            state
                        | StopReceiver reply ->
                            cancelSource cts
                            stopListener st.Listener

                            for session in state.Sessions.Values do
                                closeReceiverSession session

                            state.Sessions.Clear()
                            reply.Reply()

                            { state with
                                AcceptPending = false
                                Stopping = true }

                    let scheduledState =
                        match msg with
                        | StopReceiver _ -> nextState
                        | _ -> ensureAccepting nextState

                    if scheduledState.Stopping then
                        return ()
                    else
                        return! loop scheduledState
                }

            loop initialState)

    let create
            (log: LogLevel -> (unit -> string) -> unit)
            (self: PeerId)
            (listen: PeerEndpoint)
            (sendQueueBound: int)
            (maxFrame: int)
            : PeerTransport =

        let listener =
            let ip =
                match IPAddress.TryParse listen.Host with
                | true, addr -> addr
                | _ -> IPAddress.Any
            new TcpListener(ip, listen.Port)

        let st =
            { Self = self
              Listen = listen
              MaxFrame = maxFrame
              QueueBound = sendQueueBound
              Log = log
              Listener = listener
              Subs = ConcurrentDictionary<int, InboundEvent -> unit>()
              NextSubId = ref 0
              Senders = ConcurrentDictionary<PeerEndpoint, Sender>()
              Started = false
              Stopped = false }

        let receiver = createReceiverAgent st

        { new PeerTransport with
            member _.Start () =
                if st.Started then Ok ()
                else
                    try
                        st.Listener.Start()
                        st.Started <- true
                        receiver.Post StartAccepting
                        Ok ()
                    with ex ->
                        Error (BindFailed (listen, ex))

            member _.Send ep env =
                if not st.Stopped then
                    let sender = getOrCreateSender st ep
                    let bytes = Wire.encode env
                    sender.Agent.Post (Enqueue bytes)

            member this.Broadcast eps env =
                for ep in eps do
                    this.Send ep env

            member _.Subscribe handler =
                let id = Interlocked.Increment st.NextSubId
                st.Subs.[id] <- handler
                fun () -> st.Subs.TryRemove id |> ignore

            member _.Stop () =
                if st.Stopped then () else
                st.Stopped <- true
                receiver.PostAndReply StopReceiver

                for KeyValue(_, sender) in st.Senders do
                    sender.Agent.PostAndReply StopSender

                st.Subs.Clear()
        }
