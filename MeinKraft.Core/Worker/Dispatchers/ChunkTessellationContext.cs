namespace MeinKraft.Worker;

/// <summary>
/// Holds all per-worker mutable state required to tessellate one chunk.
/// Instantiated once per worker thread via <see cref="ThreadLocal{T}"/> in
/// <see cref="ChunkTessellationDispatcher"/> — no sharing, no locks.
/// </summary>
public sealed class ChunkTessellationContext
{
    private const int ChunkSize = 16;
    private const int BufferedChunkEdge = 18;
    private const int BufferedChunkVolume = BufferedChunkEdge * BufferedChunkEdge * BufferedChunkEdge;

    // ── Existing per-worker buffers (ModDrawTerrain) ──────────────────────────
    public readonly int[] CurrentChunk = new int[BufferedChunkVolume];
    public readonly byte[] CurrentChunkShadows = new byte[BufferedChunkVolume];
    public readonly int[] ShadowLightRadius = new int[GameConstants.MAX_BLOCKTYPES];
    public readonly bool[] ShadowIsTransparent = new bool[GameConstants.MAX_BLOCKTYPES];
    public bool BlockTypeCacheDirty = true;

    // ── Tessellator pass 1 output ─────────────────────────────────────────────
    /// <summary>Per-block TileSideFlags bitmask written by CalculateVisibleFaces.</summary>
    public readonly byte[] ChunkDraw16 = new byte[ChunkSize * ChunkSize * ChunkSize];

    // ── Tessellator pass 2 output ─────────────────────────────────────────────
    /// <summary>
    /// Per-block-per-side draw flag written by CalculateTilingCount.
    /// Index = blockIndex * 6 + sideIndex.
    /// </summary>
    public readonly byte[] ChunkDrawCount16Flat = new byte[ChunkSize * ChunkSize * ChunkSize * 6];

    // ── Tessellator pass 3 output (geometry) ──────────────────────────────────
    public GeometryModel[] Atlas;        // opaque buckets, one per atlas page
    public GeometryModel[] AtlasTransparent;
    public VerticesIndicesToLoad[] ReturnBuffer;

    // ── Per-block scratch (BuildSingleBlockPolygon) ───────────────────────────
    public CornerHeights CornerHeights;     // value-type — cleared each block
    public int TorchTopTexture;             // set in BuildSingleBlockPolygon,
    public int TorchSideTexture;            // consumed by AddTorch

    // ── Per-face scratch (BuildBlockFace / CalcShadowRation) ─────────────────
    public readonly int[] TmpNPos = new int[7];
    public readonly int[] TmpShadowRation = new int[(int)TileDirection.Count];
    public readonly bool[] TmpOccupied = new bool[(int)TileDirection.Count];
    public readonly float[] TmpFShadowRation = new float[4];

    /// <param name="atlasCount">
    /// <c>Math.Max(1, MAX_BLOCKTYPES / TerrainTexturesPerAtlas)</c> —
    /// must match the value computed in <see cref="TerrainChunkTesselator.Start"/>.
    /// </param>
    public ChunkTessellationContext(int atlasCount)
    {
        Atlas = new GeometryModel[atlasCount];
        AtlasTransparent = new GeometryModel[atlasCount];
        ReturnBuffer = new VerticesIndicesToLoad[atlasCount * 2];

        for (int i = 0; i < atlasCount; i++)
        {
            Atlas[i] = AllocModel(1024);
            AtlasTransparent[i] = AllocModel(1024);
        }
    }

    private static GeometryModel AllocModel(int capacity) => new()
    {
        Xyz = new float[capacity * 3],
        Uv = new float[capacity * 2],
        Rgba = new byte[capacity * 4],
        Indices = new int[capacity],
    };

    /// <summary>Resets geometry counters before each chunk build. O(atlasCount).</summary>
    public void ResetGeometry()
    {
        foreach (GeometryModel m in Atlas)
        { m.VerticesCount = 0; m.IndicesCount = 0; }
        foreach (GeometryModel m in AtlasTransparent)
        { m.VerticesCount = 0; m.IndicesCount = 0; }
    }
}