using MeinKraft;
using MemoryPack;

/// <summary>
/// A 32³ (or 16³) block volume stored as a flat <see cref="ushort"/> array,
/// along with any monsters and entities that currently occupy it.
/// Persisted to the chunk database and loaded on demand.
/// </summary>
[MemoryPackable]
public partial class ServerChunk
{
    /// <summary>
    /// Legacy block data stored as <see langword="byte[]"/> from older save formats.
    /// When non-null on load, its contents are migrated into <see cref="Data"/>
    /// and this field is cleared.
    /// </summary>
    public byte[]? DataOld { get; set; }

    /// <summary>
    /// Block type IDs for every position in the chunk, stored in XYZ order.
    /// Index = <c>x + y * chunksize + z * chunksize * chunksize</c>.
    /// </summary>
    public ushort[]? Data { get; set; }

    /// <summary>Simulation frame on which this chunk was last modified by the world generator or a player.</summary>
    public long LastUpdate { get; set; }

    /// <summary>When <see langword="true"/>, this chunk has been fully generated and populated with terrain.</summary>
    public bool IsPopulated { get; set; }

    /// <summary>Simulation frame of the most recent block change within this chunk.</summary>
    public int LastChange { get; set; }

    /// <summary>
    /// When <see langword="true"/>, this chunk has unsaved changes and must be
    /// written to the database on the next save pass.
    /// Not persisted — always resets to <see langword="false"/> on load.
    /// </summary>
    [MemoryPackIgnore]
    public bool DirtyForSaving { get; set; }

    /// <summary>Monsters currently residing in this chunk.</summary>
    public List<Monster> Monsters { get; set; } = [];

    /// <summary>Server entities (signs, push zones, interactive objects) located in this chunk.</summary>
    public Dictionary<int, ServerEntity>? Entities { get; set; }
}
