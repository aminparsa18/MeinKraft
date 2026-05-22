// abstract network layer
// All implementations (Dummy, TCP, WebSocket) implement these abstractions.
// Game logic only ever touches these types; no implementation details leak upward.

/// <summary>
/// A received message from the network. Carries the raw payload and metadata.
/// </summary>

/// <summary>
/// Reliability/ordering mode for outgoing messages. Mirrors Lidgren's delivery
/// method enum so the real implementation can map directly.
/// </summary>

/// <summary>
/// Type of a received message. Implementations synthesize Connect/Disconnect
/// events so callers can handle connection lifecycle uniformly.
/// </summary>

/// <summary>
/// Represents one end of an established connection. Used by the server side
/// to send messages back to a specific client.
/// </summary>
public abstract class NetConnection
{
    public abstract IpEndpoint RemoteEndPoint();

    /// <summary>
    /// Sends a payload to this specific connected peer.
    /// </summary>
    public abstract void SendMessage(ReadOnlyMemory<byte> payload, MyNetDeliveryMethod method, int sequenceChannel = 0);

    /// <summary>
    /// Called once per game tick. Implementations can use this for keep-alive,
    /// timeout checking, or any per-tick housekeeping.
    /// </summary>
    public abstract void Update();

    public abstract bool EqualsConnection(NetConnection other);
}
