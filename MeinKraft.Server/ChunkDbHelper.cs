using OpenTK.Mathematics;

/// Pairs a chunk's 3-D position with its serialized byte payload for database I/O.
/// </summary>
public struct DbChunk
{
    /// <summary>Chunk-space coordinates of this chunk.</summary>
    public Vector3i Position;

    /// <summary>Serialized block data for this chunk.</summary>
    public byte[] Chunk;
}

/// <summary>
/// Thin helper layer over <see cref="IChunkDb"/> that provides single-chunk
/// convenience methods and validates that batch queries return at most one result.
/// </summary>
public static class ChunkDbHelper
{
    /// <summary>
    /// Retrieves the serialized data for the chunk at chunk-space coordinates
    /// (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>).
    /// </summary>
    /// <returns>The serialized chunk bytes, or <see langword="null"/> if not found.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the database returns more than one result for a single-key query.
    /// </exception>
    public static byte[] GetChunk(IChunkDb db, int x, int y, int z)
    {
        List<byte[]> chunks = [.. db.GetChunks([new Vector3i(x, y, z)])];
        return chunks.Count > 1
            ? throw new InvalidOperationException($"Expected at most 1 chunk at ({x},{y},{z}), got {chunks.Count}.")
            : chunks.Count == 0 ? null : chunks[0];
    }

    /// <summary>
    /// Persists the serialized chunk data at chunk-space coordinates
    /// (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>).
    /// </summary>
    public static void SetChunk(IChunkDb db, int x, int y, int z, byte[] data)
        => db.SetChunks([new DbChunk { Position = new Vector3i(x, y, z), Chunk = data }]);

    /// <summary>
    /// Deletes the chunk at chunk-space coordinates
    /// (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>).
    /// </summary>
    public static void DeleteChunk(IChunkDb db, int x, int y, int z)
        => db.DeleteChunks([new Vector3i(x, y, z)]);

    /// <summary>
    /// Deletes all chunks at the given chunk-space positions.
    /// </summary>
    public static void DeleteChunks(IChunkDb db, List<Vector3i> positions)
        => db.DeleteChunks([.. positions]);

    /// <summary>
    /// Retrieves a batch of chunks from a specific file, keyed by their positions.
    /// </summary>
    /// <param name="positions">Chunk-space coordinates to fetch.</param>
    /// <param name="filename">Source file to query.</param>
    /// <returns>Dictionary mapping each position to its serialized chunk bytes.</returns>
    public static Dictionary<Vector3i, byte[]> GetChunksFromFile(IChunkDb db, List<Vector3i> positions, string filename)
        => db.GetChunksFromFile([.. positions], filename);

    /// <summary>
    /// Retrieves the serialized data for a single chunk from a specific file.
    /// </summary>
    /// <returns>The serialized chunk bytes, or <see langword="null"/> if not found.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the database returns more than one result for a single-key query.
    /// </exception>
    public static byte[] GetChunkFromFile(IChunkDb db, int x, int y, int z, string filename)
    {
        Vector3i key = new(x, y, z);
        Dictionary<Vector3i, byte[]> chunks = db.GetChunksFromFile([key], filename);
        if (chunks.Count > 1)
        {
            throw new InvalidOperationException($"Expected at most 1 chunk at ({x},{y},{z}), got {chunks.Count}.");
        }

        return chunks.TryGetValue(key, out byte[] data) ? data : null;
    }
}
