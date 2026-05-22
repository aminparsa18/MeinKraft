namespace MeinKraft.Worker;

// ── Per-thread context ────────────────────────────────────────────────────────

/// <summary>
/// All mutable state needed by one lighting worker thread.
/// Allocated once per thread via ThreadLocal — no sharing, no locks.
/// </summary>
public sealed class LightingThreadContext
{
    public readonly LightBase LightBase;
    public readonly LightBetweenChunks LightBetweenChunks;
    public readonly int[] ShadowLightRadius = new int[GameConstants.MAX_BLOCKTYPES];
    public readonly bool[] ShadowIsTransparent = new bool[GameConstants.MAX_BLOCKTYPES];

    /// <summary>
    /// Holds the 27 rented BaseLight snapshot buffers for the current job.
    /// Entries are rented at the start of HandleFullRelight and returned to the
    /// pool in SnapshotAndEnqueue after LightBetweenChunks has consumed them.
    /// The array itself is reused across jobs to avoid per-job allocation.
    /// </summary>
    public readonly byte[][] BaseLightSnapshots = new byte[27][];

    private int _knownCacheVersion = -1;

    public LightingThreadContext(IVoxelMap voxelMap)
    {
        LightBase = new LightBase(voxelMap);
        LightBetweenChunks = new LightBetweenChunks(voxelMap);
    }

    /// <summary>
    /// Rebuilds the block-type lookup arrays if the global cache version has
    /// advanced since this thread last refreshed.
    /// </summary>
    public void RefreshCacheIfNeeded(IBlockRegistry blockRegistry, int globalVersion)
    {
        if (_knownCacheVersion == globalVersion) return;

        foreach ((int id, BlockType blockType) in blockRegistry.BlockTypes)
        {
            ShadowLightRadius[id] = blockType.LightRadius;
            ShadowIsTransparent[id] = blockType.DrawType
                is not DrawType.Solid
                and not DrawType.ClosedDoor;
        }

        _knownCacheVersion = globalVersion;
    }
}