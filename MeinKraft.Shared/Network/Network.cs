// abstract network layer
// All implementations (Dummy, TCP, WebSocket) implement these abstractions.
// Game logic only ever touches these types; no implementation details leak upward.

/// <summary>
/// A received message from the network. Carries the raw payload and metadata.
/// </summary>
public class NetIncomingMessage
{
    public required NetConnection SenderConnection { get; init; }
    public NetworkMessageType Type { get; init; } = NetworkMessageType.Data;
    public ReadOnlyMemory<byte> Payload { get; init; }
}

/// <summary>
/// Reliability/ordering mode for outgoing messages. Mirrors Lidgren's delivery
/// method enum so the real implementation can map directly.
/// </summary>
public enum MyNetDeliveryMethod
{
    Unreliable,
    UnreliableSequenced,
    ReliableUnordered,
    ReliableSequenced,
    ReliableOrdered,
}

/// <summary>
/// Type of a received message. Implementations synthesize Connect/Disconnect
/// events so callers can handle connection lifecycle uniformly.
/// </summary>
public enum NetworkMessageType
{
    Data,
    Connect,
    Disconnect,
}

public class IpEndpointDefault : IpEndpoint
{
    private readonly string _address;

    private IpEndpointDefault(string address)
    {
        _address = address;
    }

    public static IpEndpointDefault Create(string address) => new(address);

    public override string AddressToString() => _address;
}