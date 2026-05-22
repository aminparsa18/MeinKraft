using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MeinKraft.Worker;

/// <summary>
/// Drives all registered <see cref="IScheduledTask"/> implementations.
/// Tasks share one timer loop — each tracks its own <c>NextRunAt</c> so they
/// fire independently without spinning up separate threads.
/// </summary>
public sealed class PeriodicTaskScheduler : BackgroundService
{
    // How often the scheduler wakes to check whether any task is due.
    // 1 s is more than fine — no task needs sub-second precision here.
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);

    private readonly IReadOnlyList<IScheduledTask> _tasks;
    private readonly ILogger<PeriodicTaskScheduler> _logger;

    public PeriodicTaskScheduler(
        IEnumerable<IScheduledTask> tasks,
        ILogger<PeriodicTaskScheduler> logger)
    {
        _tasks = tasks.ToList();
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Track when each task should next run.
        DateTimeOffset[] nextRun = [.. _tasks.Select(t => DateTimeOffset.UtcNow + t.Interval)];

        using PeriodicTimer timer = new(TickInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;

            for (int i = 0; i < _tasks.Count; i++)
            {
                if (now < nextRun[i])
                {
                    continue;
                }

                try
                {
                    await _tasks[i].ExecuteAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Scheduled task {Task} threw an exception", _tasks[i].GetType().Name);
                }
                finally
                {
                    // Always reschedule — a failing task should not stop firing.
                    nextRun[i] = DateTimeOffset.UtcNow + _tasks[i].Interval;
                }
            }
        }
    }
}