using System.Buffers;
using System.Numerics;

/// <summary>
/// A flat (2D) block map stored as a sparse array of fixed-size chunks,
/// backed by <see cref="ArrayPool{T}"/> to eliminate per-chunk heap allocation.
/// Typically used for heightmap data that mirrors a <see cref="VoxelMap"/>.
/// </summary>
/// <remarks>
/// Chunks are allocated from <see cref="ArrayPool{T}.Shared"/> on first write
/// and returned to the pool by <see cref="ClearChunk"/> and <see cref="Restart"/>.
/// Unwritten chunks read as <c>default(T)</c> (zero for numeric types).
/// <para>
/// <b>Chunk size</b> must be a power of two. It is supplied at construction time
/// so the map can be configured per use-case without recompiling. The bit-shift
/// and mask constants are derived once in the constructor via
/// <see cref="BitOperations.TrailingZeroCount"/>.
/// </para>
/// <para>
/// <b>Pool contract:</b> rented arrays may be larger than requested.
/// Always use <c>_chunkArea</c> (= chunkSize²) as the element count,
/// never <c>rentedArray.Length</c>.
/// </para>
/// </remarks>
/// <typeparam name="T">
/// Element type. Use a value type (e.g. <see langword="int"/>,
/// <see langword="ushort"/>) to avoid per-element heap allocations.
/// </typeparam>
public class ChunkedMap2d<T>
{
    // ── Chunk geometry ────────────────────────────────────────────────────────

    /// <summary>Edge length of one chunk in blocks. Must be a power of two.</summary>
    private readonly int _chunkSize;

    /// <summary>log₂(<see cref="_chunkSize"/>) — used for bit-shift arithmetic.</summary>
    private readonly int _chunkSizeBits;

    /// <summary>Bitmask equivalent to <c>% _chunkSize</c> for non-negative integers.</summary>
    private readonly int _chunkSizeMask;

    /// <summary>Number of elements per chunk buffer (<see cref="_chunkSize"/>²).</summary>
    private readonly int _chunkArea;

    // ── Map state ─────────────────────────────────────────────────────────────

    /// <summary>Map width measured in chunks.</summary>
    private int _chunkGridWidth;

    /// <summary>
    /// Sparse array of pooled chunk buffers.
    /// A null slot means the chunk has never been written; reads return <c>default(T)</c>.
    /// </summary>
    private T[]?[] _chunks;

    // ── Construction ──────────────────────────────────────────────────────────

    /// <param name="mapSizeX">Map width in blocks. Must be a multiple of <paramref name="chunkSize"/>.</param>
    /// <param name="mapSizeY">Map height in blocks. Must be a multiple of <paramref name="chunkSize"/>.</param>
    /// <param name="chunkSize">
    /// Edge length of one chunk in blocks. Must be a power of two.
    /// Defaults to 16 to match <c>ServerMapStorage.ChunkSize</c>.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown if <paramref name="chunkSize"/> is not a positive power of two.
    /// </exception>
    public ChunkedMap2d(int mapSizeX, int mapSizeY, int chunkSize = 16)
    {
        if (chunkSize <= 0 || !BitOperations.IsPow2(chunkSize))
        {
            throw new ArgumentException("chunkSize must be a positive power of two.", nameof(chunkSize));
        }

        _chunkSize = chunkSize;
        _chunkSizeBits = BitOperations.TrailingZeroCount((uint)chunkSize);
        _chunkSizeMask = chunkSize - 1;
        _chunkArea = chunkSize * chunkSize;
        _chunks = [];

        Restart(mapSizeX, mapSizeY);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the block value at (<paramref name="x"/>, <paramref name="y"/>),
    /// or <c>default(T)</c> if the containing chunk has never been written.
    /// </summary>
    public T? GetBlock(int x, int y)
    {
        int ci = ChunkIndex(x, y);
        T[]? chunk = _chunks[ci];
        return chunk is null ? default : chunk[BlockIndex(x, y)];
    }

    /// <summary>
    /// Writes <paramref name="value"/> at (<paramref name="x"/>, <paramref name="y"/>),
    /// allocating a pooled chunk buffer on first write.
    /// </summary>
    public void SetBlock(int x, int y, T value)
        => GetOrAllocChunk(x, y)[BlockIndex(x, y)] = value;

    /// <summary>
    /// Returns the pooled data buffer for the chunk containing block
    /// (<paramref name="x"/>, <paramref name="y"/>), allocating from
    /// <see cref="ArrayPool{T}.Shared"/> on first access.
    /// </summary>
    /// <remarks>
    /// The returned array may be larger than <c>chunkSize²</c> — always
    /// index within <c>[0, chunkSize²)</c>.
    /// </remarks>
    public T[] GetOrAllocChunk(int x, int y)
    {
        int ci = ChunkIndex(x, y);
        if (_chunks[ci] is not null)
        {
            return _chunks[ci]!;
        }

        T[] rented = ArrayPool<T>.Shared.Rent(_chunkArea);
        // Rented arrays are not zeroed — initialise so unwritten reads return default(T).
        Array.Clear(rented, 0, _chunkArea);
        _chunks[ci] = rented;
        return rented;
    }

    /// <summary>
    /// Returns the pooled buffer for the chunk containing block
    /// (<paramref name="x"/>, <paramref name="y"/>) to <see cref="ArrayPool{T}.Shared"/>
    /// and marks the slot as unwritten.
    /// Subsequent reads from this chunk return <c>default(T)</c> until written again.
    /// </summary>
    public void ClearChunk(int x, int y)
    {
        int ci = ChunkIndex(x, y);
        if (_chunks[ci] is null)
        {
            return;
        }

        ArrayPool<T>.Shared.Return(_chunks[ci]!);
        _chunks[ci] = null;
    }

    /// <summary>
    /// Reinitialises the map for new dimensions, returning all live chunk buffers
    /// to <see cref="ArrayPool{T}.Shared"/> before allocating the new slot array.
    /// </summary>
    /// <param name="mapSizeX">New map width in blocks. Must be a multiple of <c>chunkSize</c>.</param>
    /// <param name="mapSizeY">New map height in blocks. Must be a multiple of <c>chunkSize</c>.</param>
    public void Restart(int mapSizeX, int mapSizeY)
    {
        // Return all live buffers before dropping the array
        if (_chunks is not null)
        {
            foreach (T[]? chunk in _chunks)
            {
                if (chunk is not null)
                {
                    ArrayPool<T>.Shared.Return(chunk);
                }
            }
        }

        _chunkGridWidth = mapSizeX >> _chunkSizeBits;
        int n = _chunkGridWidth * (mapSizeY >> _chunkSizeBits);
        _chunks = new T[]?[n];
    }

    // ── Coordinate helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Flat index of the chunk that contains block (<paramref name="x"/>, <paramref name="y"/>).
    /// Inlined bit-shift arithmetic — no helper call overhead on the hot path.
    /// </summary>
    private int ChunkIndex(int x, int y)
        => ((y >> _chunkSizeBits) * _chunkGridWidth) + (x >> _chunkSizeBits);

    /// <summary>
    /// Flat index within a chunk for block (<paramref name="x"/>, <paramref name="y"/>).
    /// Bitmask replaces modulo — equivalent for non-negative inputs.
    /// </summary>
    private int BlockIndex(int x, int y)
        => ((y & _chunkSizeMask) * _chunkSize) + (x & _chunkSizeMask);
}