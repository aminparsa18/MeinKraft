using MeinKraft;

/// <summary>
/// Calculates final lighting for a chunk by sampling BaseLight from the
/// 3×3×3 neighbourhood, flooding light across boundaries, then writing
/// Rendered.Light.
///
/// Key optimisation over previous version:
///   CopyFace helpers now record which destination positions received new
///   light. FloodBetweenChunks seeds the BFS only from those positions
///   instead of all 4096 lit positions. Chunks whose boundary cells did
///   not improve skip the BFS entirely.
///
///   Worst case seeds per chunk per pass: 6 faces × 16 × 16 = 1536.
///   Typical case: far fewer, because most boundary cells are already
///   at least as bright as the decayed source.
/// </summary>
public class LightBetweenChunks
{
    private const int NS = 3;
    private const int NVol = NS * NS * NS;
    private const int CS = 16;
    private const int CVol = CS * CS * CS;
    private const int Out = 18;

    // Max boundary cells per chunk: 6 faces × 16 × 16.
    private const int MaxSeeds = 6 * CS * CS;

    private readonly LightFlood _flood;
    private readonly byte[][] _chunksLight = new byte[NVol][];
    private readonly int[][] _chunksData = new int[NVol][];
    private readonly int[][] _seeds = new int[NVol][];
    private readonly int[] _seedCounts = new int[NVol];
    private readonly IVoxelMap _voxelMap;

    public LightBetweenChunks(IVoxelMap voxelMap)
    {
        _voxelMap = voxelMap;
        _flood = new LightFlood();
        for (int i = 0; i < NVol; i++)
        {
            _chunksLight[i] = new byte[CVol];
            _chunksData[i] = new int[CVol];
            _seeds[i] = new int[MaxSeeds];
        }
    }

    private static int Idx(int x, int y, int z) => (z * NS + y) * NS + x;
    private static int BlockIdx(int x, int y, int z) => z * 256 + y * 16 + x;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Full relight using live chunk data: Input → FloodBetweenChunks → Output.
    /// Only safe when called from a single lighting worker (workerCount=1).
    /// Prefer the snapshot overload when running multiple lighting workers.
    /// </summary>
    public void CalculateLightBetweenChunks(
        int cx, int cy, int cz,
        int[] dataLightRadius, bool[] dataTransparent)
    {
        InputFromLiveChunks(cx, cy, cz);
        FloodBetweenChunks(dataLightRadius, dataTransparent);
        Output(cx, cy, cz);
    }

    /// <summary>
    /// Full relight using pre-snapshotted BaseLight arrays: InputFromSnapshots →
    /// FloodBetweenChunks → Output.
    ///
    /// <paramref name="baseLightSnapshots"/> must be a 27-element array indexed by
    /// <c>(z * 3 + y) * 3 + x</c> (matching <see cref="Idx"/>), where each element
    /// is a rented <c>byte[CVol]</c> containing a point-in-time copy of the
    /// corresponding neighbour chunk's BaseLight taken after LightBase completed.
    /// Callers own the snapshot buffers and must return them to the pool after this
    /// call returns.
    /// </summary>
    public void CalculateLightBetweenChunks(
        int cx, int cy, int cz,
        int[] dataLightRadius, bool[] dataTransparent,
        byte[][] baseLightSnapshots)
    {
        InputFromSnapshots(cx, cy, cz, baseLightSnapshots);
        FloodBetweenChunks(dataLightRadius, dataTransparent);
        Output(cx, cy, cz);
    }

    /// <summary>
    /// Fast refresh: Input → Output only (no flood).
    /// Used after IncrementalLightBFS — cross-boundary propagation is already
    /// correct in BaseLight so the flood step is redundant.
    /// </summary>
    public void RefreshRenderedLight(int cx, int cy, int cz)
    {
        InputFromLiveChunks(cx, cy, cz);
        Output(cx, cy, cz);
    }

    // ── Implementation ────────────────────────────────────────────────────────

    /// <summary>
    /// Reads block data and BaseLight directly from the live chunk objects.
    /// Not safe to call concurrently with LightBase writes on the same chunks.
    /// Used only by the single-worker path and RefreshRenderedLight.
    /// </summary>
    private void InputFromLiveChunks(int cx, int cy, int cz)
    {
        for (int x = 0; x < NS; x++)
            for (int y = 0; y < NS; y++)
                for (int z = 0; z < NS; z++)
                {
                    int slot = Idx(x, y, z);
                    int pcx = cx + x - 1, pcy = cy + y - 1, pcz = cz + z - 1;

                    if (!_voxelMap.IsValidChunkPos(pcx, pcy, pcz))
                    {
                        Array.Clear(_chunksData[slot], 0, CVol);
                        Array.Clear(_chunksLight[slot], 0, CVol);
                        continue;
                    }

                    Chunk chunk = _voxelMap.GetChunkAt(pcx, pcy, pcz);
                    int[] data = _chunksData[slot];
                    byte[] light = _chunksLight[slot];

                    for (int i = 0; i < CVol; i++)
                        data[i] = chunk.GetBlock(i);

                    Array.Copy(chunk.BaseLight, light, CVol);

                }
    }

    /// <summary>
    /// Reads block data from live chunks but BaseLight from immutable snapshots,
    /// eliminating the read/write race with concurrent LightBase writers (Option B).
    /// <paramref name="baseLightSnapshots"/> is indexed by <see cref="Idx"/>.
    /// </summary>
    private void InputFromSnapshots(int cx, int cy, int cz, byte[][] baseLightSnapshots)
    {
        for (int x = 0; x < NS; x++)
            for (int y = 0; y < NS; y++)
                for (int z = 0; z < NS; z++)
                {
                    int slot = Idx(x, y, z);
                    int pcx = cx + x - 1, pcy = cy + y - 1, pcz = cz + z - 1;

                    if (!_voxelMap.IsValidChunkPos(pcx, pcy, pcz))
                    {
                        Array.Clear(_chunksData[slot], 0, CVol);
                        Array.Clear(_chunksLight[slot], 0, CVol);
                        continue;
                    }

                    Chunk chunk = _voxelMap.GetChunkAt(pcx, pcy, pcz);
                    int[] data = _chunksData[slot];

                    for (int i = 0; i < CVol; i++)
                        data[i] = chunk.GetBlock(i);

                    // Snapshot is immutable at this point — no race with LightBase.
                    Array.Copy(baseLightSnapshots[slot], _chunksLight[slot], CVol);
                }
    }

    /// <summary>
    /// Two passes. Each pass:
    ///   1. Copy improved light across all six face pairs, recording which
    ///      destination cells received new light.
    ///   2. For each chunk that received any new light, flood only from
    ///      those cells. Chunks with no new boundary light skip the BFS.
    /// </summary>
    private void FloodBetweenChunks(int[] dataLightRadius, bool[] dataTransparent)
    {
        for (int pass = 0; pass < 2; pass++)
        {
            // Clear seed counts for this pass.
            Array.Clear(_seedCounts, 0, NVol);

            // Copy improved values across boundaries, recording changed positions.
            for (int x = 0; x < NS; x++)
                for (int y = 0; y < NS; y++)
                    for (int z = 0; z < NS; z++)
                    {
                        byte[] src = _chunksLight[Idx(x, y, z)];

                        if (z < NS - 1) CopyFaceZ(src, _chunksLight[Idx(x, y, z + 1)], _seeds[Idx(x, y, z + 1)], ref _seedCounts[Idx(x, y, z + 1)], srcZ: 15, dstZ: 0);
                        if (z > 0) CopyFaceZ(src, _chunksLight[Idx(x, y, z - 1)], _seeds[Idx(x, y, z - 1)], ref _seedCounts[Idx(x, y, z - 1)], srcZ: 0, dstZ: 15);
                        if (x < NS - 1) CopyFaceX(src, _chunksLight[Idx(x + 1, y, z)], _seeds[Idx(x + 1, y, z)], ref _seedCounts[Idx(x + 1, y, z)], srcX: 15, dstX: 0);
                        if (x > 0) CopyFaceX(src, _chunksLight[Idx(x - 1, y, z)], _seeds[Idx(x - 1, y, z)], ref _seedCounts[Idx(x - 1, y, z)], srcX: 0, dstX: 15);
                        if (y < NS - 1) CopyFaceY(src, _chunksLight[Idx(x, y + 1, z)], _seeds[Idx(x, y + 1, z)], ref _seedCounts[Idx(x, y + 1, z)], srcY: 15, dstY: 0);
                        if (y > 0) CopyFaceY(src, _chunksLight[Idx(x, y - 1, z)], _seeds[Idx(x, y - 1, z)], ref _seedCounts[Idx(x, y - 1, z)], srcY: 0, dstY: 15);
                    }

            // Flood only from positions that received new light.
            for (int i = 0; i < NVol; i++)
            {
                if (_seedCounts[i] == 0) continue;
                _flood.FloodLightSeeded(
                    _chunksData[i], _chunksLight[i],
                    _seeds[i], _seedCounts[i],
                    dataLightRadius, dataTransparent);
            }
        }
    }

    private void Output(int cx, int cy, int cz)
    {
        Chunk chunk = _voxelMap.GetChunkAt(cx, cy, cz);
        byte[] renderLight = chunk.Rendered.Light;

        for (int x = 0; x < Out; x++)
            for (int y = 0; y < Out; y++)
                for (int z = 0; z < Out; z++)
                {
                    int gx = CS - 1 + x, gy = CS - 1 + y, gz = CS - 1 + z;
                    renderLight[(z * Out + y) * Out + x] =
                        _chunksLight[Idx(gx / CS, gy / CS, gz / CS)]
                                    [BlockIdx(gx % CS, gy % CS, gz % CS)];
                }
    }

    // ── Face copy helpers ─────────────────────────────────────────────────────
    // Each records the destination index in seeds[] when the value improved.

    private static void CopyFaceZ(byte[] src, byte[] dst, int[] seeds, ref int count, int srcZ, int dstZ)
    {
        for (int y = 0; y < CS; y++)
            for (int x = 0; x < CS; x++)
            {
                int sv = src[BlockIdx(x, y, srcZ)] - 1;
                int di = BlockIdx(x, y, dstZ);
                if (sv > 0 && dst[di] < sv) { dst[di] = (byte)sv; seeds[count++] = di; }
            }
    }

    private static void CopyFaceX(byte[] src, byte[] dst, int[] seeds, ref int count, int srcX, int dstX)
    {
        for (int z = 0; z < CS; z++)
            for (int y = 0; y < CS; y++)
            {
                int sv = src[BlockIdx(srcX, y, z)] - 1;
                int di = BlockIdx(dstX, y, z);
                if (sv > 0 && dst[di] < sv) { dst[di] = (byte)sv; seeds[count++] = di; }
            }
    }

    private static void CopyFaceY(byte[] src, byte[] dst, int[] seeds, ref int count, int srcY, int dstY)
    {
        for (int z = 0; z < CS; z++)
            for (int x = 0; x < CS; x++)
            {
                int sv = src[BlockIdx(x, srcY, z)] - 1;
                int di = BlockIdx(x, dstY, z);
                if (sv > 0 && dst[di] < sv) { dst[di] = (byte)sv; seeds[count++] = di; }
            }
    }
}