namespace MeinKraft;

/// <summary>
/// Manages per-player health and oxygen stats and flushes changes to clients
/// via <see cref="IServerPacketService"/>.
/// </summary>
public class PlayerStatusService : IPlayerStatusService
{
    private readonly IClientRegistry _serverClientService;
    private readonly IServerPacketService _serverPacketService;

    /// <param name="serverClientService">Provides access to connected client state.</param>
    /// <param name="serverPacketService">Used to send stat update packets to clients.</param>
    public PlayerStatusService(IClientRegistry serverClientService, IServerPacketService serverPacketService)
    {
        _serverClientService = serverClientService;
        _serverPacketService = serverPacketService;
    }

    /// <inheritdoc/>
    public Dictionary<string, PacketServerPlayerStats> PlayerStats { get; } = [];

    /// <inheritdoc/>
    public PacketServerPlayerStats GetPlayerStats(string playername)
    {
        if (!PlayerStats.TryGetValue(playername, out PacketServerPlayerStats? value))
        {
            value = CreateDefaultStats();
            PlayerStats[playername] = value;
        }

        return value;
    }

    /// <inheritdoc/>
    public void NotifyPlayerStats(int clientid)
    {
        ServerPlayer c = _serverClientService.Clients[clientid];
        if (c.IsPlayerStatsDirty && c.PlayerName != GameConstants.InvalidPlayerName)
        {
            PacketServerPlayerStats stats = GetPlayerStats(c.PlayerName);
            _serverPacketService.SendPacket(clientid,
                ServerPackets.PlayerStats(
                    stats.CurrentHealth,
                    stats.MaxHealth,
                    stats.CurrentOxygen,
                    stats.MaxOxygen));
            c.IsPlayerStatsDirty = false;
        }
    }

    /// <summary>
    /// Creates a <see cref="PacketServerPlayerStats"/> populated with the default
    /// starting values for a new player (20 HP, 10 oxygen).
    /// </summary>
    private static PacketServerPlayerStats CreateDefaultStats() => new()
    {
        CurrentHealth = 20,
        MaxHealth = 20,
        CurrentOxygen = 10,
        MaxOxygen = 10,
    };
}