using MeinKraft.Worker;
using Microsoft.Extensions.DependencyInjection;

namespace MeinKraft.Extensions;

public static class WorkerClientExtensions
{
    /// <summary>
    /// Registers chunk worker pools (lighting + tessellation) and WorkerHost as singletons.
    ///
    /// Lighting pool:      workerCount = max(1, ProcessorCount / 4)
    ///                     Safe with multiple workers now that Option B (BaseLight
    ///                     snapshot) is implemented in ChunkLightingDispatcher.
    /// Tessellation pool:  workerCount = N  — parallel geometry factory
    ///
    /// Usage:
    /// <code>
    /// services.AddWorkerInfrastructure()
    ///         .AddScheduledTask&lt;SaveGameTask&gt;()
    ///         .AddScheduledTask&lt;SeasonBroadcastTask&gt;();
    /// </code>
    /// </summary>
    public static IServiceCollection AddWorkerInfrastructure(
        this IServiceCollection services,
        int workerCount = 0,
        int chunkChannelCapacity = 512)
    {
        // Registers IPublisher<T> / ISubscriber<T> for all event types.
        // Must come before any service that injects IPublisher or ISubscriber.
        services.AddMessagePipe();

        // ── Tessellation pool ─────────────────────────────────────────────────

        services.AddSingleton<ChunkTessellationDispatcher>();
        services.AddSingleton<IChunkWorkDispatcher>(sp =>
            sp.GetRequiredService<ChunkTessellationDispatcher>());

        services.AddSingleton(sp => new ChunkWorkerPool(
            sp.GetRequiredService<IChunkWorkDispatcher>(),
            sp.GetRequiredService<IGameLogger>(),
            2,
            chunkChannelCapacity));

        services.AddSingleton<IChunkWorkQueue>(sp =>
            sp.GetRequiredService<ChunkWorkerPool>());

        // ── Lighting pool ─────────────────────────────────────────────────────
        // Option B (BaseLight snapshot) is implemented — the read/write race
        // between LightBetweenChunks.Input and LightBase is eliminated.
        // Scale to ProcessorCount / 4: lighting is heavier per-chunk than
        // tessellation so it needs fewer workers to saturate the tessellation queue.

        int lightingWorkerCount = 4;// Math.Max(1, Environment.ProcessorCount / 4);

        services.AddSingleton(sp => new ChunkLightingDispatcher(
            sp.GetRequiredService<IChunkWorkQueue>(),
            sp.GetRequiredService<IVoxelMap>(),
            sp.GetRequiredService<IBlockRegistry>()));

        services.AddSingleton(sp => new ChunkLightingPool(
            sp.GetRequiredService<ChunkLightingDispatcher>(),
            sp.GetRequiredService<IGameLogger>(),
            2,
            channelCapacity: 512));

        services.AddSingleton<ILightingWorkQueue>(sp =>
            sp.GetRequiredService<ChunkLightingPool>());

        services.AddSingleton<ClientWorkerHost>();

        return services;
    }
}