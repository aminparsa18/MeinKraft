using MeinKraft.Extensions;

namespace MeinKraft.Worker;

/// <summary>
/// Manages the lifetime of all background workers for a single game session.
/// Call StartAsync from Connect(), StopAsync on exit.
/// </summary>
public sealed class ClientWorkerHost : IAsyncDisposable
{
    private readonly ChunkWorkerPool _tessellationPool;
    private readonly ChunkLightingPool _lightingPool;
    private readonly CancellationTokenSource _cts = new();
    private readonly IGameLogger _logger;

    private Task? _allWorkers;
    private bool _started;

    private CancellationToken _token;

    public ClientWorkerHost(
        ChunkWorkerPool tessellationPool,
        ChunkLightingPool lightingPool,
        IGameLogger logger)
    {
        _tessellationPool = tessellationPool;
        _lightingPool = lightingPool;
        _logger = logger;
        _token = _cts.Token;
    }

    public async Task StartAsync()
    {
        if (_started)
        {
            _logger.Client.Warning("WorkerHost.StartAsync called twice — ignoring");
            return;
        }

        _started = true;

        _logger.Client.Information("WorkerHost: starting workers");
 
        await _tessellationPool.StartAsync(_token);
        await _lightingPool.StartAsync(_token);       // single-worker lighting stage

        _allWorkers = Task.WhenAll(
            _tessellationPool.ExecuteTask ?? Task.CompletedTask,
            _lightingPool.ExecuteTask ?? Task.CompletedTask);

        _logger.Client.Information("WorkerHost: all workers running");
    }

    public async Task StopAsync()
    {
        if (!_started)
        {
            return;
        }

        _logger.Client.Information("WorkerHost: stopping workers");

        _cts.Cancel();

        // Stop lighting first — it feeds the tessellation queue.
        // Stopping in reverse pipeline order ensures no new tessellation
        // items are enqueued after the tessellation pool has shut down.
        await _lightingPool.StopAsync(CancellationToken.None);
        await _tessellationPool.StopAsync(CancellationToken.None);

        if (_allWorkers is not null)
        {
            try { await _allWorkers; }
            catch (OperationCanceledException) { }
        }

        _started = false;

        _logger.Client.Information("WorkerHost: all workers stopped");
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}