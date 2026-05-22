// TCP Server implementation of the abstract network layer.
//
// Framing protocol: every message is prefixed with a 4-byte big-endian length.
//   [4 bytes length][N bytes payload]
//
// Receiving runs on a dedicated async loop started at connect time.
// The game loop calls ReadMessage() which drains an in-memory queue — no I/O
// on the calling thread, no sends hidden inside reads.

using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

public sealed class TcpNetServer : NetServer
{
    private readonly Channel<NetIncomingMessage> _inbox =
        Channel.CreateUnbounded<NetIncomingMessage>(
            new UnboundedChannelOptions { SingleReader = true });

    // Connections keyed by remote address — same pattern as EnetNetServer,
    // so Connect/Data/Disconnect events always return the same object for a peer.
    private readonly Dictionary<string, TcpServerConnection> _connections = new();
    private readonly object _connectionsLock = new();

    private TcpListener? _listener;
    private int _port;
    private CancellationTokenSource? _cts;

    public override void SetPort(int port) => _port = port;

    public override void Start()
    {
        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Any, _port);
        _listener.Server.NoDelay = true;
        _listener.Start();
        _ = AcceptLoopAsync(_cts.Token);
    }

    public override NetIncomingMessage? ReadMessage()
    {
        _inbox.Reader.TryRead(out NetIncomingMessage? msg);
        return msg;
    }

    public void Stop() => _cts?.Cancel();

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                TcpClient tcp = await _listener!.AcceptTcpClientAsync(ct);
                tcp.NoDelay = true;
                _ = HandleClientAsync(tcp, ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
        }
        finally
        {
            _listener?.Stop();
        }
    }

    private async Task HandleClientAsync(TcpClient tcp, CancellationToken ct)
    {
        string address = tcp.Client.RemoteEndPoint?.ToString() ?? "unknown";
        TcpServerConnection conn = new(address);

        lock (_connectionsLock)
        {
            _connections[address] = conn;
        }

        await _inbox.Writer.WriteAsync(new NetIncomingMessage
        {
            Type = NetworkMessageType.Connect,
            SenderConnection = conn,
        }, ct);

        try
        {
            NetworkStream stream = tcp.GetStream();
            byte[] lenBuf = new byte[4];

            _ = conn.SendLoopAsync(stream, ct);

            while (!ct.IsCancellationRequested)
            {
                await stream.ReadExactlyAsync(lenBuf, ct);
                int length = BinaryPrimitives.ReadInt32BigEndian(lenBuf);

                byte[] payload = new byte[length];
                await stream.ReadExactlyAsync(payload, ct);

                await _inbox.Writer.WriteAsync(new NetIncomingMessage
                {
                    Type = NetworkMessageType.Data,
                    Payload = payload,
                    SenderConnection = conn,
                }, ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
        }
        finally
        {
            tcp.Close();
            lock (_connectionsLock)
            {
                _connections.Remove(address);
            }

            // Always deliver disconnect even if ct is cancelled.
            await _inbox.Writer.WriteAsync(new NetIncomingMessage
            {
                Type = NetworkMessageType.Disconnect,
                SenderConnection = conn,
            }, CancellationToken.None);
        }
    }
}

// ---------------------------------------------------------------------------
// Server-side connection handle (one per connected client)
// ---------------------------------------------------------------------------

public sealed class TcpServerConnection : NetConnection
{
    private readonly string _address;
    private readonly Channel<ReadOnlyMemory<byte>> _sendQueue =
        Channel.CreateUnbounded<ReadOnlyMemory<byte>>();

    internal TcpServerConnection(string address)
    {
        _address = address;
    }

    public override IpEndpoint RemoteEndPoint()
        => IpEndpointDefault.Create(_address);

    public override void Update() { }

    public override bool EqualsConnection(NetConnection other)
        => other is TcpServerConnection t && t._address == _address;

    public override void SendMessage(ReadOnlyMemory<byte> payload, MyNetDeliveryMethod method, int sequenceChannel = 0) => _sendQueue.Writer.TryWrite(payload);

    internal async Task SendLoopAsync(NetworkStream stream, CancellationToken ct)
    {
        try
        {
            byte[] lenBuf = new byte[4];
            await foreach (ReadOnlyMemory<byte> payload in _sendQueue.Reader.ReadAllAsync(ct))
            {
                BinaryPrimitives.WriteInt32BigEndian(lenBuf, payload.Length);
                await stream.WriteAsync(lenBuf, ct);
                await stream.WriteAsync(payload, ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
        }
    }
}