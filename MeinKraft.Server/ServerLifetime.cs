namespace MeinKraft;

/// <summary>
/// Shared cancellation primitive owned by <see cref="WorkerHost"/>.
/// <see cref="ServerSimulationStep"/> cancels it on exit instead of depending
/// on <see cref="WorkerHost"/> directly — breaking the circular dependency:
///
///   WorkerHost → SimulationLoop → ISimulationStep → ServerSimulationStep
///                                                         └── ServerLifetime ✓
///                                                    (was → WorkerHost 💥)
/// </summary>
public sealed class ServerLifetime
{
    private readonly CancellationTokenSource _cts = new();

    public CancellationToken Token => _cts.Token;

    /// <summary>
    /// Signal the simulation loop to stop after the current tick.
    /// Called by <see cref="ServerSimulationStep"/> when it detects an exit condition.
    /// </summary>
    public void SignalStop() => _cts.Cancel();
}