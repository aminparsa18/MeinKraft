namespace MeinKraft.Worker;

/// <summary>
/// A single recurring task. Implement this for any work that should run on a
/// fixed interval — saves, stats resets, season broadcasts, etc.
/// </summary>
public interface IScheduledTask
{
    /// <summary>How often this task should fire.</summary>
    TimeSpan Interval { get; }

    /// <summary>
    /// Called by <see cref="PeriodicTaskScheduler"/> on the scheduler's dedicated
    /// background thread. Implementations should be async-safe and short-lived;
    /// push heavy work onto <see cref="IChunkWorkQueue"/> if needed.
    /// </summary>
    Task ExecuteAsync(CancellationToken ct);
}