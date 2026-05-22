using OpenTK.Mathematics;
// ── Supporting types ──────────────────────────────────────────────────────────

/// <summary>
/// Contract for <see cref="SaveGameService"/>.
/// </summary>
public interface ISaveGameService
{
    public int Seed { get; set; }
    public long SimulationCurrentFrame { get; set; }
    Dictionary<string, byte[]> ModData { get; set; }

    /// <summary>
    /// Creates a backup of the current database at the given name inside the
    /// backup folder. Returns <c>false</c> and logs a warning if the name fails
    /// validation.
    /// </summary>
    bool BackupDatabase(string backupFilename);

    /// <summary>
    /// Serialises the given world-space chunk positions and writes them to a
    /// named file in the backup folder. Skips positions outside the map bounds.
    /// </summary>
    void SaveChunksToDatabase(List<Vector3i> chunkPositions, string filename);

    /// <summary>
    /// Returns the raw block data for the chunk that contains world-space
    /// position (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>),
    /// or <c>null</c> if the position is outside the map.
    /// </summary>
    ushort[] GetChunk(int x, int y, int z);

    /// <summary>
    /// Saves every loaded chunk unconditionally (not just dirty ones), then
    /// persists global data. Use for world-switch or clean shutdown scenarios.
    /// </summary>
    void SaveAll();

    /// <summary>
    /// Serialises and persists a single chunk to the chunk database immediately.
    /// </summary>
    void DoSaveChunk(int x, int y, int z, ServerChunk chunk);

    /// <summary>
    /// Saves the current world, optionally switches to a different save file,
    /// clears transient chunk state, reloads world data, and resets per-client
    /// chunk visibility so every connected client receives a fresh level stream.
    /// </summary>
    bool LoadDatabase(string filename);

    /// <summary>
    /// Serialises current world state and flushes dirty chunks to the database.
    /// Returns the raw serialised global-data blob written to the database.
    /// </summary>
    byte[] Save();

    /// <summary>
    /// Convenience wrapper — serialises world state and immediately persists it
    /// to the chunk database as global data. Equivalent to
    /// <c>chunkDb.SetGlobalData(Save())</c>.
    /// </summary>
    void SaveGlobalData();

    /// <summary>
    /// Opens the database, reads global state, and restores server fields.
    /// Creates a fresh save if the database contains no global data yet.
    /// </summary>
    void Load();

    // might be removed later, but for now it's a convenient way to get the last monster id from the save file
    int LastMonsterId { get; set; }
}
