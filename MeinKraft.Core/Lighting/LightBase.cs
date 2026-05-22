/// <summary>
/// Calculates base lighting for a single 16×16×16 chunk.
/// Handles sunlight seeding from the heightmap, sunlight flood-fill,
/// and light-emitting block propagation.
/// Does not spread light across chunk boundaries.
///
/// Change from original:
///   SunlightFlood no longer calls FloodLight per mismatched pair.
///   After sunlight seeding, one FloodLightAll call propagates from all
///   lit positions simultaneously — O(BFS) instead of O(n × BFS).
/// </summary>
public class LightBase
{
    private const int ChunkVolume = 16 * 16 * 16;
    private const int ChunkSize = 16;

    private readonly LightFlood _flood;
    private readonly IVoxelMap _voxelMap;
    private readonly int[] _workData = new int[ChunkVolume];

    public LightBase(IVoxelMap voxelMap)
    {
        _flood = new LightFlood();
        _voxelMap = voxelMap;
    }

    public void CalculateChunkBaseLight(
        int cx, int cy, int cz,
        int[] dataLightRadius,
        bool[] transparentForLight)
    {
        Chunk chunk = _voxelMap.GetChunkAt(cx, cy, cz);

        for (int i = 0; i < ChunkVolume; i++)
            _workData[i] = chunk.GetBlock(i);

        byte[] workLight = chunk.BaseLight;
        Array.Clear(workLight, 0, workLight.Length);

        SeedSunlight(cx, cy, cz, workLight, GameConstants.maxlight);

        // One multi-source BFS from every lit position replaces the old
        // per-pair double-flood loop.  Visits each cell at most once.
        _flood.FloodLightAll(_workData, workLight, dataLightRadius, transparentForLight);

        SeedEmissiveBlocks(_workData, workLight, dataLightRadius, transparentForLight);
    }

    // ── Sunlight seeding ──────────────────────────────────────────────────────

    private void SeedSunlight(
        int cx, int cy, int cz,
        byte[] workLight,
        int sunlight)
    {
        int baseHeight = cz * GameConstants.CHUNK_SIZE;

        for (int xx = 0; xx < ChunkSize; xx++)
            for (int yy = 0; yy < ChunkSize; yy++)
            {
                int height = GetLightHeight(cx, cy, xx, yy);
                int z = Math.Clamp(height - baseHeight, 0, ChunkSize);

                if (z >= ChunkSize) continue;   // sunlight enters above this chunk

                int pos = z * 256 + yy * 16 + xx;
                for (int zz = z; zz < ChunkSize; zz++, pos += LightFlood.ZPlus)
                    workLight[pos] = (byte)sunlight;
            }
    }

    private int GetLightHeight(int cx, int cy, int xx, int yy)
    {
        int[] heightmapChunk = _voxelMap.Heightmap.GetOrAllocChunk(
            cx * GameConstants.CHUNK_SIZE,
            cy * GameConstants.CHUNK_SIZE);

        if (heightmapChunk == null) return 0;

        return heightmapChunk[VectorIndexUtil.Index2d(
            xx % GameConstants.CHUNK_SIZE,
            yy % GameConstants.CHUNK_SIZE,
            GameConstants.CHUNK_SIZE)];
    }

    // ── Emissive block seeding ────────────────────────────────────────────────

    private void SeedEmissiveBlocks(
        int[] workData, byte[] workLight,
        int[] dataLightRadius, bool[] dataTransparent)
    {
        for (int pos = 0; pos < ChunkVolume; pos++)
        {
            int blockId = workData[pos];
            if (blockId < 10) continue;     // no block below ID 10 emits light

            int emitRadius = dataLightRadius[blockId];
            if (emitRadius == 0 || emitRadius <= workLight[pos]) continue;

            workLight[pos] = (byte)Math.Max(emitRadius, workLight[pos]);

            // Fast x/y/z decode without division — layout is z*256 + y*16 + x
            int xx = pos & 15;
            int yy = (pos >> 4) & 15;
            int zz = (pos >> 8) & 15;
            _flood.FloodLight(workData, workLight, xx, yy, zz, dataLightRadius, dataTransparent);
        }
    }
}