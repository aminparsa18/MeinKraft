using MeinKraft;
using MemoryPack;
using System.Runtime.CompilerServices;

public class ServerMapStorage : IServerMapStorage
{
    private ServerChunk[][] chunks;
    private int mapSizeX;
    private int mapSizeY;
    private int mapSizeZ;

    // Cached chunk-grid width — BlockToChunk(mapSizeX).
    // Used by every GetChunkValid / SetChunkValid call to index into chunks[].
    // Recomputed once in Reset() and whenever ChunkSize changes; never mid-frame.
    private int _chunksX;

    private readonly IBlockRegistry _blockRegistry;
    private readonly IModEvents _modEvents;
    private readonly IChunkDbCompressed _chunkDb;


    public ServerMapStorage(IBlockRegistry blockRegistry, IModEvents modEvents, IChunkDbCompressed chunkDb, int chunkSize = 32)
    {
        _blockRegistry = blockRegistry;
        _modEvents = modEvents;
        _chunkDb = chunkDb;
        // Route through the property setter so chunksizebits, isPower2Chunk,
        // and _chunksX are always derived from the same source of truth.
        // Never assign chunksize directly — the three fields must move together.
        ChunkSize = chunkSize;
    }

    public int MapSizeX => mapSizeX;
    public int MapSizeY => mapSizeY;
    public int MapSizeZ => mapSizeZ;

    // ── Block access ──────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the block type at the given world position, or 0 (air) if the
    /// chunk is not yet loaded. Does NOT trigger chunk loading or world generation.
    /// Use GetChunk when load-on-demand is required.
    /// </summary>
    public int GetBlock(int x, int y, int z)
    {
        // GetChunk triggers DB lookup + world gen handlers on cache miss.
        // GetBlock is called extremely frequently (lighting, physics, rendering)
        // — those callers must not silently generate chunks as a side effect.
        ServerChunk chunk = GetChunkValid(BlockToChunk(x), BlockToChunk(y), BlockToChunk(z));
        return chunk == null
            ? 0
            : chunk.Data[VectorIndexUtil.Index3d(BlockInChunk(x), BlockInChunk(y), BlockInChunk(z), _chunksize, _chunksize)];
    }

    // ── Heightmap maintenance ─────────────────────────────────────────────────

    /// <summary>
    /// Rescans the entire column at (x, y) top-down to find the new surface block
    /// and updates the heightmap. Called after every SetBlock.
    /// </summary>
    private void UpdateColumnHeight(int x, int y)
    {
        int bx = BlockInChunk(x);
        int by = BlockInChunk(y);
        // BUG FIX: cx/cy were BlockInChunk (0–15) instead of BlockToChunk.
        // GetChunkValid received intra-chunk offsets as chunk coordinates,
        // so it always looked at the wrong chunk column.
        int cx = BlockToChunk(x);
        int cy = BlockToChunk(y);

        for (int i = mapSizeZ - 1; i >= 0; i--)
        {
            // BUG FIX: cz was BlockInChunk(i) — same category of error as cx/cy above.
            int cz = BlockToChunk(i);
            int bz = BlockInChunk(i);
            ServerChunk chunk = GetChunkValid(cx, cy, cz);
            if (chunk?.Data == null)
            {
                continue;
            }

            if (!BlockType.IsTransparentForLight(_blockRegistry.BlockTypes[chunk.Data[VectorIndexUtil.Index3d(bx, by, bz, _chunksize, _chunksize)]]))
            {
                Heightmap.SetBlock(x, y, (ushort)i);
                return;
            }
        }

        Heightmap.SetBlock(x, y, 0);
    }

    // ── Block mutation ────────────────────────────────────────────────────────

    public void SetBlockNotMakingDirty(int x, int y, int z, int tileType)
    {
        ServerChunk chunk = GetChunk(x, y, z);
        chunk.Data[VectorIndexUtil.Index3d(BlockInChunk(x), BlockInChunk(y), BlockInChunk(z), _chunksize, _chunksize)] = (ushort)tileType;
        chunk.DirtyForSaving = true;
        UpdateColumnHeight(x, y);
    }

    // ── Chunk loading ─────────────────────────────────────────────────────────

    public void LoadChunk(int cx, int cy, int cz)
    {
        ServerChunk chunk = GetChunkValid(cx, cy, cz);
        if (chunk == null)
        {
            GetChunk(cx * _chunksize, cy * _chunksize, cz * _chunksize);
        }
    }

    public ServerChunk GetChunkAt(int chunkx, int chunky, int chunkz)
        => GetChunk(chunkx * _chunksize, chunky * _chunksize, chunkz * _chunksize);

    /// <summary>
    /// Returns the chunk that contains world position (x, y, z), loading or
    /// generating it if not already in the cache.
    /// </summary>
    public ServerChunk GetChunk(int x, int y, int z)
    {
        x = BlockToChunk(x);
        y = BlockToChunk(y);
        z = BlockToChunk(z);

        ServerChunk chunk = GetChunkValid(x, y, z);
        if (chunk != null)
        {
            return chunk;
        }

        // Try loading from the database first.
        byte[] serializedChunk = ChunkDbHelper.GetChunk(_chunkDb, x, y, z);
        if (serializedChunk != null)
        {
            ServerChunk deserialized = DeserializeChunk(serializedChunk);
            SetChunkValid(x, y, z, deserialized);
            UpdateChunkHeight(x, y, z, deserialized.Data);
            // Return the local reference — no need to call GetChunkValid again.
            return deserialized;
        }

        // Not in DB — generate via mod handlers.
        ushort[] newData = new ushort[_chunksize * _chunksize * _chunksize];
        _modEvents.RaiseWorldGenerator(x, y, z, newData);

        ServerChunk newChunk = new ServerChunk { Data = newData, DirtyForSaving = true };
        SetChunkValid(x, y, z, newChunk);
        UpdateChunkHeight(x, y, z, newData);
        // Return the local reference — no need to call GetChunkValid again.
        return newChunk;
    }

    // ── Height scanning ───────────────────────────────────────────────────────

    /// <summary>
    /// Updates the heightmap for every column in the chunk after a chunk load.
    /// Accepts the chunk data directly to avoid re-fetching the chunk inside
    /// the chunksize² loop.
    /// </summary>
    private void UpdateChunkHeight(int x, int y, int z, ushort[] data)
    {
        int baseWorldX = x * _chunksize;
        int baseWorldY = y * _chunksize;
        int baseWorldZ = z * _chunksize;

        for (int xx = 0; xx < _chunksize; xx++)
        {
            for (int yy = 0; yy < _chunksize; yy++)
            {
                int inChunkHeight = GetColumnHeightInChunk(data, xx, yy);
                if (inChunkHeight != 0)
                {
                    int worldX = baseWorldX + xx;
                    int worldY = baseWorldY + yy;
                    int oldHeight = Heightmap.GetBlock(worldX, worldY);
                    Heightmap.SetBlock(worldX, worldY, (ushort)Math.Max(oldHeight, inChunkHeight + baseWorldZ));
                }
            }
        }
    }

    private int GetColumnHeightInChunk(ushort[] chunk, int xx, int yy)
    {
        int height = _chunksize - 1;
        for (int i = _chunksize - 1; i >= 0; i--)
        {
            height = i;
            if (!BlockType.IsTransparentForLight(_blockRegistry.BlockTypes[chunk[VectorIndexUtil.Index3d(xx, yy, i, _chunksize, _chunksize)]]))
            {
                break;
            }
        }

        return height;
    }

    private ServerChunk DeserializeChunk(byte[] serializedChunk)
        => MemoryPackSerializer.Deserialize<ServerChunk>(serializedChunk);

    // ── Chunk size ────────────────────────────────────────────────────────────

    // These three fields must always be in sync — never assign chunksize directly.
    // All initialization goes through the ChunkSize property setter.
    private int _chunksize;
    private int chunksizebits;
    private bool isPower2Chunk;
    public int ChunkSize
    {
        get => _chunksize;
        set
        {
            _chunksize = value;
            isPower2Chunk = (_chunksize & (_chunksize - 1)) == 0 && _chunksize != 0;
            chunksizebits = (int)Math.Log2(value);
            // _chunksX depends on chunksizebits — must be recomputed here.
            // mapSizeX may be 0 if Reset() hasn't been called yet; that's fine.
            _chunksX = BlockToChunk(mapSizeX);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int BlockInChunk(int num) => isPower2Chunk ? num & (_chunksize - 1) : num % _chunksize;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int BlockToChunk(int num) => num >> chunksizebits;

    // ── Map reset ─────────────────────────────────────────────────────────────

    public void Reset(int sizex, int sizey, int sizez)
    {
        mapSizeX = sizex;
        mapSizeY = sizey;
        mapSizeZ = sizez;
        _chunksX = BlockToChunk(sizex);
        chunks = new ServerChunk[_chunksX * BlockToChunk(sizey)][];
        Heightmap.Restart(MapSizeX, MapSizeY);
    }

    public ChunkedMap2d<ushort> Heightmap { get; set; }

    // ── Chunk slot access ─────────────────────────────────────────────────────

    public ServerChunk GetChunkValid(int cx, int cy, int cz)
    {
        ServerChunk[] column = chunks[VectorIndexUtil.Index2d(cx, cy, _chunksX)];
        return column == null || (uint)cz >= (uint)column.Length ? null : column[cz];
    }

    public void SetChunkValid(int cx, int cy, int cz, ServerChunk chunk)
    {
        int colIdx = VectorIndexUtil.Index2d(cx, cy, _chunksX);
        ServerChunk[] column = chunks[colIdx];
        if (column == null)
        {
            column = new ServerChunk[BlockToChunk(mapSizeZ)];
            chunks[colIdx] = column;
        }

        column[cz] = chunk;
    }

    public void Clear() => Array.Clear(chunks, 0, chunks.Length);
}
