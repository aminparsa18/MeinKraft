// ENet implementation of the abstract network layer.
//
// ENet is a reliable UDP library. The platform abstraction owns the actual
// ENet host/peer handles; this layer translates ENet events into the
// NetIncomingMessage model the rest of the game uses.

// ---------------------------------------------------------------------------
// ENet primitives (platform-provided abstractions, unchanged except cleanup)
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// Server
// ---------------------------------------------------------------------------

public sealed class EnetNetServer : NetServer
{
    private readonly INetworkService _platform;

    private EnetHost? _host;
    private int _port;
    private int _nextClientId;

    private readonly Queue<NetIncomingMessage> _inbox = new();

    // Connections keyed by the integer UserData we assigned at connect time.
    // This means every event for the same peer reuses the same connection
    // object rather than allocating a new one per event.
    private readonly Dictionary<int, EnetNetConnection> _connections = new();

    public EnetNetServer(INetworkService platform)
    {
        _platform = platform;
    }

    public override void SetPort(int port) => _port = port;

    public override void Start()
    {
        _host = _platform.EnetCreateHost();
        _platform.EnetHostInitializeServer(_host, _port, 256);
    }

    public override NetIncomingMessage? ReadMessage()
    {
        if (_inbox.TryDequeue(out NetIncomingMessage? queued))
        {
            return queued;
        }

        PollEnetEvents();

        _inbox.TryDequeue(out NetIncomingMessage? next);
        return next;
    }

    private void PollEnetEvents()
    {
        EnetEvent? ev = _platform.EnetHostService(_host!, timeout: 0);
        if (ev is null)
        {
            return;
        }

        do
        {
            HandleEvent(ev);
            ev = _platform.EnetHostCheckEvents(_host!);
        }
        while (ev is not null);
    }

    private void HandleEvent(EnetEvent ev)
    {
        switch (ev.Type())
        {
            case EnetEventType.Connect:
                {
                    EnetPeer peer = ev.Peer();
                    int id = _nextClientId++;
                    peer.SetUserData(id);
                    EnetNetConnection conn = new(_platform, peer);
                    _connections[id] = conn;
                    _inbox.Enqueue(new NetIncomingMessage
                    {
                        Type = NetworkMessageType.Connect,
                        SenderConnection = conn,
                    });
                    break;
                }

            case EnetEventType.Receive:
                {
                    EnetPacket packet = ev.Packet();
                    ReadOnlyMemory<byte> payload = packet.GetBytes().AsMemory(0, packet.GetBytesCount());
                    packet.Dispose();
                    EnetNetConnection conn = GetConnection(ev.Peer());
                    _inbox.Enqueue(new NetIncomingMessage
                    {
                        Type = NetworkMessageType.Data,
                        Payload = payload,
                        SenderConnection = conn,
                    });
                    break;
                }

            case EnetEventType.Disconnect:
                {
                    EnetNetConnection conn = GetConnection(ev.Peer());
                    _connections.Remove(conn.Peer.UserData());
                    _inbox.Enqueue(new NetIncomingMessage
                    {
                        Type = NetworkMessageType.Disconnect,
                        SenderConnection = conn,
                    });
                    break;
                }
        }
    }

    private EnetNetConnection GetConnection(EnetPeer peer)
    {
        int id = peer.UserData();
        if (!_connections.TryGetValue(id, out EnetNetConnection? conn))
        {
            // Defensive fallback — should not happen in normal operation.
            conn = new EnetNetConnection(_platform, peer);
            _connections[id] = conn;
        }

        return conn;
    }
}