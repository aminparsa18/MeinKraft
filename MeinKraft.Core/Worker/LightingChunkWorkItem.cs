namespace MeinKraft.Worker;

using System.Threading.Tasks;

/// <summary>
/// Enqueued by the main thread (ModDrawTerrain) when a chunk is dirty.
/// The ChunkLightingWorker converts this into a TessellationChunkWorkItem
/// after computing shadows.
/// </summary>
public record LightingChunkWorkItem(
    int ChunkX,
    int ChunkY,
    int ChunkZ,
    Chunk Chunk,
    TaskCompletionSource? Completion = null,
    int Priority = 0
) : ChunkWorkItem(ChunkX, ChunkY, ChunkZ, ChunkWorkType.RelightFull, Completion, Priority);
