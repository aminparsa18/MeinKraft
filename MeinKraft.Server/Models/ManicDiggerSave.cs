using MeinKraft;
using MemoryPack;

/// <summary>
/// The top-level save file structure for a MeinKraft world.
/// Persisted to disk when the server saves and loaded on world startup.
/// </summary>
[MemoryPackable]
public partial class MeinKraftSave
{
    // ── World dimensions ──────────────────────────────────────────────────────

    /// <summary>Map width in blocks along the X axis.</summary>
    public int MapSizeX { get; set; }

    /// <summary>Map depth in blocks along the Y axis.</summary>
    public int MapSizeY { get; set; }

    /// <summary>Map height in blocks along the Z axis.</summary>
    public int MapSizeZ { get; set; }

    // ── World generation ──────────────────────────────────────────────────────

    /// <summary>Random seed used to generate this world's terrain.</summary>
    public int Seed { get; set; }

    // ── Simulation state ──────────────────────────────────────────────────────

    /// <summary>
    /// Current simulation frame counter. Used for timestamping chunk changes,
    /// scheduling events, and determining entity ages.
    /// </summary>
    public long SimulationCurrentFrame { get; set; }

    /// <summary>
    /// Current in-game time of day, expressed as a simulation tick offset.
    /// </summary>
    public long TimeOfDay { get; set; }

    /// <summary>
    /// Highest monster entity ID issued so far. Used to generate unique IDs
    /// for newly spawned monsters without collision.
    /// </summary>
    public int LastMonsterId { get; set; }

    // ── Per-player data ───────────────────────────────────────────────────────

    /// <summary>
    /// Inventory snapshots keyed by player username.
    /// </summary>
    public Dictionary<string, Inventory>? Inventory { get; set; }

    /// <summary>
    /// Health and oxygen snapshots keyed by player username.
    /// </summary>
    public Dictionary<string, PacketServerPlayerStats>? PlayerStats { get; set; }

    // ── Mod data ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Arbitrary binary data blobs keyed by mod identifier.
    /// Allows server-side mods to persist custom state alongside the world save.
    /// </summary>
    public Dictionary<string, byte[]>? ModData { get; set; }
}
