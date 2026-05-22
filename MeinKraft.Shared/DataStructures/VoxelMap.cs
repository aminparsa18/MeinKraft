using System.Buffers;

public class VoxelMap : IVoxelMap
{
    public Chunk[]? Chunks { get; private set; }

    public int MapSizeX { get; set; }
    public int MapSizeY { get; set; }
    public int MapSizeZ { get; set; }

    // ── Cached chunk-grid dimensions ──────────────────────────────────────────
    // Computed once in Reset — eliminates repeated bit-shifts on every hot-path call.

    public ChunkedMap2d<int>? Heightmap { get; set; }

    public int Mapsizexchunks { get; private set; }
    public int Mapsizeychunks { get; private set; }
    public int Mapsizezchunks { get; private set; }

    // ── Constants (local aliases to avoid repeated static lookups) ────────────

    private const int CsBits = GameConstants.chunksizebits;
    private const int Cs = GameConstants.CHUNK_SIZE;
    private const int CsMask = Cs - 1;

    // ── Chunk flat-index helpers ───────────────────────────────────────────────

    /// <summary>
    /// Flat index into <see cref="Chunks"/> for chunk-grid coordinates.
    /// Inlined arithmetic — no helper call overhead on the hot path.
    /// </summary>
    public int ChunkFlatIndex(int cx, int cy, int cz)
        => (cz * Mapsizexchunks * Mapsizeychunks) + (cy * Mapsizexchunks) + cx;

    /// <summary>Flat index within a chunk for block-local coordinates.</summary>
    private static int BlockFlatIndex(int lx, int ly, int lz)
        => (lz << CsBits << CsBits) + (ly << CsBits) + lx;

    // ── Core block access ─────────────────────────────────────────────────────

    /// <inheritdoc/>
    public int GetBlockValid(int x, int y, int z)
    {
        int ci = ChunkFlatIndex(x >> CsBits, y >> CsBits, z >> CsBits);
        Chunk chunk = Chunks[ci];
        return chunk == null ? 0 : chunk.GetBlock(BlockFlatIndex(x & CsMask, y & CsMask, z & CsMask));
    }

    /// <inheritdoc/>
    public int GetBlock(int x, int y, int z)
        => IsValidPos(x, y, z) ? GetBlockValid(x, y, z) : 0;

    /// <inheritdoc/>
    public void SetBlockRaw(int x, int y, int z, int tileType)
    {
        Chunk chunk = GetChunk(x, y, z);
        chunk.SetBlock(BlockFlatIndex(x & CsMask, y & CsMask, z & CsMask), tileType);
    }

    // ── Chunk access ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public Chunk GetChunk(int x, int y, int z)
    {
        if (x < 0 || y < 0 || z < 0)
        {
            Console.WriteLine($"[WARN] GetChunk negative input: ({x}, {y}, {z})");
        }

        return GetChunkAt(x >> CsBits, y >> CsBits, z >> CsBits);
    }

    /// <inheritdoc/>
    public Chunk GetChunkAt(int cx, int cy, int cz)
    {
        int ci = ChunkFlatIndex(cx, cy, cz);
        Chunk chunk = Chunks[ci];

        if (chunk != null)
        {
            return chunk;
        }

        const int n = Cs * Cs * Cs;
        byte[] data = ArrayPool<byte>.Shared.Rent(n);
        byte[] baseLight = ArrayPool<byte>.Shared.Rent(n);
        data.AsSpan(0, n).Clear();
        baseLight.AsSpan(0, n).Clear();

        chunk = new Chunk { Data = data, BaseLight = baseLight };
        Chunks[ci] = chunk;
        return chunk;
    }

    // ── Map lifecycle ─────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void Reset(int sizex, int sizey, int sizez)
    {
        if (Chunks != null)
        {
            for (int i = 0; i < Chunks.Length; i++)
            {
                Chunks[i]?.Release();
                Chunks[i] = null;
            }
        }

        MapSizeX = sizex;
        MapSizeY = sizey;
        MapSizeZ = sizez;

        Mapsizexchunks = sizex >> CsBits;
        Mapsizeychunks = sizey >> CsBits;
        Mapsizezchunks = sizez >> CsBits;

        Chunks = new Chunk[Mapsizexchunks * Mapsizeychunks * Mapsizezchunks];
    }

    // ── Bulk map operations ───────────────────────────────────────────────────

    /// <inheritdoc/>
    public void GetMapPortion(int[] outPortion, int x, int y, int z, int portionsizex, int portionsizey, int portionsizez)
    {
        int totalChunks = Mapsizexchunks * Mapsizeychunks * Mapsizezchunks;

        Array.Clear(outPortion, 0, portionsizex * portionsizey * portionsizez);

        int startCX = x >> CsBits;
        int startCY = y >> CsBits;
        int startCZ = z >> CsBits;
        int endCX = (x + portionsizex - 1) >> CsBits;
        int endCY = (y + portionsizey - 1) >> CsBits;
        int endCZ = (z + portionsizez - 1) >> CsBits;

        for (int cz = startCZ; cz <= endCZ; cz++)
        {
            int chunkGlobalZ = cz << CsBits;
            int blockZ0 = Math.Max(z, chunkGlobalZ);
            int blockZ1 = Math.Min(z + portionsizez, chunkGlobalZ + Cs);

            for (int cy = startCY; cy <= endCY; cy++)
            {
                int chunkGlobalY = cy << CsBits;
                int blockY0 = Math.Max(y, chunkGlobalY);
                int blockY1 = Math.Min(y + portionsizey, chunkGlobalY + Cs);

                for (int cx = startCX; cx <= endCX; cx++)
                {
                    int ci = ChunkFlatIndex(cx, cy, cz);
                    if ((uint)ci >= (uint)totalChunks)
                    {
                        continue;
                    }

                    Chunk chunk = Chunks[ci];
                    if (chunk == null || !chunk.HasData())
                    {
                        continue;
                    }

                    int chunkGlobalX = cx << CsBits;
                    int blockX0 = Math.Max(x, chunkGlobalX);
                    int blockX1 = Math.Min(x + portionsizex, chunkGlobalX + Cs);

                    // Hoist Z and Y row base indices outside the innermost loop.
                    for (int bz = blockZ0; bz < blockZ1; bz++)
                    {
                        int inChunkZ = bz - chunkGlobalZ;
                        int outZ = bz - z;
                        int chunkZRow = inChunkZ << CsBits << CsBits; // inChunkZ * Cs * Cs
                        int outZRow = outZ * portionsizey;

                        for (int by = blockY0; by < blockY1; by++)
                        {
                            int inChunkY = by - chunkGlobalY;
                            int outY = by - y;
                            int chunkBase = chunkZRow + (inChunkY << CsBits); // + inChunkY * Cs
                            int outBase = (outZRow + outY) * portionsizex;

                            for (int bx = blockX0; bx < blockX1; bx++)
                            {
                                outPortion[outBase + (bx - x)] =
                                    chunk.GetBlock(chunkBase + (bx - chunkGlobalX));
                            }
                        }
                    }
                }
            }
        }
    }

    /// <inheritdoc/>
    public void SetMapPortion(int x, int y, int z, int[] source, int sizeX, int sizeY, int sizeZ)
    {
        int csBits = CsBits;
        int chunksX = sizeX >> csBits;
        int chunksY = sizeY >> csBits;
        int chunksZ = sizeZ >> csBits;

        // Collect chunks that need dirtying — deduplicated by flat map index.
        // Two sets: one for block-changed chunks, one for neighbor-only dirty.
        // Using HashSet avoids the ~6.5× redundant SetChunkDirty calls seen in profiling.
        HashSet<int> blocksChanged = new(chunksX * chunksY * chunksZ);
        HashSet<int> neighborDirty = new(chunksX * chunksY * chunksZ * 6);

        for (int cx = 0; cx < chunksX; cx++)
        {
            int worldX = x + (cx << csBits);
            int srcX = cx << csBits;

            for (int cy = 0; cy < chunksY; cy++)
            {
                int worldY = y + (cy << csBits);
                int srcY = cy << csBits;

                for (int cz = 0; cz < chunksZ; cz++)
                {
                    int worldZ = z + (cz << csBits);
                    int srcZ = cz << csBits;

                    Chunk c = GetChunk(worldX, worldY, worldZ);
                    FillChunk(c, srcX, srcY, srcZ, source, sizeX, sizeY, sizeZ);

                    int ccx = worldX >> csBits;
                    int ccy = worldY >> csBits;
                    int ccz = worldZ >> csBits;

                    // Mark the written chunk
                    _ = blocksChanged.Add(ChunkFlatIndex(ccx, ccy, ccz));

                    // Collect its 6 neighbors
                    CollectNeighbor(ccx - 1, ccy, ccz, neighborDirty);
                    CollectNeighbor(ccx + 1, ccy, ccz, neighborDirty);
                    CollectNeighbor(ccx, ccy - 1, ccz, neighborDirty);
                    CollectNeighbor(ccx, ccy + 1, ccz, neighborDirty);
                    CollectNeighbor(ccx, ccy, ccz - 1, neighborDirty);
                    CollectNeighbor(ccx, ccy, ccz + 1, neighborDirty);
                }
            }
        }

        // Single dirty pass — each chunk touched at most once.
        foreach (int ci in blocksChanged)
        {
            SetChunkDirtyAt(ci, dirty: true, blocksChanged: true);
        }

        foreach (int ci in neighborDirty)
        {
            if (!blocksChanged.Contains(ci))
            {
                SetChunkDirtyAt(ci, dirty: true, blocksChanged: false);
            }
        }
    }

    // ── Dirty marking ─────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void SetChunkDirty(int cx, int cy, int cz, bool dirty, bool blockschanged)
    {
        if (!IsValidChunkPos(cx, cy, cz))
        {
            return;
        }

        SetChunkDirtyAt(ChunkFlatIndex(cx, cy, cz), dirty, blockschanged);
    }

    /// <summary>
    /// Marks a chunk dirty by its pre-computed flat index.
    /// Caller is responsible for ensuring the index is valid.
    /// </summary>
    private void SetChunkDirtyAt(int ci, bool dirty, bool blocksChanged)
    {
        Chunk c = Chunks[ci];
        if (c == null)
        {
            return;
        }

        c.Rendered ??= new RenderedChunk();
        c.Rendered.Dirty = dirty;
        if (blocksChanged)
        {
            c.BaseLightDirty = true;
        }
    }

    /// <inheritdoc/>
    public void SetChunksAroundDirty(int cx, int cy, int cz)
    {
        SetChunkDirty(cx - 1, cy, cz, true, false);
        SetChunkDirty(cx + 1, cy, cz, true, false);
        SetChunkDirty(cx, cy - 1, cz, true, false);
        SetChunkDirty(cx, cy + 1, cz, true, false);
        SetChunkDirty(cx, cy, cz - 1, true, false);
        SetChunkDirty(cx, cy, cz + 1, true, false);
    }

    /// <inheritdoc/>
    public void SetBlockDirty(int x, int y, int z)
    {
        Span<(int x, int y, int z)> offsets =
        [
            (x,     y,     z    ),
            (x - 1, y,     z    ),
            (x + 1, y,     z    ),
            (x,     y - 1, z    ),
            (x,     y + 1, z    ),
            (x,     y,     z - 1),
            (x,     y,     z + 1),
        ];

        for (int i = 0; i < offsets.Length; i++)
        {
            (int px, int py, int pz) = offsets[i];
            if ((uint)px >= (uint)MapSizeX ||
                (uint)py >= (uint)MapSizeY ||
                (uint)pz >= (uint)MapSizeZ)
            {
                continue;
            }

            SetChunkDirty(px >> CsBits, py >> CsBits, pz >> CsBits, true, true);
        }
    }

    // ── Validation ────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public bool IsValidPos(int x, int y, int z)
        => (uint)x < (uint)MapSizeX &&
           (uint)y < (uint)MapSizeY &&
           (uint)z < (uint)MapSizeZ;

    /// <inheritdoc/>
    public bool IsValidChunkPos(int cx, int cy, int cz)
        => (uint)cx < (uint)Mapsizexchunks &&
           (uint)cy < (uint)Mapsizeychunks &&
           (uint)cz < (uint)Mapsizezchunks;

    // ── Light ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public int MaybeGetLight(int x, int y, int z)
    {
        if (!IsValidPos(x, y, z))
        {
            return -1;
        }

        int cx = x >> CsBits;
        int cy = y >> CsBits;
        int cz = z >> CsBits;
        if (!IsValidChunkPos(cx, cy, cz))
        {
            return -1;
        }

        Chunk c = Chunks[ChunkFlatIndex(cx, cy, cz)];
        if (c?.Rendered?.Light == null)
        {
            return -1;
        }

        const int lightCS = Cs + 2;
        int lx = (x & CsMask) + 1;
        int ly = (y & CsMask) + 1;
        int lz = (z & CsMask) + 1;
        return c.Rendered.Light[(lz * lightCS * lightCS) + (ly * lightCS) + lx];
    }

    // ── Render state ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public bool IsChunkRendered(int cx, int cy, int cz)
    {
        Chunk c = Chunks[ChunkFlatIndex(cx, cy, cz)];
        return c?.Rendered?.Ids != null;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Adds the flat index of the neighbor chunk at (<paramref name="cx"/>,
    /// <paramref name="cy"/>, <paramref name="cz"/>) to <paramref name="set"/>
    /// if the position is within the map.
    /// </summary>
    private void CollectNeighbor(int cx, int cy, int cz, HashSet<int> set)
    {
        if (IsValidChunkPos(cx, cy, cz))
        {
            _ = set.Add(ChunkFlatIndex(cx, cy, cz));
        }
    }

    /// <summary>
    /// Copies a chunk-sized sub-cube from <paramref name="source"/> into
    /// <paramref name="destination"/>, using the source offset
    /// (<paramref name="srcX"/>, <paramref name="srcY"/>, <paramref name="srcZ"/>).
    /// </summary>
    private static void FillChunk(
        Chunk destination,
        int srcX, int srcY, int srcZ,
        int[] source, int srcSizeX, int srcSizeY, int srcSizeZ)
    {
        for (int z = 0; z < Cs; z++)
        {
            int srcZRow = (z + srcZ) * srcSizeY;
            int dstZRow = z << CsBits << CsBits; // z * Cs * Cs

            for (int y = 0; y < Cs; y++)
            {
                int srcBase = ((srcZRow + y + srcY) * srcSizeX) + srcX;
                int dstBase = dstZRow + (y << CsBits);

                for (int x = 0; x < Cs; x++)
                {
                    destination.SetBlock(dstBase + x, source[srcBase + x]);
                }
            }
        }
    }
}