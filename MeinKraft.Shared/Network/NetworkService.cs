using ENet;

public class NetworkService : INetworkService
{
    private readonly IGameLogger _gameLogger;

    public NetworkService(IGameLogger gameLogger)
    {
        _gameLogger = gameLogger;
    }

    public bool EnetAvailable() => true;

    public EnetHost EnetCreateHost() => new EnetHostWrapper(new Host());

    public EnetEvent? EnetHostCheckEvents(EnetHost host)
    {
        int ret = ((EnetHostWrapper)host).Host.CheckEvents(out Event e);
        return ret > 0 ? new EnetEventWrapper(e) : null;
    }

    public EnetPeer EnetHostConnect(EnetHost host, string hostName, int port, int channelCount, int data)
    {
        Address address = new() { Port = (ushort)port };
        _ = address.SetHost(hostName);
        Peer peer = ((EnetHostWrapper)host).Host.Connect(address, channelCount, (uint)data);
        return new EnetPeerWrapper(peer);
    }

    public void EnetHostInitialize(EnetHost host, IpEndpoint? address, int peerLimit, int channelLimit, int incomingBandwidth, int outgoingBandwidth)
    {
        ((EnetHostWrapper)host).Host.Create(
            null,
            peerLimit,
            channelLimit,
            (uint)incomingBandwidth,
            (uint)outgoingBandwidth,
            4 * 1024 * 1024);
    }

    public void EnetHostInitializeServer(EnetHost host, int port, int peerLimit)
    {
        Address address = new() { Port = (ushort)port };
        ((EnetHostWrapper)host).Host.Create(
            address,
            peerLimit,
            0,
            0u,
            0u,
            4 * 1024 * 1024);
    }

    public EnetEvent? EnetHostService(EnetHost host, int timeout)
    {
        if (_unflushledPackets > 0)
        {
        //    _gameLogger.Client.Debug($"[ENET-SERVICE] flushing — {_unflushledPackets} packets were queued since last service");
            _unflushledPackets = 0;
        }

        int ret = ((EnetHostWrapper)host).Host.Service(timeout, out Event e);
        return ret > 0 ? new EnetEventWrapper(e) : null;
    }

    private int _unflushledPackets;

    public void EnetPeerSend(EnetPeer peer, int channelId, ReadOnlyMemory<byte> payload, int flags)
    {
        ENet.Packet packet = default;
        packet.Create(payload.ToArray(), payload.Length, (PacketFlags)flags);
        _ = ((EnetPeerWrapper)peer).Peer.Send((byte)channelId, ref packet);

        _unflushledPackets++;
       // _gameLogger.Client.Debug($"[ENET-SEND] queued packet #{_unflushledPackets} unflushed");
    }

    public bool TcpAvailable() => true;

    public bool WebSocketAvailable() => true;

    public void WebSocketConnect(string ip, int port)
    {
    }

    public int WebSocketReceive(byte[] data, int dataLength) => -1;

    public void WebSocketSend(byte[] data, int dataLength)
    {
    }
}

#region ENet

// ---------------------------------------------------------------------------
// Native wrappers — thin shells that satisfy our abstract types.
// All live in the platform assembly, not in game logic.
// ---------------------------------------------------------------------------

/// <summary>Wraps ENet-CSharp's Host struct.</summary>
internal sealed class EnetHostWrapper : EnetHost
{
    internal readonly Host Host;
    internal EnetHostWrapper(Host host)
    {
        Host = host;
    }
}

/// <summary>Wraps ENet-CSharp's Peer struct.</summary>
internal sealed class EnetPeerWrapper : EnetPeer
{
    internal Peer Peer; // Not readonly — Peer is a struct, SetUserData must mutate it in place
    internal EnetPeerWrapper(Peer peer)
    {
        Peer = peer;
    }

    public override int UserData() => (int)Peer.Data;
    public override void SetUserData(int value) => Peer.Data = value;
    public override IpEndpoint GetRemoteAddress()
        => IpEndpointDefault.Create(Peer.IP);
}

/// <summary>
/// Wraps ENet-CSharp's Event struct.
/// Only allocated when an event actually occurred (ret > 0 from Service/CheckEvents).
/// </summary>
internal sealed class EnetEventWrapper : EnetEvent
{
    private readonly Event _e;
    internal EnetEventWrapper(Event e)
    {
        _e = e;
    }

    public override EnetEventType Type() => _e.Type switch
    {
        EventType.Connect => EnetEventType.Connect,
        EventType.Disconnect => EnetEventType.Disconnect,
        EventType.Receive => EnetEventType.Receive,
        EventType.Timeout => EnetEventType.Disconnect, // treat timeout as disconnect
        _ => EnetEventType.None,
    };

    public override EnetPeer Peer() => new EnetPeerWrapper(_e.Peer);

    public override EnetPacket Packet() => new EnetPacketWrapper(_e.Packet);
}

/// <summary>Wraps ENet-CSharp's Packet struct.</summary>
internal sealed class EnetPacketWrapper : EnetPacket
{
    private readonly ENet.Packet _p;
    internal EnetPacketWrapper(ENet.Packet p)
    {
        _p = p;
    }

    public override int GetBytesCount() => _p.Length;
    public override byte[] GetBytes()
    {
        byte[] buffer = new byte[_p.Length];
        _p.CopyTo(buffer);
        return buffer;
    }
    public override void Dispose() => _p.Dispose();
}
#endregion
