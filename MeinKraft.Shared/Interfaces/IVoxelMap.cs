/// <summary>
/// Represents the voxel world, storing block data in a sparse grid of <see cref="Chunk"/> objects.
/// Block-space coordinates are in individual block units; chunk coordinates are derived by
/// dividing by <see cref="GameConstants.CHUNK_SIZE"/> (or right-shifting by <see cref="GameConstants.chunksizebits"/>).
/// </summary>
public interface IVoxelMap
{
    Chunk[] Chunks { get; }

    /// <summary>Width of the map measured in chunks.</summary>
    int Mapsizexchunks { get; }

    /// <summary>Height of the map measured in chunks.</summary>
    int Mapsizeychunks { get; }

    /// <summary>Depth of the map measured in chunks.</summary>
    int Mapsizezchunks { get; }

    int MapSizeX { get; set; }
    int MapSizeY { get; set; }
    int MapSizeZ { get; set; }

    /// <summary>Cached surface height map.</summary>
    ChunkedMap2d<int> Heightmap { get; set; }

    /// <summary>
    /// Returns the block type at the given block-space position,
    /// or 0 (air) when the position is outside map bounds.
    /// </summary>
    int GetBlock(int x, int y, int z);

    /// <summary>
    /// Returns the block type at the given block-space position without performing map-bounds validation.
    /// Returns 0 (air) if the owning chunk has not been allocated yet.
    /// Use <see cref="GetBlock"/> for safe access that also checks bounds.
    /// </summary>
    int GetBlockValid(int x, int y, int z);

    /// <summary>
    /// Returns the chunk that contains the block at block-space coordinates,
    /// allocating a new chunk if one does not already exist.
    /// </summary>
    Chunk GetChunk(int x, int y, int z);

    /// <summary>
    /// Returns the chunk at the given chunk-space coordinates, allocating a new one if absent.
    /// Backing arrays are rented from <see cref="ArrayPool{T}.Shared"/> and zeroed before use
    /// so the caller always receives clean, initialised memory.
    /// </summary>
    Chunk GetChunkAt(int cx, int cy, int cz);

    /// <summary>
    /// Reads a rectangular sub-region of the map into a flat array.
    /// Unallocated or out-of-bounds positions are written as 0 (air).
    /// Output array layout: <c>index = (z * sizeY + y) * sizeX + x</c>.
    /// </summary>
    void GetMapPortion(int[] outPortion, int x, int y, int z, int portionsizex, int portionsizey, int portionsizez);

    /// <summary>
    /// Returns true when the chunk has been rendered at least once
    /// (i.e. its render mesh exists and contains vertex/index data).
    /// </summary>
    bool IsChunkRendered(int cx, int cy, int cz);

    /// <summary>Returns true when chunk-space coordinates fall within map bounds.</summary>
    bool IsValidChunkPos(int cx, int cy, int cz);

    /// <summary>Returns true when block-space coordinates fall within map bounds.</summary>
    bool IsValidPos(int x, int y, int z);

    /// <summary>
    /// Attempts to read the baked light value at a block-space position.
    /// Returns -1 if the position is invalid, the chunk is unallocated,
    /// or the chunk's light buffer has not been computed yet.
    /// </summary>
    int MaybeGetLight(int x, int y, int z);

    /// <summary>
    /// Reinitialises the map with new dimensions, discarding all existing chunk data.
    /// All sizes must be exact multiples of <see cref="GameConstants.CHUNK_SIZE"/>.
    /// </summary>
    /// <remarks>
    /// Every live chunk's pooled arrays are returned to <see cref="ArrayPool{T}.Shared"/>
    /// before the chunk array is replaced, preventing pool leaks on world reload.
    /// </remarks>
    void Reset(int sizex, int sizey, int sizez);

    /// <summary>
    /// Marks the chunk containing the given block and all chunks containing its 6 face-adjacent
    /// neighbours as dirty with light invalidation. Call after any single-block edit.
    /// </summary>
    void SetBlockDirty(int x, int y, int z);

    /// <summary>
    /// Writes a block type directly into the map without marking any chunks dirty.
    /// Prefer this during bulk operations such as world generation to avoid redundant re-render triggers.
    /// For interactive single-block edits, dirty-marking must be handled separately via <see cref="SetBlockDirty"/>.
    /// </summary>
    void SetBlockRaw(int x, int y, int z, int tileType);

    /// <summary>
    /// Marks the chunk at chunk-space coordinates as dirty, optionally flagging that its block
    /// data has changed (which also invalidates base lighting via <see cref="Chunk.BaseLightDirty"/>).
    /// Does nothing if the position is out of range or the chunk is null.
    /// </summary>
    void SetChunkDirty(int cx, int cy, int cz, bool dirty, bool blockschanged);

    /// <summary>
    /// Marks the six face-adjacent neighbour chunks of the chunk at the given chunk-space coordinates
    /// as dirty for re-rendering (without invalidating lighting).
    /// </summary>
    void SetChunksAroundDirty(int cx, int cy, int cz);

    /// <summary>
    /// Writes a rectangular block of data into the map and marks all affected chunks
    /// (and their neighbours) dirty for re-rendering.
    /// Input array layout must match <see cref="GetMapPortion"/>.
    /// </summary>
    void SetMapPortion(int x, int y, int z, int[] chunk, int sizeX, int sizeY, int sizeZ);

    int ChunkFlatIndex(int cx, int cy, int cz);
}