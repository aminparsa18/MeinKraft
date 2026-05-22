using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace MeinKraft.Worker;

/// <summary>
/// Reads from <see cref="IChunkWorkQueue"/> using a configurable number of parallel
/// workers — one <see cref="Task"/> per worker, each running on the thread pool.
/// </summary>
public class ChunkWorkerPool : BackgroundService, IChunkWorkQueue
{
    public static int DefaultWorkerCount => Math.Max(1, Environment.ProcessorCount - 1);

    private readonly ConcurrentQueue<TerrainRendererRedraw> _results = new();
    private readonly Channel<ChunkWorkItem> _channel;
    private readonly IChunkWorkDispatcher _dispatcher;
    private readonly IGameLogger _logger;
    private readonly int _workerCount;

    public int PendingCount => _channel.Reader.Count;

    public ChunkWorkerPool(
        IChunkWorkDispatcher dispatcher,
        IGameLogger logger,
        int workerCount = 0,
        int channelCapacity = 512)
    {
        _dispatcher = dispatcher;
        _logger = logger;
        _workerCount = workerCount > 0 ? workerCount : DefaultWorkerCount;

        _channel = Channel.CreateBounded<ChunkWorkItem>(new BoundedChannelOptions(channelCapacity)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest,
        });

        _logger.Client.Information(
            "ChunkWorkerPool: {Workers} workers, channel capacity {Capacity}",
            _workerCount, channelCapacity);
    }

    // IChunkWorkQueue ──────────────────────────────────────────────────────────

    public ValueTask EnqueueAsync(ChunkWorkItem item, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(item, ct);

    public void EnqueueResult(TerrainRendererRedraw redraw)
        => _results.Enqueue(redraw);

    public bool TryDequeueResult(out TerrainRendererRedraw redraw)
        => _results.TryDequeue(out redraw);

    // BackgroundService ────────────────────────────────────────────────────────

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Task[] workers = new Task[_workerCount];
        for (int i = 0; i < _workerCount; i++)
        {
            int workerId = i;
            workers[i] = Task.Run(
                () => WorkerLoopAsync(workerId, stoppingToken), stoppingToken);
        }

        stoppingToken.Register(() => _channel.Writer.TryComplete());

        return Task.WhenAll(workers);
    }

    private async Task WorkerLoopAsync(int workerId, CancellationToken ct)
    {
        await foreach (ChunkWorkItem item in _channel.Reader.ReadAllAsync(ct))
        {
            try
            {
                await _dispatcher.DispatchAsync(item, ct);
                item.Completion?.TrySetResult();
            }
            catch (OperationCanceledException)
            {
                item.Completion?.TrySetCanceled(ct);
                break;
            }
            catch (Exception ex)
            {
                _logger.Client.Error(ex,
                    "Chunk worker {Id} failed on {Type} ({X},{Y},{Z})",
                    workerId, item.Type, item.ChunkX, item.ChunkY, item.ChunkZ);

                item.Completion?.TrySetException(ex);
            }
        }
    }
}