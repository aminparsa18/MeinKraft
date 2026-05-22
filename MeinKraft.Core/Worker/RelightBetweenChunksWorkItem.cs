namespace MeinKraft.Worker;

using System.Threading.Tasks;

/// <summary>
/// Partial relight — LightBetweenChunks only (BaseLight already updated by IncrementalLightBFS).
/// Used for runtime block changes that do not affect the sunlight heightmap.
/// </summary>
public record RelightBetweenChunksWorkItem(
    int ChunkX,
    int ChunkY,
    int ChunkZ,
    Chunk? Chunk = null,
    TaskCompletionSource? Completion = null,
    int Priority = 0
) : ChunkWorkItem(ChunkX, ChunkY, ChunkZ, ChunkWorkType.RelightBetweenChunks, Completion, Priority);
