// ENet implementation of the abstract network layer.
//
// ENet is a reliable UDP library. The platform abstraction owns the actual
// ENet host/peer handles; this layer translates ENet events into the
// NetIncomingMessage model the rest of the game uses.

// ---------------------------------------------------------------------------
// ENet primitives (platform-provided abstractions, unchanged except cleanup)
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// Client
// ---------------------------------------------------------------------------

public sealed class EnetNetClient : NetClient
{
    private readonly INetworkService _platform;

    private EnetHost? _host;
    private EnetNetConnection? _connection;

    // Two-phase connection: EnetHostConnect fires immediately (connected),
    // but the ENet handshake completes asynchronously (ready to send).
    private bool _connecting;
    private bool _handshakeComplete;

    // Messages that arrived before the caller polled ReadMessage.
    private readonly Queue<NetIncomingMessage> _inbox = new();

    // Outgoing messages queued before the ENet handshake completed.
    private readonly Queue<ReadOnlyMemory<byte>> _pendingSend = new();

    public EnetNetClient(INetworkService platform)
    {
        _platform = platform;
    }

    public override void Start()
    {
        Console.WriteLine("[ENet] Start() called");
        _host = _platform.EnetCreateHost();
        _platform.EnetHostInitialize(_host, null, 1, 0, 0, 0);
        Console.WriteLine("[ENet] Host initialized");
    }

    public override NetConnection Connect(string ip, int port)
    {
        Console.WriteLine($"[ENet] Connect() called → {ip}:{port}");
        bool available = _platform.EnetAvailable();
        Console.WriteLine($"[ENet] ENet available: {available}");

        EnetPeer peer = _platform.EnetHostConnect(_host!, ip, port, channelCount: 2, data: 0);
        Console.WriteLine($"[ENet] Peer created: {peer != null}");

        _connection = new EnetNetConnection(_platform, peer);
        _connecting = true;
        return _connection;
    }

    public override NetIncomingMessage? ReadMessage()
    {
        if (!_connecting)
        {
            return null;
        }

        if (_handshakeComplete)
        {
            while (_pendingSend.TryDequeue(out ReadOnlyMemory<byte> pending))
            {
                Console.WriteLine("[ENet] Flushing pending send");
                _connection!.SendMessage(pending, MyNetDeliveryMethod.ReliableOrdered);
            }
        }

        if (_inbox.TryDequeue(out NetIncomingMessage? queued))
        {
            return queued;
        }

        PollEnetEvents();

        _inbox.TryDequeue(out NetIncomingMessage? next);
        return next;
    }

    public override void SendMessage(ReadOnlyMemory<byte> payload, MyNetDeliveryMethod method)
    {
        if (!_handshakeComplete)
        {
            Console.WriteLine("[ENet] SendMessage queued (handshake not complete yet)");
            _pendingSend.Enqueue(payload);
            return;
        }

        _connection!.SendMessage(payload, method);
    }

    private int _pollCount = 0;

    private void PollEnetEvents()
    {
        _pollCount++;

        // Log every 300 polls (~5 sec at 60fps) so we can see if polling is alive
        if (_pollCount % 300 == 0)
        {
            Console.WriteLine($"[ENet] Still polling... count={_pollCount} handshake={_handshakeComplete} connecting={_connecting}");
        }

        EnetEvent? ev = _platform.EnetHostService(_host, timeout: 0);
        if (ev is null)
        {
            return;
        }

        Console.WriteLine($"[ENet] EnetHostService returned event: {ev.Type()}");

        do
        {
            HandleEvent(ev);
            ev = _platform.EnetHostCheckEvents(_host!);
            if (ev != null)
                Console.WriteLine($"[ENet] EnetHostCheckEvents returned event: {ev.Type()}");
        }
        while (ev is not null);
    }

    private void HandleEvent(EnetEvent ev)
    {
        EnetEventType type = ev.Type();
        Console.WriteLine($"[ENet] HandleEvent: {type}");

        switch (type)
        {
            case EnetEventType.Connect:
                Console.WriteLine("[ENet] ✅ Handshake complete — connected!");
                _handshakeComplete = true;
                break;

            case EnetEventType.Receive:
                EnetPacket packet = ev.Packet();
                int length = packet.GetBytesCount();
                Console.WriteLine($"[ENet] 📦 Packet received, length={length}");
                ReadOnlyMemory<byte> payload = packet.GetBytes().AsMemory(0, length);
                packet.Dispose();
                _inbox.Enqueue(new NetIncomingMessage
                {
                    Type = NetworkMessageType.Data,
                    Payload = payload,
                    SenderConnection = _connection,
                });
                break;

            case EnetEventType.Disconnect:
                Console.WriteLine("[ENet] ❌ Disconnected");
                _inbox.Enqueue(new NetIncomingMessage
                {
                    Type = NetworkMessageType.Disconnect,
                    SenderConnection = _connection,
                });
                _handshakeComplete = false;
                _connecting = false;
                break;

            default:
                Console.WriteLine($"[ENet] Unknown event type: {type}");
                break;
        }
    }
}
