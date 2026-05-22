using MeinKraft.Worker;


namespace MeinKraft.Extensions;

/// <summary>
/// Single-worker ChunkWorkerPool for the lighting stage.
/// Implements ILightingWorkQueue so the DI container resolves it unambiguously
/// alongside the tessellation ChunkWorkerPool that implements IChunkWorkQueue.
/// </summary>
public sealed class ChunkLightingPool(
    IChunkWorkDispatcher dispatcher,
    IGameLogger logger,
    int workerCount,
    int channelCapacity)
    : ChunkWorkerPool(dispatcher, logger, workerCount, channelCapacity), ILightingWorkQueue;