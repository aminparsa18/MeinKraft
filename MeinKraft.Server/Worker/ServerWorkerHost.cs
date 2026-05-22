namespace MeinKraft.Worker;

/// <summary>
/// Manages the lifetime of all background workers for a single game session.
/// Call StartAsync from Connect(), StopAsync on exit.
/// </summary>
public sealed class ServerWorkerHost : IAsyncDisposable, IServerWorkerHost
{
    private readonly SimulationLoop _simulationLoop;
    private readonly PeriodicTaskScheduler _periodicScheduler;
    private readonly ServerLifetime _lifetime;
    private readonly IGameLogger _logger;

    private Task? _allWorkers;
    private bool _started;

    public ServerWorkerHost(PeriodicTaskScheduler periodicScheduler, ServerLifetime lifetime, SimulationLoop simulationLoop,
        IGameLogger logger)
    {
        _periodicScheduler = periodicScheduler;
        _lifetime = lifetime;
        _logger = logger;
        _simulationLoop = simulationLoop;
    }

    public async Task StartAsync()
    {
        if (_started)
        {
            _logger.Client.Warning("WorkerHost.StartAsync called twice — ignoring");
            return;
        }

        _started = true;
        CancellationToken ct = _lifetime.Token;

        _logger.Client.Information("WorkerHost: starting workers");

        await _simulationLoop.StartAsync(ct);
        await _periodicScheduler.StartAsync(ct);

        _allWorkers = Task.WhenAll(
            _simulationLoop.ExecuteTask ?? Task.CompletedTask,
            _periodicScheduler.ExecuteTask ?? Task.CompletedTask);

        _logger.Client.Information("WorkerHost: all workers running");
    }

    public async Task StopAsync()
    {
        if (!_started)
        {
            return;
        }

        _logger.Client.Information("WorkerHost: stopping workers");

        _lifetime.SignalStop();

        // Stop lighting first — it feeds the tessellation queue.
        // Stopping in reverse pipeline order ensures no new tessellation
        // items are enqueued after the tessellation pool has shut down.
        await _simulationLoop.StopAsync(CancellationToken.None);
        await _periodicScheduler.StopAsync(CancellationToken.None);

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