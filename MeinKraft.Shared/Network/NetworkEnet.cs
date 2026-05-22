// ENet implementation of the abstract network layer.
//
// ENet is a reliable UDP library. The platform abstraction owns the actual
// ENet host/peer handles; this layer translates ENet events into the
// NetIncomingMessage model the rest of the game uses.

// ---------------------------------------------------------------------------
// ENet primitives (platform-provided abstractions, unchanged except cleanup)
// ---------------------------------------------------------------------------

public class EnetHost { }

public abstract class EnetEvent
{
    public abstract EnetEventType Type();
    public abstract EnetPeer Peer();
    public abstract EnetPacket Packet();
}

public enum EnetEventType
{
    None,
    Connect,
    Disconnect,
    Receive,
}

/// <summary>
/// ENet packet flag constants. Kept as named constants rather than an enum
/// because ENet itself treats these as a bitmask passed to its C API.
/// </summary>
public static class EnetPacketFlags
{
    public const int None = 0;
    public const int Reliable = 1;
    public const int Unsequenced = 2;
    public const int NoAllocate = 4;
    public const int UnreliableFragment = 8;
}

public abstract class EnetPeer
{
    public abstract int UserData();
    public abstract void SetUserData(int value);
    public abstract IpEndpoint GetRemoteAddress();
}

public abstract class EnetPacket
{
    public abstract int GetBytesCount();
    public abstract byte[] GetBytes();
    public abstract void Dispose();
}

// ---------------------------------------------------------------------------
// Connection handle
// ---------------------------------------------------------------------------

public sealed class EnetNetConnection : NetConnection
{
    private readonly INetworkService _platform;
    public EnetPeer Peer { get; private set; }

    public EnetNetConnection(INetworkService platform, EnetPeer peer)
    {
        _platform = platform;
        Peer = peer;
    }

    public override IpEndpoint RemoteEndPoint()
        => IpEndpointDefault.Create(Peer.GetRemoteAddress().AddressToString());

    public override void SendMessage(ReadOnlyMemory<byte> payload, MyNetDeliveryMethod method, int sequenceChannel = 0)
    {
        int flags = ToEnetFlags(method);
        _platform.EnetPeerSend(Peer, sequenceChannel, payload, flags);
    }

    public override void Update() { }

    public override bool EqualsConnection(NetConnection other)
        => other is EnetNetConnection e && e.Peer.UserData() == Peer.UserData();

    // CastToEnetNetConnection on IGamePlatform is no longer needed —
    // callers can just cast directly or use pattern matching as above.

    private static int ToEnetFlags(MyNetDeliveryMethod method) => method switch
    {
        MyNetDeliveryMethod.ReliableOrdered
            or MyNetDeliveryMethod.ReliableSequenced
            or MyNetDeliveryMethod.ReliableUnordered => EnetPacketFlags.Reliable,
        MyNetDeliveryMethod.UnreliableSequenced => EnetPacketFlags.Unsequenced,
        _ => EnetPacketFlags.None,
    };
}