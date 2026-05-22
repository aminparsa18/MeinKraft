namespace MeinKraft.Worker;

/// <summary>
/// Enqueues chunk work items. Inject this anywhere that needs to schedule chunk work
/// (ModDrawTerrain, block placement handlers, world gen, etc.).
/// </summary>
public interface IChunkWorkQueue
{
    /// <summary>
    /// Post a work item. Awaits only if the channel is full (back-pressure).
    /// Fire-and-forget callers can safely discard the ValueTask.
    /// </summary>
    ValueTask EnqueueAsync(ChunkWorkItem item, CancellationToken ct = default);

    /// <summary>Number of items currently waiting to be processed.</summary>
    int PendingCount { get; }

    // Output side — drain tessellation results on main thread
    void EnqueueResult(TerrainRendererRedraw redraw);
    bool TryDequeueResult(out TerrainRendererRedraw redraw);
}