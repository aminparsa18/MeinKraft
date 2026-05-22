using LibNoise;
using System.Runtime.CompilerServices;

namespace MeinKraft.Mods;

public class TreeGenerator : IMod
{
    private const int TreeCount = 5;

    private readonly Random _rnd = new();
    private readonly Billow _treeNoise = new();
    private IServerModManager _m;

    private int BLOCK_GRASS;
    private int BLOCK_OAKTRUNK, BLOCK_OAKLEAVES, BLOCK_APPLES;
    private int BLOCK_SPRUCETRUNK, BLOCK_SPRUCELEAVES;
    private int BLOCK_BIRCHTRUNK, BLOCK_BIRCHLEAVES;

    // ── Pre-computed 8-direction lookup (45° increments) ─────────────────────
    // Replaces repeated Math.Cos/Sin calls inside tight loops.
    // Each entry is (dx, dy) for angles 45°, 90°, 135°, ..., 360°.
    private static readonly (int dx, int dy)[] Dirs8 =
    [
        ( 1,  1), ( 0,  1), (-1,  1), (-1,  0),
        (-1, -1), ( 0, -1), ( 1, -1), ( 1,  0),
    ];

    // ── IMod ─────────────────────────────────────────────────────────────────

    public void PreStart(IServerModManager m) => m.RequireMod("CoreBlocks");

    public void Start(IServerModManager manager, IModEvents modEvents)
    {
        _m = manager;

        BLOCK_GRASS = _m.GetBlockId("Grass");
        BLOCK_OAKTRUNK = _m.GetBlockId("OakTreeTrunk");
        BLOCK_OAKLEAVES = _m.GetBlockId("OakLeaves");
        BLOCK_APPLES = _m.GetBlockId("Apples");
        BLOCK_SPRUCETRUNK = _m.GetBlockId("SpruceTreeTrunk");
        BLOCK_SPRUCELEAVES = _m.GetBlockId("SpruceLeaves");
        BLOCK_BIRCHTRUNK = _m.GetBlockId("BirchTreeTrunk");
        BLOCK_BIRCHLEAVES = _m.GetBlockId("BirchLeaves");

        InitNoise(_m.Seed);

        modEvents.PopulateChunk += PopulateChunk;
    }

    private void InitNoise(int seed)
    {
        // Low-frequency billow — drives forest cluster density across the map.
        // High lacunarity means clusters form quickly over short distances.
        _treeNoise.Seed = seed + 2;
        _treeNoise.OctaveCount = 6;
        _treeNoise.Frequency = 1f / 180f;
        _treeNoise.Lacunarity = 2f;
        _treeNoise.Persistence = 0.5f;
    }

    // ── Chunk population ──────────────────────────────────────────────────────

    private void PopulateChunk(PopulateChunkArgs args)
    {
        int chunkSize = _m.GetChunkSize();
        int ox = args.X * chunkSize;
        int oy = args.Y * chunkSize;
        int oz = args.Z * chunkSize;

        // Forest density: billow value in [-0.5, 1.5] × 1000, capped at 300.
        // High-value areas get dense forest clusters; low-value areas get sparse trees.
        float density = _treeNoise.GetValue(ox / 512f, 0f, oy / 512f) * 1000f;
        int forestCount = (int)MathF.Min(MathF.Max(density, 0f), 300f);

        // Forest cluster pass — concentrated in noise-high areas
        PlaceTrees(ox, oy, oz, chunkSize, forestCount);

        // Background scatter — always some trees regardless of noise
        PlaceTrees(ox, oy, oz, chunkSize, TreeCount);
    }

    private void PlaceTrees(int ox, int oy, int oz, int chunkSize, int count)
    {
        for (int i = 0; i < count; i++)
        {
            int x = ox + _rnd.Next(chunkSize);
            int y = oy + _rnd.Next(chunkSize);

            // Scan down from chunk top to find the actual surface grass block.
            // The original picked a random z, which almost never hit the surface.
            int surfaceZ = FindSurface(x, y, oz, oz + chunkSize - 1);
            if (surfaceZ == -1)
            {
                continue;
            }

            switch (_rnd.Next(3))
            {
                case 0: MakeSpruce(x, y, surfaceZ); break;
                case 1: MakeOak(x, y, surfaceZ); break;
                case 2: MakeBirch(x, y, surfaceZ); break;
            }
        }
    }

    /// <summary>
    /// Scans downward from <paramref name="zTop"/> to <paramref name="zBottom"/>
    /// and returns the z of the highest grass block, or -1 if none found.
    /// </summary>
    private int FindSurface(int x, int y, int zBottom, int zTop)
    {
        for (int z = zTop; z >= zBottom; z--)
        {
            if (!_m.IsValidPos(x, y, z))
            {
                continue;
            }

            if (_m.GetBlock(x, y, z) == BLOCK_GRASS)
            {
                return z;
            }
        }

        return -1;
    }

    // ── Tree types ────────────────────────────────────────────────────────────

    /// <summary>
    /// Spruce — tall, layered conical canopy with two branch tiers.
    /// </summary>
    private void MakeSpruce(int x, int y, int z)
    {
        int h = _rnd.Next(8, 12);

        for (int i = 0; i < h; i++)
        {
            Set(x, y, z + i, BLOCK_SPRUCETRUNK);
        }

        // Lower branch tier — wide spread
        SpruceCanopyTier(x, y, z + h - 3, maxK: 4);

        // Upper branch tier — narrow crown
        SpruceCanopyTier(x, y, z + h - 1, maxK: 3);
    }

    private void SpruceCanopyTier(int x, int y, int z, int maxK)
    {
        foreach ((int dx, int dy) in Dirs8)
        {
            // Cardinal directions get full length, diagonals get half
            bool cardinal = dx == 0 || dy == 0;
            for (int k = 1; k < maxK; k++)
            {
                int length = cardinal ? k : k / 2;
                if (length == 0)
                {
                    continue;
                }

                int bx = x + (dx * length);
                int by = y + (dy * length);

                Set(bx, by, z, BLOCK_SPRUCETRUNK);
                SetIfEmpty(bx, by, z + 1, BLOCK_SPRUCELEAVES);
                SetIfEmpty(bx + 1, by, z, BLOCK_SPRUCELEAVES);
                SetIfEmpty(bx - 1, by, z, BLOCK_SPRUCELEAVES);
                SetIfEmpty(bx, by + 1, z, BLOCK_SPRUCELEAVES);
                SetIfEmpty(bx, by - 1, z, BLOCK_SPRUCELEAVES);
            }
        }
    }

    /// <summary>
    /// Oak — short, rounded canopy. Chance to spawn as an apple tree.
    /// </summary>
    private void MakeOak(int x, int y, int z)
    {
        int h = _rnd.Next(4, 6);
        bool isAppleTree = _rnd.NextSingle() < 0.1f;

        for (int i = 0; i < h; i++)
        {
            Set(x, y, z + i, BLOCK_OAKTRUNK);
        }

        foreach ((int dx, int dy) in Dirs8)
        {
            bool cardinal = dx == 0 || dy == 0;
            int bx = x + (cardinal ? dx : 0);
            int by = y + (cardinal ? dy : 0);
            if (!cardinal)
            {
                continue; // Oak crown is cross-shaped, skip diagonals
            }

            Set(bx, by, z + h - 1, BLOCK_OAKTRUNK);

            if (isAppleTree)
            {
                const float appleChance = 0.4f;
                SetIfEmpty(bx, by, z + h, _rnd.NextSingle() < appleChance ? BLOCK_APPLES : BLOCK_OAKLEAVES);
                SetIfEmpty(bx + 1, by, z + h - 1, _rnd.NextSingle() < appleChance ? BLOCK_APPLES : BLOCK_OAKLEAVES);
                SetIfEmpty(bx - 1, by, z + h - 1, _rnd.NextSingle() < appleChance ? BLOCK_APPLES : BLOCK_OAKLEAVES);
                SetIfEmpty(bx, by + 1, z + h - 1, _rnd.NextSingle() < appleChance ? BLOCK_APPLES : BLOCK_OAKLEAVES);
                SetIfEmpty(bx, by - 1, z + h - 1, _rnd.NextSingle() < appleChance ? BLOCK_APPLES : BLOCK_OAKLEAVES);
            }
            else
            {
                SetIfEmpty(bx, by, z + h, BLOCK_OAKLEAVES);
                SetIfEmpty(bx + 1, by, z + h - 1, BLOCK_OAKLEAVES);
                SetIfEmpty(bx - 1, by, z + h - 1, BLOCK_OAKLEAVES);
                SetIfEmpty(bx, by + 1, z + h - 1, BLOCK_OAKLEAVES);
                SetIfEmpty(bx, by - 1, z + h - 1, BLOCK_OAKLEAVES);
            }
        }
    }

    /// <summary>
    /// Birch — medium height, layered branches every 3 blocks above the base.
    /// Alternates between tight (k=2) and wide (k=3) tiers for a layered look.
    /// </summary>
    private void MakeBirch(int x, int y, int z)
    {
        int h = _rnd.Next(6, 9);

        for (int i = 0; i < h; i++)
        {
            Set(x, y, z + i, BLOCK_BIRCHTRUNK);

            if (i > 3)
            {
                // Alternate between tight and wide canopy tiers every 3 blocks
                bool wideTier = i % 3 == 2;
                BirchCanopyTier(x, y, z + i, maxK: wideTier ? 3 : 2);
            }
        }

        // Cap
        SetIfEmpty(x, y, z + h, BLOCK_BIRCHLEAVES);
    }

    private void BirchCanopyTier(int x, int y, int z, int maxK)
    {
        foreach ((int dx, int dy) in Dirs8)
        {
            bool cardinal = dx == 0 || dy == 0;
            for (int k = 1; k < maxK; k++)
            {
                int length = cardinal ? k : k / 2;
                if (length == 0)
                {
                    continue;
                }

                int bx = x + (dx * length);
                int by = y + (dy * length);

                Set(bx, by, z, BLOCK_BIRCHTRUNK);
                SetIfEmpty(bx, by, z + 1, BLOCK_BIRCHLEAVES);
                SetIfEmpty(bx + 1, by, z, BLOCK_BIRCHLEAVES);
                SetIfEmpty(bx - 1, by, z, BLOCK_BIRCHLEAVES);
                SetIfEmpty(bx, by + 1, z, BLOCK_BIRCHLEAVES);
                SetIfEmpty(bx, by - 1, z, BLOCK_BIRCHLEAVES);
            }
        }
    }

    // ── Block helpers ─────────────────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Set(int x, int y, int z, int block)
    {
        if (_m.IsValidPos(x, y, z))
        {
            _m.SetBlock(x, y, z, block);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetIfEmpty(int x, int y, int z, int block)
    {
        if (_m.IsValidPos(x, y, z) && _m.GetBlock(x, y, z) == 0)
        {
            _m.SetBlock(x, y, z, block);
        }
    }
}