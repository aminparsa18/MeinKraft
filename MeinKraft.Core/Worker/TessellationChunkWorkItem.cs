namespace MeinKraft.Worker;

/// <summary>
/// Tessellation-specific work item. Carries the pre-baked shadow snapshot
/// and the chunk reference so the worker thread never touches lighting state.
/// <see cref="ChunkWorkType.Tessellate"/> is baked into the base constructor
/// call — a mismatched Type is structurally impossible.
/// </summary>
public record TessellationChunkWorkItem(
    int ChunkX,
    int ChunkY,
    int ChunkZ,
    Chunk Chunk,
    /// <summary>
    /// ArrayPool-rented 18³ (5 832-byte) snapshot of rendered.Light,
    /// computed on the main thread before enqueue. Read-only on the worker.
    /// The worker returns it to the pool after MakeChunk, in all paths.
    /// </summary>
    byte[] ShadowBuffer,
    bool ShadowBufferRented,
    TaskCompletionSource? Completion = null,
    int Priority = 0
) : ChunkWorkItem(ChunkX, ChunkY, ChunkZ, ChunkWorkType.Tessellate, Completion);