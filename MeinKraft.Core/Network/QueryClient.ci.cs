// queries a server for its info (name, player count, etc.)
// without joining. Used by the server browser.
//
// The query is async with a real timeout — no busy-wait spin loop.

using MemoryPack;

public sealed class QueryClient
{
    private readonly INetworkService _platform;

    public QueryClient(INetworkService platform)
    {
        _platform = platform;
    }

    /// <summary>
    /// Queries the server at the given address.
    /// Returns the result on success, or null on timeout or rejection.
    /// <paramref name="serverMessage"/> carries a human-readable status on failure.
    /// </summary>
    public async Task<(QueryResult? result, string serverMessage)> QueryAsync(
        string ip, int port, TimeSpan timeout = default)
    {
        if (timeout == default)
        {
            timeout = TimeSpan.FromSeconds(2);
        }

        NetClient client = CreateClient();
        client.Start();
        client.Connect(ip, port);
        SendRequest(client);

        return await ReadResponseAsync(client, timeout);
    }

    private NetClient CreateClient()
    {
        if (_platform.EnetAvailable())
        {
            return new EnetNetClient(_platform);
        }

        return new TcpNetClient();
    }

    private static void SendRequest(NetClient client)
    {
        var packet = MemoryPackSerializer.Serialize(ClientPackets.ServerQuery());
        client.SendMessage(packet.AsMemory(0, packet.Length), MyNetDeliveryMethod.ReliableOrdered);
    }

    private static async Task<(QueryResult? result, string serverMessage)> ReadResponseAsync(
        NetClient client, TimeSpan timeout)
    {
        using CancellationTokenSource cts = new(timeout);

        while (!cts.IsCancellationRequested)
        {
            NetIncomingMessage? msg = client.ReadMessage();

            if (msg == null)
            {
                await Task.Delay(10, cts.Token);
                continue;
            }

            Packet_Server packet = MemoryPackSerializer.Deserialize<Packet_Server>(msg.Payload.Span);
            switch (packet.Id)
            {
                case Packet_ServerIdEnum.QueryAnswer:
                    return (QueryResult.FromPacket(packet.QueryAnswer), "");

                case Packet_ServerIdEnum.DisconnectPlayer:
                    return (null, packet.DisconnectPlayer.DisconnectReason);
            }
            // All other packets are silently dropped — not relevant to a query.
        }

        return (null, "Timeout while querying server.");
    }
}

public sealed class QueryResult
{
    public string Name { get; init; } = "";
    public string Motd { get; init; } = "";
    public int PlayerCount { get; init; }
    public int MaxPlayers { get; init; }
    public string PlayerList { get; init; } = "";
    public int Port { get; init; }
    public string GameMode { get; init; } = "";
    public bool Password { get; init; }
    public string PublicHash { get; init; } = "";
    public string ServerVersion { get; init; } = "";
    public int MapSizeX { get; init; }
    public int MapSizeY { get; init; }
    public int MapSizeZ { get; init; }
    public byte[] ServerThumbnail { get; init; } = [];

    internal static QueryResult FromPacket(Packet_ServerQueryAnswer a) => new()
    {
        Name = a.Name,
        Motd = a.MOTD,
        PlayerCount = a.PlayerCount,
        MaxPlayers = a.MaxPlayers,
        PlayerList = a.PlayerList,
        Port = a.Port,
        GameMode = a.GameMode,
        Password = a.Password,
        PublicHash = a.PublicHash,
        ServerVersion = a.ServerVersion,
        MapSizeX = a.MapSizeX,
        MapSizeY = a.MapSizeY,
        MapSizeZ = a.MapSizeZ,
        ServerThumbnail = a.ServerThumbnail,
    };
}