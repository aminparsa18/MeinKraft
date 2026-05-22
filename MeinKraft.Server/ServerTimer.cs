using System.Diagnostics;

/// <summary>
/// A mod-registered recurring callback driven by <see cref="ServerGameService.ProcessMain"/>.
/// Not to be confused with <see cref="PeriodicTaskScheduler"/> which handles
/// internal infrastructure tasks. This is the runtime mod scripting API.
/// </summary>
public sealed class ServerTimer
{
    /// <summary>How often the callback fires.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Caps the accumulated delta to prevent a spiral of death after a long stall.
    /// Defaults to uncapped.
    /// </summary>
    public TimeSpan MaxDeltaTime { get; set; } = TimeSpan.MaxValue;

    private double _accumulator;
    private double _lastTime;

    public ServerTimer() => Reset();

    public void Reset()
    {
        _accumulator = 0;
        _lastTime = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
    }

    public void Update(Action callback)
    {
        double now = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        double delta = now - _lastTime;
        _lastTime = now;

        _accumulator += delta;

        if (MaxDeltaTime != TimeSpan.MaxValue)
            _accumulator = Math.Min(_accumulator, MaxDeltaTime.TotalSeconds);

        double dt = Interval.TotalSeconds;
        while (_accumulator >= dt)
        {
            callback();
            _accumulator -= dt;
        }
    }
}
