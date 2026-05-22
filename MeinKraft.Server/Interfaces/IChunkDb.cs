using OpenTK.Mathematics;

/// <summary>
/// Abstraction over all chunk storage backends (file, database, in-memory, etc.).
/// Implementations are responsible for serialization, persistence, and thread safety.
/// </summary>
public interface IChunkDb
{
    /// <summary>Opens or creates the storage at the given path.</summary>
    /// <param name="filename">Path to the storage file or directory.</param>
    void Open(string filename);

    /// <summary>Copies the current storage contents to <paramref name="backupFilename"/>.</summary>
    /// <param name="backupFilename">Destination path for the backup.</param>
    void Backup(string backupFilename);

    /// <summary>
    /// Retrieves serialized chunk data for each position in <paramref name="chunkpositions"/>.
    /// Positions with no stored data are omitted from the result.
    /// </summary>
    /// <param name="chunkpositions">Chunk-space coordinates to fetch.</param>
    /// <returns>Serialized byte arrays in the same order as the requested positions.</returns>
    IEnumerable<byte[]> GetChunks(IEnumerable<Vector3i> chunkpositions);

    /// <summary>
    /// Persists serialized chunk data for each entry in <paramref name="chunks"/>.
    /// Existing data at the same position is overwritten.
    /// </summary>
    void SetChunks(IEnumerable<DbChunk> chunks);

    /// <summary>Permanently removes the chunks at the given chunk-space positions.</summary>
    void DeleteChunks(IEnumerable<Vector3i> chunkpositions);

    /// <summary>
    /// Returns the global (non-chunk) data blob, or <see langword="null"/> if none has been stored.
    /// </summary>
    byte[] GetGlobalData();

    /// <summary>Persists a global data blob, replacing any previously stored value.</summary>
    void SetGlobalData(byte[] data);

    /// <summary>
    /// Retrieves serialized chunk data from a specific file rather than the primary storage,
    /// keyed by chunk-space position.
    /// </summary>
    /// <param name="chunkpositions">Chunk-space coordinates to fetch.</param>
    /// <param name="filename">Source file to query.</param>
    /// <returns>Dictionary mapping each found position to its serialized bytes.</returns>
    Dictionary<Vector3i, byte[]> GetChunksFromFile(IEnumerable<Vector3i> chunkpositions, string filename);

    /// <summary>
    /// Writes serialized chunk data to a specific file rather than the primary storage.
    /// </summary>
    /// <param name="chunks">Chunks to write.</param>
    /// <param name="filename">Destination file path.</param>
    void SetChunksToFile(IEnumerable<DbChunk> chunks, string filename);

    /// <summary>Returns <see langword="true"/> if this storage is in read-only mode.</summary>
    bool ReadOnly { get; set; }

}