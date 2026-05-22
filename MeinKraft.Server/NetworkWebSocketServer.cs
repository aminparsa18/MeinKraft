using System.Threading.Channels;
using WebSocketSharp;
using WebSocketSharp.Server;
using ErrorEventArgs = WebSocketSharp.ErrorEventArgs;

public sealed class WebSocketNetServer : NetServer
{
    private readonly Channel<NetIncomingMessage> _inbox =
        Channel.CreateUnbounded<NetIncomingMessage>();

    private readonly ILogger<WebSocketNetServer> _logger;

    private WebSocketServer? _server;
    private int _port;

    public override void SetPort(int port) => _port = port;

    public override void Start()
    {
        _server = new WebSocketServer(_port);

        // Pass the inbox writer into the behavior factory so there is no singleton.
        ChannelWriter<NetIncomingMessage> writer = _inbox.Writer;
        _server.AddWebSocketService("/Game", () => new WebSocketSession(writer));
        _server.Start();

        if (_server.IsListening)
        {
            _logger.LogInformation("WebSocket server listening on port {Port}.", _port);
        }
    }

    public override NetIncomingMessage? ReadMessage()
    {
        _inbox.Reader.TryRead(out NetIncomingMessage? msg);
        return msg;
    }

    public void Stop() => _server?.Stop();
}

internal sealed class WebSocketSession : WebSocketBehavior
{
    private readonly ChannelWriter<NetIncomingMessage> _inbox;
    private WebSocketConnection? _connection;

    internal WebSocketSession(ChannelWriter<NetIncomingMessage> inbox)
    {
        IgnoreExtensions = true;
        _inbox = inbox;
    }

    protected override void OnOpen()
    {
        _connection = new WebSocketConnection(this);
        _inbox.TryWrite(new NetIncomingMessage
        {
            Type = NetworkMessageType.Connect,
            SenderConnection = _connection,
        });
    }

    protected override void OnMessage(MessageEventArgs e)
    {
        _inbox.TryWrite(new NetIncomingMessage
        {
            Type = NetworkMessageType.Data,
            Payload = e.RawData,
            SenderConnection = _connection,
        });
    }

    protected override void OnClose(CloseEventArgs e)
    {
        _inbox.TryWrite(new NetIncomingMessage
        {
            Type = NetworkMessageType.Disconnect,
            SenderConnection = _connection,
        });
    }

    protected override void OnError(ErrorEventArgs e) => Log.Warn($"WebSocket session error: {e.Message}");

    internal void Send(ReadOnlyMemory<byte> payload)
    {
        // websocket-sharp's SendAsync handles its own internal send queue.
        SendAsync(payload.ToArray(), completed =>
        {
            if (!completed)
            {
                Log.Warn("WebSocket server send did not complete.");
            }
        });
    }
}

// ---------------------------------------------------------------------------
// Server-side connection handle
// ---------------------------------------------------------------------------

public sealed class WebSocketConnection : NetConnection
{
    private readonly WebSocketSession _session;

    internal WebSocketConnection(WebSocketSession session)
    {
        _session = session;
    }

    public override IpEndpoint RemoteEndPoint()
    {
        try
        {
            return IpEndpointDefault.Create(
                _session.Context.UserEndPoint.Address.ToString());
        }
        catch
        {
            return IpEndpointDefault.Create("unknown");
        }
    }

    public override void SendMessage(ReadOnlyMemory<byte> payload, MyNetDeliveryMethod method, int sequenceChannel = 0) => _session.Send(payload);

    public override void Update() { }

    public override bool EqualsConnection(NetConnection other)
        => other is WebSocketConnection w && w._session == _session;
}
