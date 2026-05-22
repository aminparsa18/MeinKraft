using MemoryPack;

namespace MeinKraft;

/// <summary>
/// Server → client packet carrying the player's current health and oxygen values.
/// Sent periodically and whenever either stat changes.
/// </summary>
[MemoryPackable]
public partial class PacketServerPlayerStats
{
    /// <summary>Player's current health points. Defaults to <c>20</c>.</summary>
    public int CurrentHealth { get; set; } = 20;

    /// <summary>Player's maximum health points. Defaults to <c>20</c>.</summary>
    public int MaxHealth { get; set; } = 20;

    /// <summary>
    /// Player's current oxygen level. Depletes while the player is underwater.
    /// Defaults to <c>10</c>.
    /// </summary>
    public int CurrentOxygen { get; set; } = 10;

    /// <summary>Player's maximum oxygen capacity. Defaults to <c>10</c>.</summary>
    public int MaxOxygen { get; set; } = 10;
}