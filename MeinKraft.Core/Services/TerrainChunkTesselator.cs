//Block definition:
//
//      Z
//      |
//      |
//      |
//      +----- X
//     /
//    /
//   Y
//

// <summary>
// Generates triangles for a single 16x16x16 chunk.
// Needs to know the surrounding of the chunk (18x18x18 blocks total).
// This class is heavily inlined and unrolled for performance.
// Special-shape (rare) blocks don't need as much performance.
// </summary>
using MeinKraft;
using MeinKraft.Worker;
using OpenTK.Mathematics;
using static ModDrawHand3d;

public class TerrainChunkTesselator : ITerrainChunkTesselator
{
    private float _texrecLeft;
    private float _texrecRight;
    private float _texrecWidth;
    private float _texrecHeight;
    private int _colorWhite;

    public bool EnableSmoothLight { get; set; }

    private readonly IVoxelMap _voxelMap;
    private readonly IGameWindowService _platform;
    private readonly IBlockRegistry _blockTypeRegistry;

    private const int chunksize = 16;

    private bool started;
    private int mapsizex;
    private int mapsizey;
    private int mapsizez;

    private int _atlasCount;   // was toreturnatlas1dLength

   // private int terrainTexturesPerAtlas;
    private float terrainTexturesPerAtlasInverse;

    // Transparent, Lowered and Fluid flags are packed into a single byte per block.
    // One allocation, one cache line covers ~64 block types simultaneously.
    private BlockRenderFlags[] _blockFlags;

    // Convenience accessors — keep existing call-sites readable.
    private bool IsTransparent(int id) => (_blockFlags[id] & BlockRenderFlags.Transparent) != 0;
    private bool IsLowered(int id) => (_blockFlags[id] & BlockRenderFlags.Lowered) != 0;
    private bool IsFluid(int id) => (_blockFlags[id] & BlockRenderFlags.Fluid) != 0;

    public float BlockShadow { get; set; }
    public bool DarkenBlockSidesOption { get; set; }
    public int TerrainTexturesPerAtlas { get; set; }
    public int[] TerrainTextures1d { get; set; }
    /// <summary>Texture IDs indexed by [blockId][TileSide].</summary>
    public Dictionary<int, int[]> TextureId { get; set; } = [];

    private bool option_DoNotDrawEdges;
    private float AtiArtifactFix;

    private readonly Vector3i[][] c_OcclusionNeighbors;

    // Indexed [TileSide][Corner] → Corner index into CornerHeights, or -1 = unmodified.
    // Populated once in the constructor; read-only thereafter.
    private readonly int[,] _cornerHeightLookup;

    public TerrainChunkTesselator(IVoxelMap voxelMap, IGameWindowService platform, IBlockRegistry blockTypeRegistry)
    {
        _voxelMap = voxelMap;
        _platform = platform;
        _blockTypeRegistry = blockTypeRegistry;
        EnableSmoothLight = true;
        ENABLE_TEXTURE_TILING = true;
        _colorWhite = ColorUtils.ColorFromArgb(255, 255, 255, 255);
        BlockShadow = 0.6f;
        DarkenBlockSidesOption = true;
        option_DoNotDrawEdges = true;
        occ = 0.7f;
        halfocc = 0.4f;

        // Dimensions: [SideCount, 4 corners]. -1 means "no height modification".
        const int sides = TileSideExt.Count;
        const int corners = 4;
        _cornerHeightLookup = new int[sides, corners];

        // Default everything to -1 (no modification).
        for (int s = 0; s < sides; s++)
        {
            for (int c = 0; c < corners; c++)
            {
                _cornerHeightLookup[s, c] = -1;
            }
        }

        // Top: corners map to themselves.
        _cornerHeightLookup[(int)TileSide.Top, (int)Corner.TopLeft] = (int)Corner.TopLeft;
        _cornerHeightLookup[(int)TileSide.Top, (int)Corner.TopRight] = (int)Corner.TopRight;
        _cornerHeightLookup[(int)TileSide.Top, (int)Corner.BottomLeft] = (int)Corner.BottomLeft;
        _cornerHeightLookup[(int)TileSide.Top, (int)Corner.BottomRight] = (int)Corner.BottomRight;

        // Right side.
        _cornerHeightLookup[(int)TileSide.Right, (int)Corner.TopRight] = (int)Corner.TopRight;
        _cornerHeightLookup[(int)TileSide.Right, (int)Corner.TopLeft] = (int)Corner.BottomRight;

        // Left side.
        _cornerHeightLookup[(int)TileSide.Left, (int)Corner.TopLeft] = (int)Corner.TopLeft;
        _cornerHeightLookup[(int)TileSide.Left, (int)Corner.TopRight] = (int)Corner.BottomLeft;

        // Front side.
        _cornerHeightLookup[(int)TileSide.Front, (int)Corner.TopLeft] = (int)Corner.BottomLeft;
        _cornerHeightLookup[(int)TileSide.Front, (int)Corner.TopRight] = (int)Corner.BottomRight;

        // Back side.
        _cornerHeightLookup[(int)TileSide.Back, (int)Corner.TopLeft] = (int)Corner.TopRight;
        _cornerHeightLookup[(int)TileSide.Back, (int)Corner.TopRight] = (int)Corner.TopLeft;

        // Bottom: never modified (all remain -1).

        // ── Occlusion neighbour offsets ───────────────────────────────────────
        c_OcclusionNeighbors = new Vector3i[TileSideExt.Count][];
        for (int i = 0; i < TileSideExt.Count; i++)
        {
            c_OcclusionNeighbors[i] = new Vector3i[(int)TileDirection.Count];
        }

        // Top
        c_OcclusionNeighbors[(int)TileSide.Top][(int)TileDirection.Center] = new Vector3i(0, 0, 1);
        c_OcclusionNeighbors[(int)TileSide.Top][(int)TileDirection.Top] = new Vector3i(0, -1, 1);
        c_OcclusionNeighbors[(int)TileSide.Top][(int)TileDirection.Bottom] = new Vector3i(0, 1, 1);
        c_OcclusionNeighbors[(int)TileSide.Top][(int)TileDirection.Left] = new Vector3i(-1, 0, 1);
        c_OcclusionNeighbors[(int)TileSide.Top][(int)TileDirection.Right] = new Vector3i(1, 0, 1);
        c_OcclusionNeighbors[(int)TileSide.Top][(int)TileDirection.TopLeft] = new Vector3i(-1, -1, 1);
        c_OcclusionNeighbors[(int)TileSide.Top][(int)TileDirection.TopRight] = new Vector3i(1, -1, 1);
        c_OcclusionNeighbors[(int)TileSide.Top][(int)TileDirection.BottomLeft] = new Vector3i(-1, 1, 1);
        c_OcclusionNeighbors[(int)TileSide.Top][(int)TileDirection.BottomRight] = new Vector3i(1, 1, 1);

        // Left
        c_OcclusionNeighbors[(int)TileSide.Left][(int)TileDirection.Center] = new Vector3i(-1, 0, 0);
        c_OcclusionNeighbors[(int)TileSide.Left][(int)TileDirection.Top] = new Vector3i(-1, 0, 1);
        c_OcclusionNeighbors[(int)TileSide.Left][(int)TileDirection.Bottom] = new Vector3i(-1, 0, -1);
        c_OcclusionNeighbors[(int)TileSide.Left][(int)TileDirection.Left] = new Vector3i(-1, -1, 0);
        c_OcclusionNeighbors[(int)TileSide.Left][(int)TileDirection.Right] = new Vector3i(-1, 1, 0);
        c_OcclusionNeighbors[(int)TileSide.Left][(int)TileDirection.TopLeft] = new Vector3i(-1, -1, 1);
        c_OcclusionNeighbors[(int)TileSide.Left][(int)TileDirection.TopRight] = new Vector3i(-1, 1, 1);
        c_OcclusionNeighbors[(int)TileSide.Left][(int)TileDirection.BottomLeft] = new Vector3i(-1, -1, -1);
        c_OcclusionNeighbors[(int)TileSide.Left][(int)TileDirection.BottomRight] = new Vector3i(-1, 1, -1);

        // Bottom
        c_OcclusionNeighbors[(int)TileSide.Bottom][(int)TileDirection.Center] = new Vector3i(0, 0, -1);
        c_OcclusionNeighbors[(int)TileSide.Bottom][(int)TileDirection.Top] = new Vector3i(0, 1, -1);
        c_OcclusionNeighbors[(int)TileSide.Bottom][(int)TileDirection.Bottom] = new Vector3i(0, -1, -1);
        c_OcclusionNeighbors[(int)TileSide.Bottom][(int)TileDirection.Left] = new Vector3i(-1, 0, -1);
        c_OcclusionNeighbors[(int)TileSide.Bottom][(int)TileDirection.Right] = new Vector3i(1, 0, -1);
        c_OcclusionNeighbors[(int)TileSide.Bottom][(int)TileDirection.TopLeft] = new Vector3i(-1, 1, -1);
        c_OcclusionNeighbors[(int)TileSide.Bottom][(int)TileDirection.TopRight] = new Vector3i(1, 1, -1);
        c_OcclusionNeighbors[(int)TileSide.Bottom][(int)TileDirection.BottomLeft] = new Vector3i(-1, -1, -1);
        c_OcclusionNeighbors[(int)TileSide.Bottom][(int)TileDirection.BottomRight] = new Vector3i(1, -1, -1);

        // Right
        c_OcclusionNeighbors[(int)TileSide.Right][(int)TileDirection.Center] = new Vector3i(1, 0, 0);
        c_OcclusionNeighbors[(int)TileSide.Right][(int)TileDirection.Top] = new Vector3i(1, 0, 1);
        c_OcclusionNeighbors[(int)TileSide.Right][(int)TileDirection.Bottom] = new Vector3i(1, 0, -1);
        c_OcclusionNeighbors[(int)TileSide.Right][(int)TileDirection.Left] = new Vector3i(1, 1, 0);
        c_OcclusionNeighbors[(int)TileSide.Right][(int)TileDirection.Right] = new Vector3i(1, -1, 0);
        c_OcclusionNeighbors[(int)TileSide.Right][(int)TileDirection.TopLeft] = new Vector3i(1, 1, 1);
        c_OcclusionNeighbors[(int)TileSide.Right][(int)TileDirection.TopRight] = new Vector3i(1, -1, 1);
        c_OcclusionNeighbors[(int)TileSide.Right][(int)TileDirection.BottomLeft] = new Vector3i(1, 1, -1);
        c_OcclusionNeighbors[(int)TileSide.Right][(int)TileDirection.BottomRight] = new Vector3i(1, -1, -1);

        // Back
        c_OcclusionNeighbors[(int)TileSide.Back][(int)TileDirection.Center] = new Vector3i(0, -1, 0);
        c_OcclusionNeighbors[(int)TileSide.Back][(int)TileDirection.Top] = new Vector3i(0, -1, 1);
        c_OcclusionNeighbors[(int)TileSide.Back][(int)TileDirection.Bottom] = new Vector3i(0, -1, -1);
        c_OcclusionNeighbors[(int)TileSide.Back][(int)TileDirection.Left] = new Vector3i(1, -1, 0);
        c_OcclusionNeighbors[(int)TileSide.Back][(int)TileDirection.Right] = new Vector3i(-1, -1, 0);
        c_OcclusionNeighbors[(int)TileSide.Back][(int)TileDirection.TopLeft] = new Vector3i(1, -1, 1);
        c_OcclusionNeighbors[(int)TileSide.Back][(int)TileDirection.TopRight] = new Vector3i(-1, -1, 1);
        c_OcclusionNeighbors[(int)TileSide.Back][(int)TileDirection.BottomLeft] = new Vector3i(1, -1, -1);
        c_OcclusionNeighbors[(int)TileSide.Back][(int)TileDirection.BottomRight] = new Vector3i(-1, -1, -1);

        // Front
        c_OcclusionNeighbors[(int)TileSide.Front][(int)TileDirection.Center] = new Vector3i(0, 1, 0);
        c_OcclusionNeighbors[(int)TileSide.Front][(int)TileDirection.Top] = new Vector3i(0, 1, 1);
        c_OcclusionNeighbors[(int)TileSide.Front][(int)TileDirection.Bottom] = new Vector3i(0, 1, -1);
        c_OcclusionNeighbors[(int)TileSide.Front][(int)TileDirection.Left] = new Vector3i(-1, 1, 0);
        c_OcclusionNeighbors[(int)TileSide.Front][(int)TileDirection.Right] = new Vector3i(1, 1, 0);
        c_OcclusionNeighbors[(int)TileSide.Front][(int)TileDirection.TopLeft] = new Vector3i(-1, 1, 1);
        c_OcclusionNeighbors[(int)TileSide.Front][(int)TileDirection.TopRight] = new Vector3i(1, 1, 1);
        c_OcclusionNeighbors[(int)TileSide.Front][(int)TileDirection.BottomLeft] = new Vector3i(-1, 1, -1);
        c_OcclusionNeighbors[(int)TileSide.Front][(int)TileDirection.BottomRight] = new Vector3i(1, 1, -1);
    }

    private static int Index3d(int x, int y, int h, int sizex, int sizey)
        => (((h * sizey) + y) * sizex) + x;

    public void Start()
    {
        mapsizex = _voxelMap.MapSizeX;
        mapsizey = _voxelMap.MapSizeY;
        mapsizez = _voxelMap.MapSizeZ;
        started = true;

        _blockFlags = new BlockRenderFlags[GameConstants.MAX_BLOCKTYPES];

        RefreshBlockTypeCache();
    }

    public void OnAtlasReady(int texturesPerAtlas)
    {
        TerrainTexturesPerAtlas = texturesPerAtlas;
        terrainTexturesPerAtlasInverse = 1f / texturesPerAtlas;
        AtiArtifactFix = _platform.IsFastSystem() ? 1 / 32f * 0.25f : 1 / 32f * 1.5f;
        _texrecWidth = 1 - (AtiArtifactFix * 2);
        _texrecHeight = terrainTexturesPerAtlasInverse * (1 - (AtiArtifactFix * 2));
        _texrecLeft = AtiArtifactFix;
        _texrecRight = _texrecLeft + _texrecWidth;
        _atlasCount = Math.Max(1, GameConstants.MAX_BLOCKTYPES / texturesPerAtlas);
    }

    /// <summary>
    /// Creates a context sized for the current atlas configuration.
    /// Call after <see cref="Start"/>. Typically called once per worker thread
    /// inside the <see cref="ThreadLocal{T}"/> factory.
    /// </summary>
    public ChunkTessellationContext CreateContext()
        => new ChunkTessellationContext(_atlasCount);

    /// <summary>
    /// Rebuilds the per-block render flag cache from the current block type definitions.
    /// Called once from <see cref="Start"/> and again whenever block types change.
    /// Previously this ran inside <see cref="MakeChunk"/>, executing on every single
    /// chunk build even though block definitions almost never change.
    /// </summary>
    public void RefreshBlockTypeCache()
    {
        foreach ((int id, BlockType? b) in _blockTypeRegistry.BlockTypes)
        {
            BlockRenderFlags flags = BlockRenderFlags.None;

            if (b.DrawType is not DrawType.Solid and not DrawType.Fluid)
            {
                flags |= BlockRenderFlags.Transparent;
            }

            if (b.DrawType == DrawType.HalfHeight
             || b.DrawType == DrawType.Flat
             || b.Rail != 0)
            {
                flags |= BlockRenderFlags.Lowered;
            }

            if (b.DrawType == DrawType.Fluid)
            {
                flags |= BlockRenderFlags.Fluid;
            }

            _blockFlags[id] = flags;
        }
    }

    // ── Visible face calculation ───────────────────────────────────────────────
    private void CalculateVisibleFaces(int[] currentChunk, ChunkTessellationContext ctx)
    {
        int movez = (chunksize + 2) * (chunksize + 2);

        for (int zz = 1; zz < chunksize + 1; zz++)
        {
            for (int yy = 1; yy < chunksize + 1; yy++)
            {
                int posstart = Index3d(0, yy, zz, chunksize + 2, chunksize + 2);
                for (int xx = 1; xx < chunksize + 1; xx++)
                {
                    int pos = posstart + xx;
                    int tt = currentChunk[pos];
                    if (tt == 0)
                    {
                        continue;
                    }

                    TileSideFlags draw = TileSideFlags.None;

                    int[] nPos = ctx.TmpNPos;
                    nPos[(int)TileSide.Top] = pos + movez;
                    nPos[(int)TileSide.Bottom] = pos - movez;
                    nPos[(int)TileSide.Front] = pos + chunksize + 2;
                    nPos[(int)TileSide.Back] = pos - (chunksize + 2);
                    nPos[(int)TileSide.Left] = pos - 1;
                    nPos[(int)TileSide.Right] = pos + 1;

                    bool blnIsFluid = IsFluid(tt);
                    bool blnIsLowered = IsLowered(tt);

                    draw |= GetFaceVisibility(TileSide.Top, currentChunk, nPos, blnIsFluid, blnIsLowered);
                    draw |= GetFaceVisibility(TileSide.Bottom, currentChunk, nPos, blnIsFluid, blnIsLowered);
                    draw |= GetFaceVisibility(TileSide.Left, currentChunk, nPos, blnIsFluid, blnIsLowered);
                    draw |= GetFaceVisibility(TileSide.Right, currentChunk, nPos, blnIsFluid, blnIsLowered);
                    draw |= GetFaceVisibility(TileSide.Back, currentChunk, nPos, blnIsFluid, blnIsLowered);
                    draw |= GetFaceVisibility(TileSide.Front, currentChunk, nPos, blnIsFluid, blnIsLowered);

                    if (blnIsLowered && draw != TileSideFlags.None)
                    {
                        if (!draw.HasFlag(TileSideFlags.Top))
                        {
                            if (draw.HasFlag(TileSideFlags.Front | TileSideFlags.Back
                                           | TileSideFlags.Right | TileSideFlags.Left))
                            {
                                draw |= TileSideFlags.Top;
                            }
                        }

                        int nRail = Rail(tt);
                        if (nRail > 0)
                        {
                            RailSlope nSlope = GetRailSlope(xx, yy, zz, currentChunk);
                            switch (nSlope)
                            {
                                case RailSlope.TwoDownRaised: draw |= TileSideFlags.Right | TileSideFlags.Front | TileSideFlags.Back; break;
                                case RailSlope.TwoUpRaised: draw |= TileSideFlags.Left | TileSideFlags.Front | TileSideFlags.Back; break;
                                case RailSlope.TwoLeftRaised: draw |= TileSideFlags.Front | TileSideFlags.Right | TileSideFlags.Left; break;
                                case RailSlope.TwoRightRaised: draw |= TileSideFlags.Back | TileSideFlags.Right | TileSideFlags.Left; break;
                            }
                        }
                    }

                    ctx.ChunkDraw16[Index3d(xx - 1, yy - 1, zz - 1, chunksize, chunksize)] = (byte)draw;
                }
            }
        }
    }

    private TileSideFlags GetFaceVisibility(TileSide side, int[] currentChunk, int[] nPos,
                                             bool blnIsFluid, bool blnIsLowered)
    {
        TileSideFlags nReturn = TileSideFlags.None;
        int nIndex = nPos[(int)side];
        int tt2 = currentChunk[nIndex];

        if (tt2 == 0 || (IsTransparent(tt2) && !IsLowered(tt2)) || (IsFluid(tt2) && !blnIsFluid))
        {
            nReturn |= side.ToFlags();
        }
        else if (blnIsFluid && side != TileSide.Bottom)
        {
            if (IsFluid(currentChunk[nPos[(int)TileSide.Bottom]]))
            {
                if (!IsFluid(tt2))
                {
                    int movez = (chunksize + 2) * (chunksize + 2);
                    int nPos2 = nPos[(int)side] - movez;
                    if (nPos2 > 0 && IsFluid(currentChunk[nPos2]))
                    {
                        nReturn |= side.ToFlags();
                    }
                }
            }
        }

        if (IsLowered(tt2) && side != TileSide.Top)
        {
            if (!blnIsLowered)
            {
                nReturn |= side.ToFlags();
            }
            else if (side == TileSide.Bottom)
            {
                nReturn |= TileSideFlags.Bottom;
            }
            else
            {
                nReturn |= TileSideFlags.Top;
            }
        }

        return nReturn;
    }

    private void CalculateTilingCount(int[] currentChunk, int startx, int starty, int startz, ChunkTessellationContext ctx)
    {
        ctx.ChunkDrawCount16Flat.AsSpan(0, chunksize * chunksize * chunksize * 6).Clear();

        for (int zz = 1; zz < chunksize + 1; zz++)
        {
            for (int yy = 1; yy < chunksize + 1; yy++)
            {
                int pos = Index3d(0, yy, zz, chunksize + 2, chunksize + 2);
                for (int xx = 1; xx < chunksize + 1; xx++)
                {
                    int drawCountBase = Index3d(xx - 1, yy - 1, zz - 1, chunksize, chunksize) * 6;
                    int tt = currentChunk[pos + xx];
                    if (tt == 0)
                    {
                        continue;
                    }

                    int draw = ctx.ChunkDraw16[Index3d(xx - 1, yy - 1, zz - 1, chunksize, chunksize)];
                    if (draw == 0)
                    {
                        continue;
                    }

                    if ((draw & (int)TileSideFlags.Top) != 0)
                    {
                        ctx.ChunkDrawCount16Flat[drawCountBase + (int)TileSide.Top] = 1;
                    }

                    if ((draw & (int)TileSideFlags.Bottom) != 0)
                    {
                        ctx.ChunkDrawCount16Flat[drawCountBase + (int)TileSide.Bottom] = 1;
                    }

                    if ((draw & (int)TileSideFlags.Right) != 0)
                    {
                        ctx.ChunkDrawCount16Flat[drawCountBase + (int)TileSide.Left] = 1;
                    }

                    if ((draw & (int)TileSideFlags.Left) != 0)
                    {
                        ctx.ChunkDrawCount16Flat[drawCountBase + (int)TileSide.Right] = 1;
                    }

                    if ((draw & (int)TileSideFlags.Front) != 0)
                    {
                        ctx.ChunkDrawCount16Flat[drawCountBase + (int)TileSide.Back] = 1;
                    }

                    if ((draw & (int)TileSideFlags.Back) != 0)
                    {
                        ctx.ChunkDrawCount16Flat[drawCountBase + (int)TileSide.Front] = 1;
                    }
                }
            }
        }
    }

    public bool ENABLE_TEXTURE_TILING { get; set; }

    private int GetShadowRatio(int xx, int yy, int zz, byte[] shadows)
        => shadows[Index3d(xx, yy, zz, chunksize + 2, chunksize + 2)];

    private void BuildBlockPolygons(
    int x, int y, int z,
    int[] currentChunk, byte[] currentChunkShadows, float[] lightLevels,
    ChunkTessellationContext ctx)
    {
        for (int xx = 0; xx < chunksize; xx++)
            for (int yy = 0; yy < chunksize; yy++)
                for (int zz = 0; zz < chunksize; zz++)
                {
                    if (ctx.ChunkDraw16[Index3d(xx, yy, zz, chunksize, chunksize)] != 0)
                    {
                        BuildSingleBlockPolygon(
                            (x * chunksize) + xx,
                            (y * chunksize) + yy,
                            (z * chunksize) + zz,
                            currentChunk,
                            currentChunkShadows,
                            lightLevels,
                            ctx);
                    }
                }
    }

    private static int ColorMultiply(int color, float fValue)
        => ColorUtils.ColorFromArgb(ColorUtils.ColorA(color),
            (int)(ColorUtils.ColorR(color) * fValue),
            (int)(ColorUtils.ColorG(color) * fValue),
            (int)(ColorUtils.ColorB(color) * fValue));

    private float occ;
    private float halfocc;


    private void BuildBlockFace(
        int x, int y, int z, int tileType,
        float vOffsetX, float vOffsetY, float vOffsetZ,
        float vScaleX, float vScaleY, float vScaleZ,
        int[] currentChunk, byte[] currentChunkShadows, float[] lightLevels,
        TileSide tileSide, ChunkTessellationContext ctx)
    {
        int xx = (x % chunksize) + 1;
        int yy = (y % chunksize) + 1;
        int zz = (z % chunksize) + 1;
        Vector3i[] vNeighbors = c_OcclusionNeighbors[(int)tileSide];

        int[] shadowration = ctx.TmpShadowRation;
        bool[] occupied = ctx.TmpOccupied;

        int shadowratio = GetShadowRatio(
            vNeighbors[(int)TileDirection.Center].X + xx,
            vNeighbors[(int)TileDirection.Center].Y + yy,
            vNeighbors[(int)TileDirection.Center].Z + zz, currentChunkShadows);

        float[] fShadowRation = ctx.TmpFShadowRation;
        float main = lightLevels[shadowratio];
        fShadowRation[(int)Corner.TopLeft] = main;
        fShadowRation[(int)Corner.TopRight] = main;
        fShadowRation[(int)Corner.BottomLeft] = main;
        fShadowRation[(int)Corner.BottomRight] = main;

        if (EnableSmoothLight)
        {
            for (int i = 0; i < (int)TileDirection.Count; i++)
            {
                int vPosX = vNeighbors[i].X + xx;
                int vPosY = vNeighbors[i].Y + yy;
                int vPosZ = vNeighbors[i].Z + zz;
                int nBlockType = currentChunk[Index3d(vPosX, vPosY, vPosZ, chunksize + 2, chunksize + 2)];

                if (nBlockType != 0)
                {
                    occupied[i] = !IsTransparentForLight(nBlockType);
                    shadowration[i] = shadowratio;
                }
                else
                {
                    occupied[i] = false;
                    shadowration[i] = GetShadowRatio(vPosX, vPosY, vPosZ, currentChunkShadows);
                }
            }

            CalcShadowRation(TileDirection.Top, TileDirection.Left, TileDirection.TopLeft, Corner.TopLeft, fShadowRation, occupied, shadowration, lightLevels);
            CalcShadowRation(TileDirection.Top, TileDirection.Right, TileDirection.TopRight, Corner.TopRight, fShadowRation, occupied, shadowration, lightLevels);
            CalcShadowRation(TileDirection.Bottom, TileDirection.Left, TileDirection.BottomLeft, Corner.BottomLeft, fShadowRation, occupied, shadowration, lightLevels);
            CalcShadowRation(TileDirection.Bottom, TileDirection.Right, TileDirection.BottomRight, Corner.BottomRight, fShadowRation, occupied, shadowration, lightLevels);
        }

        DrawBlockFace(x, y, z, tileType, tileSide, vOffsetX, vOffsetY, vOffsetZ,
                      vScaleX, vScaleY, vScaleZ, vNeighbors, fShadowRation, ctx);
    }

    private void CalcShadowRation(
    TileDirection nDir1, TileDirection nDir2, TileDirection nDirBetween,
    Corner nCorner, float[] fShadowRation, bool[] occupied, int[] shadowRationInt,
    float[] lightLevels)
    {
        int d1 = (int)nDir1, d2 = (int)nDir2, db = (int)nDirBetween, c = (int)nCorner;

        if (occupied[d1] && occupied[d2])
        {
            fShadowRation[c] *= halfocc;
        }
        else
        {
            byte facesconsidered = 1;
            if (!occupied[d1])
            {
                fShadowRation[c] += lightLevels[shadowRationInt[d1]];
                facesconsidered++;
            }

            if (!occupied[d2])
            {
                fShadowRation[c] += lightLevels[shadowRationInt[d2]];
                facesconsidered++;
            }

            if (!occupied[db])
            {
                fShadowRation[c] += lightLevels[shadowRationInt[db]];
                facesconsidered++;
            }

            fShadowRation[c] /= facesconsidered;

            if (occupied[d1] || occupied[d2] || occupied[db])
            {
                fShadowRation[c] *= occ;
            }
        }
    }

    private void DrawBlockFace(
        int x, int y, int z, int tileType, TileSide tileSide,
        float vOffsetX, float vOffsetY, float vOffsetZ,
        float vScaleX, float vScaleY, float vScaleZ,
        Vector3i[] vNeighbors, float[] fShadowRation,
        ChunkTessellationContext ctx)
    {
        int color = _colorWhite;
        if (DarkenBlockSidesOption)
        {
            switch (tileSide)
            {
                case TileSide.Bottom:
                case TileSide.Left:
                case TileSide.Right:
                    color = ColorMultiply(color, BlockShadow);
                    break;
            }
        }

        int sidetexture = GetTextureId(tileType, tileSide);
        GeometryModel toreturn = GetModelData(tileType, sidetexture, ctx);
        float texrecTop = (terrainTexturesPerAtlasInverse * (sidetexture % TerrainTexturesPerAtlas))
                           + (AtiArtifactFix * terrainTexturesPerAtlasInverse);
        float texrecBottom = texrecTop + _texrecHeight;
        int lastelement = toreturn.VerticesCount;

        float AddVertex_GetZ(Corner corner)
        {
            Vector3i v = vNeighbors[(int)(corner == Corner.TopLeft ? TileDirection.TopLeft
                                        : corner == Corner.TopRight ? TileDirection.TopRight
                                        : corner == Corner.BottomLeft ? TileDirection.BottomLeft
                                                                       : TileDirection.BottomRight)]
                       + new Vector3i(1, 1, 1);
            float slope = GetCornerHeightModifier(tileSide, corner, ctx);
            return z + vOffsetZ + (v.Z * 0.5f * vScaleZ) + slope;
        }

        {
            Vector3i v = vNeighbors[(int)TileDirection.TopRight] + new Vector3i(1, 1, 1);
            GeometryModel.AddVertex(toreturn,
                x + vOffsetX + (v.X * 0.5f * vScaleX),
                AddVertex_GetZ(Corner.TopRight),
                y + vOffsetY + (v.Y * 0.5f * vScaleY),
                _texrecRight, texrecTop, ColorMultiply(color, fShadowRation[(int)Corner.TopRight]));
        }

        {
            Vector3i v = vNeighbors[(int)TileDirection.TopLeft] + new Vector3i(1, 1, 1);
            GeometryModel.AddVertex(toreturn,
                x + vOffsetX + (v.X * 0.5f * vScaleX),
                AddVertex_GetZ(Corner.TopLeft),
                y + vOffsetY + (v.Y * 0.5f * vScaleY),
                _texrecLeft, texrecTop, ColorMultiply(color, fShadowRation[(int)Corner.TopLeft]));
        }

        {
            Vector3i v = vNeighbors[(int)TileDirection.BottomRight] + new Vector3i(1, 1, 1);
            GeometryModel.AddVertex(toreturn,
                x + vOffsetX + (v.X * 0.5f * vScaleX),
                AddVertex_GetZ(Corner.BottomRight),
                y + vOffsetY + (v.Y * 0.5f * vScaleY),
                _texrecRight, texrecBottom, ColorMultiply(color, fShadowRation[(int)Corner.BottomRight]));
        }

        {
            Vector3i v = vNeighbors[(int)TileDirection.BottomLeft] + new Vector3i(1, 1, 1);
            GeometryModel.AddVertex(toreturn,
                x + vOffsetX + (v.X * 0.5f * vScaleX),
                AddVertex_GetZ(Corner.BottomLeft),
                y + vOffsetY + (v.Y * 0.5f * vScaleY),
                _texrecLeft, texrecBottom, ColorMultiply(color, fShadowRation[(int)Corner.BottomLeft]));
        }

        GeometryModel.AddIndex(toreturn, lastelement + 0);
        GeometryModel.AddIndex(toreturn, lastelement + 1);
        GeometryModel.AddIndex(toreturn, lastelement + 2);
        GeometryModel.AddIndex(toreturn, lastelement + 1);
        GeometryModel.AddIndex(toreturn, lastelement + 3);
        GeometryModel.AddIndex(toreturn, lastelement + 2);
    }

    /// <summary>
    /// Returns the Z height modifier for the given face corner.
    /// Uses a pre-built lookup table instead of a nested switch.
    /// Returns 0 when no modification applies (e.g. bottom face, unmodified corner).
    /// </summary>
    private float GetCornerHeightModifier(TileSide side, Corner corner, ChunkTessellationContext ctx)
    {
        int index = _cornerHeightLookup[(int)side, (int)corner];
        return index < 0 ? 0f : ctx.CornerHeights[(Corner)index];
    }

    private TileSideFlags GetToDrawFlags(int xx, int yy, int zz, ChunkTessellationContext ctx)
    {
        int baseIdx = Index3d(xx - 1, yy - 1, zz - 1, chunksize, chunksize) * 6;
        ReadOnlySpan<byte> drawFlags = ctx.ChunkDrawCount16Flat.AsSpan(baseIdx, 6);

        TileSideFlags nToDraw = TileSideFlags.None;
        if (drawFlags[(int)TileSide.Top] > 0)
        {
            nToDraw |= TileSideFlags.Top;
        }

        if (drawFlags[(int)TileSide.Bottom] > 0)
        {
            nToDraw |= TileSideFlags.Bottom;
        }

        if (drawFlags[(int)TileSide.Left] > 0)
        {
            nToDraw |= TileSideFlags.Right;
        }

        if (drawFlags[(int)TileSide.Right] > 0)
        {
            nToDraw |= TileSideFlags.Left;
        }

        if (drawFlags[(int)TileSide.Back] > 0)
        {
            nToDraw |= TileSideFlags.Front;
        }

        if (drawFlags[(int)TileSide.Front] > 0)
        {
            nToDraw |= TileSideFlags.Back;
        }

        return nToDraw;
    }

    private void BuildSingleBlockPolygon(int x, int y, int z, int[] currentChunk, byte[] currentChunkShadows,
        float[] lightLevels, ChunkTessellationContext ctx)
    {
        ctx.CornerHeights.Clear();

        int xx = (x % chunksize) + 1;
        int yy = (y % chunksize) + 1;
        int zz = (z % chunksize) + 1;

        TileSideFlags nToDraw = GetToDrawFlags(xx, yy, zz, ctx);
        int tiletype = currentChunk[Index3d(xx, yy, zz, chunksize + 2, chunksize + 2)];

        float vOffsetX = 0, vOffsetY = 0, vOffsetZ = 0;
        float vScaleX = 1, vScaleY = 1, vScaleZ = 1;

        if (!Isvalid(tiletype) || nToDraw == TileSideFlags.None)
        {
            return;
        }

        if (option_DoNotDrawEdges)
        {
            if (z == 0)
            {
                nToDraw &= ~TileSideFlags.Bottom;
            }

            if (x == 0)
            {
                nToDraw &= ~TileSideFlags.Front;
            }

            if (x == mapsizex - 1)
            {
                nToDraw &= ~TileSideFlags.Back;
            }

            if (y == 0)
            {
                nToDraw &= ~TileSideFlags.Left;
            }

            if (y == mapsizey - 1)
            {
                nToDraw &= ~TileSideFlags.Right;
            }
        }

        if (IsFlower(tiletype))
        {
            vScaleX = 0.9f;
            vScaleY = 0.9f;
            vScaleZ = 1f;
            BuildBlockFace(x, y, z, tiletype, 0.5f, 0.05f, 0f, vScaleX, vScaleY, vScaleZ, currentChunk, currentChunkShadows, lightLevels, TileSide.Left, ctx);
            BuildBlockFace(x, y, z, tiletype, 0.05f, 0.5f, 0f, vScaleX, vScaleY, vScaleZ, currentChunk, currentChunkShadows, lightLevels, TileSide.Back, ctx);
            return;
        }   

        DrawType drawType = _blockTypeRegistry.BlockTypes[tiletype].DrawType;

        if (drawType == DrawType.Cactus)
        {
            float fScale = 0.875f;
            float fOffset = (1f - fScale) / 2f;
            BuildBlockFace(x, y, z, tiletype, fOffset, 0, 0, fScale, 1f, 1f, currentChunk, currentChunkShadows, lightLevels, TileSide.Left, ctx);
            BuildBlockFace(x, y, z, tiletype, fOffset, 0, 0, fScale, 1f, 1f, currentChunk, currentChunkShadows, lightLevels, TileSide.Right, ctx);
            BuildBlockFace(x, y, z, tiletype, 0, fOffset, 0, 1f, fScale, 1f, currentChunk, currentChunkShadows, lightLevels, TileSide.Front, ctx);
            BuildBlockFace(x, y, z, tiletype, 0, fOffset, 0, 1f, fScale, 1f, currentChunk, currentChunkShadows, lightLevels, TileSide.Back, ctx);
            nToDraw = nToDraw & (TileSideFlags.Top | TileSideFlags.Bottom);
        }
        else if (drawType is DrawType.OpenDoorLeft or DrawType.OpenDoorRight)
        {
            bool blnDrawn = false;
            float fOffset = 0.025f;

            if (currentChunk[Index3d(xx - 1, yy, zz, chunksize + 2, chunksize + 2)] == 0 &&
                currentChunk[Index3d(xx + 1, yy, zz, chunksize + 2, chunksize + 2)] == 0)
            {
                nToDraw = TileSideFlags.Left;
                vOffsetY = fOffset;
                blnDrawn = true;
            }

            if (!blnDrawn ||
                currentChunk[Index3d(xx, yy - 1, zz, chunksize + 2, chunksize + 2)] == 0 &&
                currentChunk[Index3d(xx, yy + 1, zz, chunksize + 2, chunksize + 2)] == 0)
            {
                vOffsetX = fOffset;
                vOffsetY = 0;
                nToDraw = TileSideFlags.Front;
            }
        }
        else if (drawType is DrawType.Fence or DrawType.ClosedDoor)
        {
            bool blnSideDrawn = false;
            if (currentChunk[Index3d(xx - 1, yy, zz, chunksize + 2, chunksize + 2)] != 0 ||
                currentChunk[Index3d(xx + 1, yy, zz, chunksize + 2, chunksize + 2)] != 0)
            {
                BuildBlockFace(x, y, z, tiletype, 0, -0.5f, 0, vScaleX, vScaleY, vScaleZ, currentChunk, currentChunkShadows, lightLevels, TileSide.Front, ctx);
                blnSideDrawn = true;
            }

            if (!blnSideDrawn ||
                currentChunk[Index3d(xx, yy - 1, zz, chunksize + 2, chunksize + 2)] != 0 ||
                currentChunk[Index3d(xx, yy + 1, zz, chunksize + 2, chunksize + 2)] != 0)
            {
                BuildBlockFace(x, y, z, tiletype, 0.5f, 0, 0, vScaleX, vScaleY, vScaleZ, currentChunk, currentChunkShadows, lightLevels, TileSide.Left, ctx);
            }

            return;
        }
        else if (drawType == DrawType.Ladder)
        {
            vOffsetX = 0.025f;
            vOffsetY = 0.025f;
            vScaleX = 0.95f;
            vScaleY = 0.95f;
            vScaleZ = 1f;
            nToDraw = TileSideFlags.None;

            int ladderWall = GetBestLadderWall(xx, yy, zz, currentChunk);
            if (ladderWall < 0)
            {
                int below = GetBestLadderInDirection(xx, yy, zz, currentChunk, -1);
                int above = GetBestLadderInDirection(xx, yy, zz, currentChunk, 1);
                if (below != 0)
                {
                    ladderWall = GetBestLadderWall(xx, yy, zz + below, currentChunk);
                }
                else if (above != 0)
                {
                    ladderWall = GetBestLadderWall(xx, yy, zz + above, currentChunk);
                }
            }
            // TODO: remove magic numbers
            nToDraw = ladderWall switch
            {
                1 => TileSideFlags.Right,
                2 => TileSideFlags.Front,
                3 => TileSideFlags.Back,
                _ => TileSideFlags.Left,
            };
        }
        else if (drawType == DrawType.HalfHeight)
        {
            vScaleZ = 0.5f;
        }
        else if (drawType == DrawType.Flat)
        {
            vScaleZ = 0.05f;
        }
        else if (drawType == DrawType.Torch)
        {
            ctx.TorchSideTexture = GetTextureId(tiletype, TileSide.Left);
            ctx.TorchTopTexture = GetTextureId(tiletype, TileSide.Top);

            TorchType type = TorchType.Normal;
            if (CanSupportTorch(currentChunk[Index3d(xx - 1, yy, zz, chunksize + 2, chunksize + 2)]))
            {
                type = TorchType.Front;
            }

            if (CanSupportTorch(currentChunk[Index3d(xx + 1, yy, zz, chunksize + 2, chunksize + 2)]))
            {
                type = TorchType.Back;
            }

            if (CanSupportTorch(currentChunk[Index3d(xx, yy - 1, zz, chunksize + 2, chunksize + 2)]))
            {
                type = TorchType.Left;
            }

            if (CanSupportTorch(currentChunk[Index3d(xx, yy + 1, zz, chunksize + 2, chunksize + 2)]))
            {
                type = TorchType.Right;
            }

            AddTorch(x, y, z, type, tiletype, ctx);
            return;
        }
        else
        {
            int fluidId = _blockTypeRegistry.BlockIdLava;
            if (fluidId >= 0 && tiletype == fluidId)
            {
                if (currentChunk[Index3d(xx, yy, zz - 1, chunksize + 2, chunksize + 2)] == fluidId)
                {
                    vOffsetZ = -0.1f;
                }
                else
                {
                    vScaleZ = 0.9f;
                }
            }
            else
            {
                int rail = Rail(tiletype);
                if (rail != (int)RailDirectionFlags.None)
                {
                    RailSlope slope = GetRailSlope(xx, yy, zz, currentChunk);
                    vScaleZ = 0.3f;
                    const float fSlopeMod = 1.0f;
                    switch (slope)
                    {
                        case RailSlope.TwoRightRaised: ctx.CornerHeights.TopRight = fSlopeMod; ctx.CornerHeights.BottomRight = fSlopeMod; break;
                        case RailSlope.TwoLeftRaised: ctx.CornerHeights.TopLeft = fSlopeMod; ctx.CornerHeights.BottomLeft = fSlopeMod; break;
                        case RailSlope.TwoUpRaised: ctx.CornerHeights.TopLeft = fSlopeMod; ctx.CornerHeights.TopRight = fSlopeMod; break;
                        case RailSlope.TwoDownRaised: ctx.CornerHeights.BottomLeft = fSlopeMod; ctx.CornerHeights.BottomRight = fSlopeMod; break;
                    }
                }
            }
        }

        for (int i = 0; i < TileSideExt.Count; i++)
        {
            TileSide side = (TileSide)i;
            if ((nToDraw & side.ToFlags()) != TileSideFlags.None)
            {
                BuildBlockFace(x, y, z, tiletype, vOffsetX, vOffsetY, vOffsetZ,
                               vScaleX, vScaleY, vScaleZ, currentChunk, currentChunkShadows, lightLevels, side, ctx);
            }
        }
    }

    private bool IsTransparentForLight(int block)
    {
        BlockType b = _blockTypeRegistry.BlockTypes[block];
        return b.DrawType is not DrawType.Solid and not DrawType.ClosedDoor;
    }

    private GeometryModel GetModelData(int tiletype, int textureid, ChunkTessellationContext ctx)
    {
        return (IsFluid(tiletype) || (IsTransparent(tiletype) && !IsLowered(tiletype)))
            ? ctx.AtlasTransparent[textureid / TerrainTexturesPerAtlas]
            : ctx.Atlas[textureid / TerrainTexturesPerAtlas];
    }

    private int GetTextureId(int tiletype, TileSide side)
        => TextureId[tiletype][(int)side];

    private bool CanSupportTorch(int blocktype)
        => blocktype != 0 && _blockTypeRegistry.BlockTypes[blocktype].DrawType != DrawType.Torch;

    private RailSlope GetRailSlope(int xx, int yy, int zz, int[] currentChunk)
    {
        int tiletype = currentChunk[Index3d(xx, yy, zz, chunksize + 2, chunksize + 2)];
        int rail = Rail(tiletype);
        int blocknear;

        blocknear = currentChunk[Index3d(xx + 1, yy, zz, chunksize + 2, chunksize + 2)];
        if (rail == (int)RailDirectionFlags.Horizontal && blocknear != 0 && Rail(blocknear) == (int)RailDirectionFlags.None)
        {
            return RailSlope.TwoRightRaised;
        }

        blocknear = currentChunk[Index3d(xx - 1, yy, zz, chunksize + 2, chunksize + 2)];
        if (rail == (int)RailDirectionFlags.Horizontal && blocknear != 0 && Rail(blocknear) == (int)RailDirectionFlags.None)
        {
            return RailSlope.TwoLeftRaised;
        }

        blocknear = currentChunk[Index3d(xx, yy - 1, zz, chunksize + 2, chunksize + 2)];
        if (rail == (int)RailDirectionFlags.Vertical && blocknear != 0 && Rail(blocknear) == (int)RailDirectionFlags.None)
        {
            return RailSlope.TwoUpRaised;
        }

        blocknear = currentChunk[Index3d(xx, yy + 1, zz, chunksize + 2, chunksize + 2)];
        if (rail == (int)RailDirectionFlags.Vertical && blocknear != 0 && Rail(blocknear) == (int)RailDirectionFlags.None)
        {
            return RailSlope.TwoDownRaised;
        }

        return RailSlope.Flat;
    }

    private int Rail(int tiletype) => _blockTypeRegistry.BlockTypes[tiletype].Rail;

    private bool IsFlower(int tiletype)
        => _blockTypeRegistry.BlockTypes[tiletype].DrawType == DrawType.Plant;

    private bool Isvalid(int tt)
        => _blockTypeRegistry.BlockTypes[tt]?.Name != null;

    private static int GetBestLadderWall(int x, int y, int z, int[] currentChunk)
    {
        bool front = false, back = false, left = false;
        int wallscount = 0;

        if (currentChunk[Index3d(x, y - 1, z, chunksize + 2, chunksize + 2)] != 0)
        {
            front = true;
            wallscount++;
        }

        if (currentChunk[Index3d(x, y + 1, z, chunksize + 2, chunksize + 2)] != 0)
        {
            back = true;
            wallscount++;
        }

        if (currentChunk[Index3d(x - 1, y, z, chunksize + 2, chunksize + 2)] != 0)
        {
            left = true;
            wallscount++;
        }

        if (currentChunk[Index3d(x + 1, y, z, chunksize + 2, chunksize + 2)] != 0)
        {
            wallscount++;
        }

        if (wallscount != 1)
        {
            return -1;
        }

        if (front)
        {
            return 0;
        }

        if (back)
        {
            return 1;
        }

        if (left)
        {
            return 2;
        }

        return 3;
    }

    private static int GetBestLadderInDirection(int x, int y, int z, int[] currentChunk, int dir)
    {
        // TODO: resolve ladder block ID via BlockTypeRegistry instead of this constant.
        // For now the magic number is preserved but named for visibility.
        const int LadderBlockId = 152;

        int dz = dir;
        while (Index3d(x, y, z + dz, chunksize + 2, chunksize + 2) >= 0
            && Index3d(x, y, z + dz, chunksize + 2, chunksize + 2) < (chunksize + 2) * (chunksize + 2) * (chunksize + 2)
            && currentChunk[Index3d(x, y, z + dz, chunksize + 2, chunksize + 2)] == LadderBlockId)
        {
            int result = dz;
            if (GetBestLadderWall(x, y, z + dz, currentChunk) != -1)
            {
                return result;
            }

            dz += dir;
        }

        return 0;
    }

    /// <summary>
    /// Emits two triangles forming a quad into <paramref name="model"/>.
    /// Vertex order: v00=top-left, v01=top-right, v10=bottom-left, v11=bottom-right.
    /// <paramref name="flipWinding"/> reverses the winding for back-facing quads.
    /// </summary>
    private static void AddQuad(GeometryModel model,
        Vector3 v00, Vector3 v01, Vector3 v10, Vector3 v11,
        float uLeft, float uRight, float vTop, float vBottom,
        int color, bool flipWinding = false)
    {
        int e = model.VerticesCount;
        GeometryModel.AddVertex(model, v00.X, v00.Y, v00.Z, uLeft, vTop, color);
        GeometryModel.AddVertex(model, v01.X, v01.Y, v01.Z, uRight, vTop, color);
        GeometryModel.AddVertex(model, v10.X, v10.Y, v10.Z, uLeft, vBottom, color);
        GeometryModel.AddVertex(model, v11.X, v11.Y, v11.Z, uRight, vBottom, color);

        if (!flipWinding)
        {
            GeometryModel.AddIndex(model, e + 0);
            GeometryModel.AddIndex(model, e + 1);
            GeometryModel.AddIndex(model, e + 2);
            GeometryModel.AddIndex(model, e + 1);
            GeometryModel.AddIndex(model, e + 3);
            GeometryModel.AddIndex(model, e + 2);
        }
        else
        {
            GeometryModel.AddIndex(model, e + 1);
            GeometryModel.AddIndex(model, e + 0);
            GeometryModel.AddIndex(model, e + 2);
            GeometryModel.AddIndex(model, e + 3);
            GeometryModel.AddIndex(model, e + 1);
            GeometryModel.AddIndex(model, e + 2);
        }
    }

    private void AddTorch(int x, int y, int z, TorchType type, int tt, ChunkTessellationContext ctx)
    {
        int color = _colorWhite;
        const float sxy = 0.16f;

        float topx = 0.5f - (sxy / 2) + x;
        float topy = 0.5f - (sxy / 2) + y;
        float botx = topx;
        float boty = topy;

        if (type == TorchType.Front)
        {
            botx = x - sxy;
        }

        if (type == TorchType.Back)
        {
            botx = x + 1;
        }

        if (type == TorchType.Left)
        {
            boty = y - sxy;
        }

        if (type == TorchType.Right)
        {
            boty = y + 1;
        }

        float tz = z;
        Vector3 t00 = new(topx, tz + 0.9f, topy);
        Vector3 t01 = new(topx, tz + 0.9f, topy + sxy);
        Vector3 t10 = new(topx + sxy, tz + 0.9f, topy);
        Vector3 t11 = new(topx + sxy, tz + 0.9f, topy + sxy);

        // Tilt torch top toward the wall it leans on.
        if (type == TorchType.Left)
        {
            t01.Y -= 0.1f;
            t11.Y -= 0.1f;
        }

        if (type == TorchType.Right)
        {
            t10.Y -= 0.1f;
            t00.Y -= 0.1f;
        }

        if (type == TorchType.Front)
        {
            t10.Y -= 0.1f;
            t11.Y -= 0.1f;
        }

        if (type == TorchType.Back)
        {
            t01.Y -= 0.1f;
            t00.Y -= 0.1f;
        }

        Vector3 b00 = new(botx, tz, boty);
        Vector3 b01 = new(botx, tz, boty + sxy);
        Vector3 b10 = new(botx + sxy, tz, boty);
        Vector3 b11 = new(botx + sxy, tz, boty + sxy);

        // ── Shared texture coords ─────────────────────────────────────────────
        float sideTexrecTop = terrainTexturesPerAtlasInverse * (ctx.TorchSideTexture % TerrainTexturesPerAtlas);
        float sideTexrecBottom = sideTexrecTop + _texrecHeight;
        float topTexrecTop = terrainTexturesPerAtlasInverse * (ctx.TorchTopTexture % TerrainTexturesPerAtlas);
        float topTexrecBottom = topTexrecTop + _texrecHeight;

        GeometryModel mSide = GetModelData(tt, ctx.TorchSideTexture, ctx);
        GeometryModel mTop = GetModelData(tt, ctx.TorchTopTexture, ctx);

        // Top cap
        AddQuad(mTop, t00, t10, t01, t11, _texrecLeft, _texrecRight, topTexrecTop, topTexrecBottom, color);
        // Bottom cap (flipped winding)
        AddQuad(mSide, b00, b10, b01, b11, _texrecLeft, _texrecRight, sideTexrecTop, sideTexrecBottom, color, flipWinding: true);
        // Front face
        AddQuad(mSide, b00, b01, t00, t01, _texrecLeft, _texrecRight, sideTexrecBottom, sideTexrecTop, color);
        // Back face (flipped)
        AddQuad(mSide, b10, b11, t10, t11, _texrecRight, _texrecLeft, sideTexrecBottom, sideTexrecTop, color, flipWinding: true);
        // Left face
        AddQuad(mSide, b00, t00, b10, t10, _texrecRight, _texrecLeft, sideTexrecBottom, sideTexrecTop, color);
        // Right face (flipped)
        AddQuad(mSide, b01, t01, b11, t11, _texrecLeft, _texrecRight, sideTexrecBottom, sideTexrecTop, color, flipWinding: true);
    }

    private VerticesIndicesToLoad[] GetFinalVerticesIndices(
    int x, int y, int z,
    ChunkTessellationContext ctx,
    out int retCount)
    {
        retCount = 0;
        float posX = x * chunksize;
        float posY = y * chunksize;
        float posZ = z * chunksize;

        for (int i = 0; i < _atlasCount; i++)
        {
            if (ctx.Atlas[i].IndicesCount > 0)
            {
                ref VerticesIndicesToLoad v = ref ctx.ReturnBuffer[retCount];
                v.ModelData = ctx.Atlas[i];
                v.PositionX = posX;
                v.PositionY = posY;
                v.PositionZ = posZ;
                v.Texture = TerrainTextures1d[i % TerrainTexturesPerAtlas];
                v.Transparent = false;
                retCount++;
            }
        }

        for (int i = 0; i < _atlasCount; i++)
        {
            if (ctx.AtlasTransparent[i].IndicesCount > 0)
            {
                ref VerticesIndicesToLoad v = ref ctx.ReturnBuffer[retCount];
                v.ModelData = ctx.AtlasTransparent[i];
                v.PositionX = posX;
                v.PositionY = posY;
                v.PositionZ = posZ;
                v.Texture = TerrainTextures1d[i % TerrainTexturesPerAtlas];
                v.Transparent = true;
                retCount++;
            }
        }

        return ctx.ReturnBuffer;
    }

    public VerticesIndicesToLoad[] MakeChunk(
     int x, int y, int z,
     float[] lightLevels,
     ChunkTessellationContext ctx,
     out int retCount)
    {
        if (x < 0 || y < 0 || z < 0)
        {
            retCount = 0;
            return [];
        }

        if (!started)
        {
            throw new InvalidOperationException("Call Start() first.");
        }

        if (x >= mapsizex / chunksize
         || y >= mapsizey / chunksize
         || z >= mapsizez / chunksize)
        {
            retCount = 0;
            return [];
        }

        ctx.ResetGeometry();

        CalculateVisibleFaces(ctx.CurrentChunk, ctx);
        CalculateTilingCount(ctx.CurrentChunk, x * chunksize, y * chunksize, z * chunksize, ctx);
        BuildBlockPolygons(x, y, z, ctx.CurrentChunk, ctx.CurrentChunkShadows, lightLevels, ctx);
        return GetFinalVerticesIndices(x, y, z, ctx, out retCount);
    }
}

/// <summary>Bit-flags for sets of block faces.</summary>
[Flags]
public enum TileSideFlags
{
    None = 0,
    Top = 1 << 0,
    Bottom = 1 << 1,
    Right = 1 << 2,
    Left = 1 << 3,
    Front = 1 << 4,
    Back = 1 << 5,
}

/// <summary>
/// Converts a <see cref="TileSide"/> to the corresponding <see cref="TileSideFlags"/> bit.
/// </summary>
public static class TileSideExt
{
    public static TileSideFlags ToFlags(this TileSide side) => side switch
    {
        TileSide.Top => TileSideFlags.Top,
        TileSide.Bottom => TileSideFlags.Bottom,
        TileSide.Left => TileSideFlags.Front,
        TileSide.Right => TileSideFlags.Back,
        TileSide.Back => TileSideFlags.Left,
        TileSide.Front => TileSideFlags.Right,
        _ => TileSideFlags.None,
    };

    public const int Count = 6;
}

/// <summary>Slope type for a rail tile.</summary>
public enum RailSlope
{
    Flat = 0,
    TwoLeftRaised = 1,
    TwoRightRaised = 2,
    TwoUpRaised = 3,
    TwoDownRaised = 4,
}

/// <summary>
/// Per-block-type render flags packed into a single byte.
/// Replaces three separate <c>bool[]</c> arrays (istransparent, isLowered, isFluid),
/// cutting three allocations to one and improving cache locality.
/// </summary>
[Flags]
public enum BlockRenderFlags : byte
{
    None = 0,
    Transparent = 1 << 0,
    Lowered = 1 << 1,
    Fluid = 1 << 2,
}
