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
/// Connects to a server and exchanges messages with it.
/// </summary>
public abstract class NetClient
{
    public abstract void Start();

    /// <summary>
    /// Initiates a connection. Returns the connection handle immediately;
    /// the connection may not be fully established until a Connect message
    /// is received from <see cref="ReadMessage"/>.
    /// </summary>
    public abstract NetConnection Connect(string ip, int port);

    /// <summary>
    /// Returns the next pending message from the server, or null if none.
    /// </summary>
    public abstract NetIncomingMessage? ReadMessage();

    /// <summary>
    /// Sends a payload to the server with the specified delivery guarantee.
    /// </summary>
    public abstract void SendMessage(ReadOnlyMemory<byte> payload, MyNetDeliveryMethod method);
}
