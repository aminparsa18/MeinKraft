using System.Buffers;

/// <summary>
/// Stores block data for a single chunk of the voxel map.
/// Block data is stored as <see cref="byte"/> until a block value of 255 or greater is set,
/// at which point the storage is transparently promoted to <see cref="int"/> to accommodate
/// the larger value.
/// </summary>
/// <remarks>
/// All backing arrays (<see cref="Data"/>, <see cref="dataInt"/>, <see cref="BaseLight"/>)
/// are rented from <see cref="ArrayPool{T}.Shared"/> and must be returned by calling
/// <see cref="Release"/> before the chunk reference is discarded.
/// </remarks>
public class Chunk
{
    /// <summary>Total number of blocks in a full chunk volume (ChunkSide³).</summary>
    private static readonly int ChunkVolume = GameConstants.CHUNK_SIZE * GameConstants.CHUNK_SIZE * GameConstants.CHUNK_SIZE;

    /// <summary>
    /// Expanded int storage, allocated on demand when a block value ≥ 255 is written.
    /// When non-null, <see cref="Data"/> is null (and has been returned to the pool).
    /// </summary>
    private int[]? dataInt;

    // ── Backing stores ───────────────────────────────────────────────────────
    // Exactly one of data/dataInt is active at any time; the other is null.

    /// <summary>Compact byte storage used when all block values are below 255.</summary>
    public byte[]? Data { get; set; }

    /// <summary>Per-block base light levels for this chunk.</summary>
    public byte[]? BaseLight { get; set; }

    /// <summary>The last rendered state of this chunk, used by the renderer.</summary>
    public RenderedChunk? Rendered { get; set; }

    // ── BaseLightDirty — atomic so multiple lighting workers never double-compute ──

    /// <summary>
    /// Backing field for <see cref="BaseLightDirty"/>.
    /// 1 = dirty, 0 = clean. Use <see cref="Interlocked"/> for safe multi-worker access.
    /// </summary>
    private int _baseLightDirty = 1;

    /// <summary>
    /// Whether <see cref="BaseLight"/> needs to be recalculated.
    /// Safe to read/write from the main thread (single writer).
    /// Lighting workers must use <see cref="TryClaimBaseLightDirty"/> instead.
    /// </summary>
    public bool BaseLightDirty
    {
        get => _baseLightDirty == 1;
        set => _baseLightDirty = value ? 1 : 0;
    }

    /// <summary>
    /// Atomically checks and clears the dirty flag.
    /// Returns <see langword="true"/> if this call successfully claimed the dirty
    /// work — the caller must then run <c>LightBase.CalculateChunkBaseLight</c>.
    /// Returns <see langword="false"/> if another lighting worker already claimed it.
    /// </summary>
    public bool TryClaimBaseLightDirty() =>
        Interlocked.Exchange(ref _baseLightDirty, 0) == 1;

    // ── Block accessors ───────────────────────────────────────────────────────

    /// <summary>
    /// Returns the block value at the given flat index within this chunk.
    /// Reads from whichever backing store is currently active.
    /// </summary>
    /// <param name="pos">Flat index into the chunk's block array.</param>
    public int GetBlock(int pos) => dataInt != null ? dataInt[pos] : Data[pos];

    /// <summary>
    /// Sets the block value at the given flat index within this chunk.
    /// If <paramref name="block"/> is ≥ 255 and the chunk is using byte storage,
    /// the byte array is returned to the pool and storage is promoted to a rented int array.
    /// </summary>
    /// <param name="pos">Flat index into the chunk's block array.</param>
    /// <param name="block">The block type to store.</param>
    public void SetBlock(int pos, int block)
    {
        if (dataInt != null)
        {
            dataInt[pos] = block;
            return;
        }

        if (block <= 255)
        {
            Data[pos] = (byte)block;
            return;
        }

        // ── Promote byte storage → int storage ───────────────────────────────
        // Rent a new int array at least ChunkVolume in size.
        int n = ChunkVolume;
        int[] promoted = ArrayPool<int>.Shared.Rent(n);
        Buffer.BlockCopy(Data, 0, promoted, 0, n);

        // Return the now-redundant byte array to the pool.
        ArrayPool<byte>.Shared.Return(Data);
        Data = null;

        dataInt = promoted;
        dataInt[pos] = block;
    }

    /// <summary>
    /// Returns <see langword="true"/> if this chunk has been populated with block data.
    /// A chunk with no data is considered empty/unloaded.
    /// </summary>
    public bool HasData() => Data != null || dataInt != null;

    /// <summary>
    /// Copies <see cref="BaseLight"/> into <paramref name="destination"/> (first
    /// <paramref name="length"/> bytes) under <see cref="BaseLightLock"/>.
    /// If <see cref="BaseLight"/> is null, <paramref name="destination"/> is zeroed.
    /// The caller is responsible for renting and returning <paramref name="destination"/>.
    /// <paramref name="chunkIndex"/> is the flat map index, used by the debug race detector;
    /// pass -1 to skip detector instrumentation.
    /// </summary>
    public void SnapshotBaseLight(byte[] destination, int length, int chunkIndex = -1)
    {
        if (BaseLight != null)
        {
            BaseLight.AsSpan(0, length).CopyTo(destination.AsSpan(0, length));
        }
        else
        {
            destination.AsSpan(0, length).Fill(0);
        }
    }

    /// <summary>
    /// Returns all pooled arrays back to <see cref="ArrayPool{T}.Shared"/> and nulls
    /// every reference so the chunk slot can safely be set to null by the caller.
    /// Must be called before discarding a chunk reference to avoid leaking pool memory.
    /// </summary>
    /// <remarks>
    /// It is safe to call <see cref="Release"/> on a chunk that was never fully
    /// initialised (e.g. if data was already null); each guard is checked individually.
    /// </remarks>
    public void Release()
    {
        if (Data != null)
        {
            ArrayPool<byte>.Shared.Return(Data);
            Data = null;
        }

        if (dataInt != null)
        {
            ArrayPool<int>.Shared.Return(dataInt);
            dataInt = null;
        }

        if (BaseLight != null)
        {
            ArrayPool<byte>.Shared.Return(BaseLight);
            BaseLight = null;
        }

        Rendered?.ReleaseLight();
        Rendered = null;
    }
}