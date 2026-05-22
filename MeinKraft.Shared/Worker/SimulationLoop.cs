using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MeinKraft.Worker;

/// <summary>
/// Replaces <c>ServerThreadStart</c>'s <c>while(true) { server.Process(); Thread.Sleep(1); }</c>.
///
/// Runs <see cref="ISimulationStep.Tick"/> at a configurable rate using
/// <see cref="PeriodicTimer"/> — no busy-wait, no <c>Thread.Sleep</c>, proper
/// cancellation, and accurate timing via the OS high-resolution clock.
/// </summary>
public sealed class SimulationLoop : BackgroundService
{
    /// <summary>Target simulation rate. 20 Hz = 50 ms per tick (Minecraft-style).</summary>
    public static readonly TimeSpan DefaultTickInterval = TimeSpan.FromMilliseconds(50);

    private readonly ISimulationStep _simulation;
    private readonly ILogger<SimulationLoop> _logger;
    private readonly TimeSpan _tickInterval;

    public SimulationLoop(
        ISimulationStep simulation,
        ILogger<SimulationLoop> logger,
        TimeSpan tickInterval = default)
    {
        _simulation = simulation;
        _logger = logger;
        _tickInterval = tickInterval == default ? DefaultTickInterval : tickInterval;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "SimulationLoop started at {Hz:F1} Hz",
            1000.0 / _tickInterval.TotalMilliseconds);

        using PeriodicTimer timer = new(_tickInterval);
        long lastTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            float dt = (float)((double)(now - lastTimestamp) / System.Diagnostics.Stopwatch.Frequency);
            lastTimestamp = now;

            try
            {
                _simulation.Tick(dt);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Log and continue — one bad tick should not kill the loop.
                _logger.LogError(ex, "SimulationLoop: unhandled exception in Tick");
            }
        }

        _logger.LogInformation("SimulationLoop stopped");
    }
}