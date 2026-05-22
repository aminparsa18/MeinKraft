namespace MeinKraft.Worker;

/// <summary>
/// Describes a single unit of background chunk work.
/// Kept as a record so callers can deconstruct and pattern-match cleanly.
/// </summary>
public record ChunkWorkItem(
    int ChunkX,
    int ChunkY,
    int ChunkZ,
    ChunkWorkType Type,
    TaskCompletionSource? Completion = null,   // optional — lets callers await a specific item
    int Priority = 0
);