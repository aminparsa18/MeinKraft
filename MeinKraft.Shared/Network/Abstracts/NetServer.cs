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

// ---------------------------------------------------------------------------
// Abstract server / client / connection
// ---------------------------------------------------------------------------

/// <summary>
/// Listens for incoming connections and reads messages from all connected clients.
/// </summary>
public abstract class NetServer
{
    public abstract void SetPort(int port);
    public abstract void Start();

    /// <summary>
    /// Returns the next pending message, or null if none is available.
    /// May return Connect/Disconnect lifecycle messages as well as Data messages.
    /// </summary>
    public abstract NetIncomingMessage? ReadMessage();
}
