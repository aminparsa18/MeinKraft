namespace MeinKraft;

/// <summary>
/// Contract for managing player health and oxygen statistics, and notifying
/// clients when those stats change.
/// </summary>
public interface IPlayerStatusService
{
    /// <summary>
    /// All known player stats keyed by player name.
    /// A default entry is created on first access via <see cref="GetPlayerStats"/>.
    /// </summary>
    Dictionary<string, PacketServerPlayerStats> PlayerStats { get; }

    /// <summary>
    /// Returns the stats for the given player, creating a default entry if one
    /// does not yet exist.
    /// </summary>
    /// <param name="playername">The player's username.</param>
    PacketServerPlayerStats GetPlayerStats(string playername);

    /// <summary>
    /// Sends an updated <see cref="PacketServerPlayerStats"/> packet to the client
    /// if their stats have been marked dirty. Does nothing if the player name is
    /// not yet assigned or the stats are clean.
    /// </summary>
    /// <param name="clientid">The numeric client ID on the server.</param>
    void NotifyPlayerStats(int clientid);
}
