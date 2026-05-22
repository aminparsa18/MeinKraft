// TCP client implementation of the abstract network layer.
//
// Framing protocol: every message is prefixed with a 4-byte big-endian length.
//   [4 bytes length][N bytes payload]
//
// Receiving runs on a dedicated async loop started at connect time.
// The game loop calls ReadMessage() which drains an in-memory queue — no I/O
// on the calling thread, no sends hidden inside reads.

using System.Buffers.Binary;
using System.Net.Sockets;
using System.Threading.Channels;

public sealed class TcpNetClient : NetClient
{
    // Completed messages waiting for the game loop to poll.
    private readonly Channel<ReadOnlyMemory<byte>> _inbox =
        Channel.CreateUnbounded<ReadOnlyMemory<byte>>(
            new UnboundedChannelOptions { SingleReader = true });

    // Outgoing messages queued before the connection is established.
    private readonly Channel<ReadOnlyMemory<byte>> _pendingSend =
        Channel.CreateUnbounded<ReadOnlyMemory<byte>>(
            new UnboundedChannelOptions { SingleWriter = true });

    private TcpNetConnection? _connection;
    private CancellationTokenSource? _cts;

    public override void Start() { }

    public override NetConnection Connect(string ip, int port)
    {
        _cts = new CancellationTokenSource();
        _connection = new TcpNetConnection(ip, port, _inbox.Writer, _pendingSend.Reader, _cts.Token);
        _ = _connection.RunAsync();
        return _connection;
    }

    public override NetIncomingMessage? ReadMessage()
    {
        return _inbox.Reader.TryRead(out ReadOnlyMemory<byte> payload)
            ? new NetIncomingMessage
            {
                Type = NetworkMessageType.Data,
                Payload = payload,
                SenderConnection = _connection,
            }
            : null;
    }

    public override void SendMessage(ReadOnlyMemory<byte> payload, MyNetDeliveryMethod method)
        // Always enqueue — RunAsync flushes _pendingSend once connected,
        // so pre-connection messages are delivered in order after the handshake.
        => _pendingSend.Writer.TryWrite(payload);

    public void Disconnect() => _cts?.Cancel();
}

// ---------------------------------------------------------------------------
// Connection handle + async I/O loop
// ---------------------------------------------------------------------------

public sealed class TcpNetConnection : NetConnection
{
    private readonly string _ip;
    private readonly int _port;
    private readonly ChannelWriter<ReadOnlyMemory<byte>> _inbox;
    private readonly ChannelReader<ReadOnlyMemory<byte>> _pendingSend;
    private readonly CancellationToken _ct;

    private TcpClient? _tcp;
    private NetworkStream? _stream;

    internal TcpNetConnection(
        string ip, int port,
        ChannelWriter<ReadOnlyMemory<byte>> inbox,
        ChannelReader<ReadOnlyMemory<byte>> pendingSend,
        CancellationToken ct)
    {
        _ip = ip;
        _port = port;
        _inbox = inbox;
        _pendingSend = pendingSend;
        _ct = ct;
    }

    public override IpEndpoint RemoteEndPoint()
        => IpEndpointDefault.Create($"{_ip}:{_port}");

    public override void Update() { }

    public override bool EqualsConnection(NetConnection other)
        => other is TcpNetConnection t && t._ip == _ip && t._port == _port;

    /// <summary>
    /// Connects, flushes any queued pre-connection sends, then runs the
    /// receive loop until cancelled or the remote end closes.
    /// </summary>
    internal async Task RunAsync()
    {
        _tcp = new TcpClient { NoDelay = true };
        await _tcp.ConnectAsync(_ip, _port, _ct);
        _stream = _tcp.GetStream();

        // Flush messages that were sent before the connection was ready.
        while (_pendingSend.TryRead(out ReadOnlyMemory<byte> queued))
        {
            await WriteFramedAsync(queued);
        }

        // Receive loop and drain of any further sends run concurrently.
        await Task.WhenAll(ReceiveLoopAsync(), SendLoopAsync());

        _tcp?.Close();
        _ = _inbox.TryComplete();
    }

    private async Task ReceiveLoopAsync()
    {
        byte[] lenBuf = new byte[4];
        while (!_ct.IsCancellationRequested)
        {
            await _stream!.ReadExactlyAsync(lenBuf, _ct);
            int length = BinaryPrimitives.ReadInt32BigEndian(lenBuf);

            byte[] payload = new byte[length];
            await _stream.ReadExactlyAsync(payload, _ct);
            await _inbox.WriteAsync(payload, _ct);
        }
    }

    private async Task SendLoopAsync()
    {
        await foreach (ReadOnlyMemory<byte> payload in _pendingSend.ReadAllAsync(_ct))
        {
            await WriteFramedAsync(payload);
        }
    }

    public override async void SendMessage(ReadOnlyMemory<byte> payload, MyNetDeliveryMethod method, int sequenceChannel = 0)
    {
        // Called from the game loop. Fire-and-forget via the send channel
        // so the game loop is never blocked on I/O.
        if (_stream is not null)
        {
            await WriteFramedAsync(payload);
        }
    }

    private async Task WriteFramedAsync(ReadOnlyMemory<byte> payload)
    {
        byte[] lenBuf = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(lenBuf, payload.Length);
        await _stream!.WriteAsync(lenBuf, _ct);
        await _stream.WriteAsync(payload, _ct);
    }
}