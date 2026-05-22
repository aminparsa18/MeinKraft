
// ─────────────────────────────────────────────────────────────────────────────
// Networking (TCP / ENet / WebSocket)
// ─────────────────────────────────────────────────────────────────────────────

public interface INetworkService
{
    bool TcpAvailable();
    // ENet
    bool EnetAvailable();
    EnetHost EnetCreateHost();
    void EnetHostInitialize(EnetHost host, IpEndpoint? address, int peerLimit, int channelLimit, int incomingBandwidth, int outgoingBandwidth);
    void EnetHostInitializeServer(EnetHost host, int port, int peerLimit);
    EnetEvent? EnetHostService(EnetHost host, int timeout);
    EnetEvent? EnetHostCheckEvents(EnetHost host);
    EnetPeer EnetHostConnect(EnetHost host, string hostName, int port, int channelCount, int data);
    void EnetPeerSend(EnetPeer peer, int channelId, ReadOnlyMemory<byte> payload, int flags);

    // WebSocket
    bool WebSocketAvailable();
    void WebSocketConnect(string ip, int port);
    void WebSocketSend(byte[] data, int dataLength);
    int WebSocketReceive(byte[] data, int dataLength);
}

