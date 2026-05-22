// WebSocket implementation of the abstract network layer.
// Uses websocket-sharp (sta/websocket-sharp on GitHub).
//
// WebSocket already frames messages — no length-prefix protocol needed.
// Both client and server receive complete messages, so there is no reassembly.

using Serilog;
using System.Threading.Channels;
using WebSocketSharp;

// ---------------------------------------------------------------------------
// Client
// ---------------------------------------------------------------------------
public sealed class WebSocketNetClient : NetClient
{
    private readonly Channel<NetIncomingMessage> _inbox =
        Channel.CreateUnbounded<NetIncomingMessage>(
            new UnboundedChannelOptions { SingleReader = true });

    private WebSocketClientConnection? _connection;

    public override void Start() { }

    public override NetConnection Connect(string ip, int port)
    {
        string url = $"ws://{ip}:{port}/Game";
        WebSocket ws = new(url);

        _connection = new WebSocketClientConnection(ws, $"{ip}:{port}");

        ws.OnOpen += (_, _) =>
            _inbox.Writer.TryWrite(new NetIncomingMessage
            {
                Type = NetworkMessageType.Connect,
                SenderConnection = _connection,
            });

        ws.OnMessage += (_, e) =>
            _inbox.Writer.TryWrite(new NetIncomingMessage
            {
                Type = NetworkMessageType.Data,
                Payload = e.RawData,
                SenderConnection = _connection,
            });

        ws.OnClose += (_, _) =>
            _inbox.Writer.TryWrite(new NetIncomingMessage
            {
                Type = NetworkMessageType.Disconnect,
                SenderConnection = _connection,
            });

        ws.OnError += (_, e) =>
            Log.Warning("WebSocket client error: {Message}", e.Message);

        ws.ConnectAsync();
        return _connection;
    }

    public override NetIncomingMessage? ReadMessage()
    {
        _ = _inbox.Reader.TryRead(out NetIncomingMessage? msg);
        return msg;
    }

    public override void SendMessage(ReadOnlyMemory<byte> payload, MyNetDeliveryMethod method) => _connection?.SendMessage(payload, method);
}

public sealed class WebSocketClientConnection : NetConnection
{
    private readonly WebSocket _ws;
    private readonly string _address;

    internal WebSocketClientConnection(WebSocket ws, string address)
    {
        _ws = ws;
        _address = address;
    }

    public override IpEndpoint RemoteEndPoint()
        => IpEndpointDefault.Create(_address);

    public override void SendMessage(ReadOnlyMemory<byte> payload, MyNetDeliveryMethod method, int sequenceChannel = 0)
    {
        // websocket-sharp queues SendAsync calls internally — no manual queue needed.
        _ws.SendAsync(payload.ToArray(), completed =>
        {
            if (!completed)
            {
                Log.Warning("WebSocket client send did not complete.");
            }
        });
    }

    public override void Update() { }

    public override bool EqualsConnection(NetConnection other)
        => other is WebSocketClientConnection w && w._address == _address;
}

// ---------------------------------------------------------------------------
// Server
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// Server-side session (one instance per connected client, created by websocket-sharp)
// ---------------------------------------------------------------------------

