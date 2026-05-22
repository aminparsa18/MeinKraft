using System.Diagnostics;

/// <summary>
/// Tracks in-game time independently of real-world wall-clock time by scaling
/// elapsed real seconds by <see cref="SpeedOfTime"/>. Driven by repeated calls
/// to <see cref="Tick"/> from the server loop.
/// </summary>
public class GameTimer
{
    private long _startTimestamp = 0;
    private long _lastIngameSecond = 0;
    private bool _running = false;

    /// <summary>
    /// How many in-game seconds pass per real second.
    /// A value of <c>60</c> means one real second equals one in-game minute.
    /// Set to <c>0</c> to pause time progression.
    /// </summary>
    public int SpeedOfTime { get; set; } = 60;

    /// <summary>
    /// Number of in-game days that make up one in-game year.
    /// Used by <see cref="Day"/>, <see cref="Year"/>, and <see cref="Season"/>.
    /// </summary>
    public int DaysPerYear { get; set; } = 4;

    /// <summary>Total elapsed in-game time as a <see cref="TimeSpan"/>.</summary>
    public TimeSpan Time { get; private set; } = TimeSpan.Zero;

    /// <summary>Current in-game hour of the day (0–23).</summary>
    public int Hour => Time.Hours;

    /// <summary>Total elapsed in-game hours, including fractional hours.</summary>
    public double HourTotal => Time.TotalHours;

    /// <summary>
    /// Current in-game day within the year (0 to <see cref="DaysPerYear"/> − 1).
    /// </summary>
    public int Day => (int)(Time.TotalDays % DaysPerYear);

    /// <summary>Total elapsed in-game days, including fractional days.</summary>
    public double DaysTotal => Time.TotalDays;

    /// <summary>Current in-game year, starting at 0.</summary>
    public int Year => (int)(Time.TotalDays / DaysPerYear);

    /// <summary>
    /// Current season index (0–3), cycling every year.
    /// Derived from <see cref="Year"/> modulo 4.
    /// </summary>
    public int Season => Year % 4;

    /// <summary>
    /// Initialises the timer to a specific in-game time and resets internal
    /// tracking state. Must be called before <see cref="Start"/>.
    /// </summary>
    /// <param name="ticks">
    /// In-game time expressed as <see cref="TimeSpan"/> ticks
    /// (see <see cref="TimeSpan.Ticks"/>).
    /// </param>
    internal void Init(long ticks)
    {
        Time = TimeSpan.FromTicks(ticks);
        _lastIngameSecond = 0;
        _startTimestamp = Stopwatch.GetTimestamp();
    }

    /// <summary>Starts the timer so that subsequent <see cref="Tick"/> calls advance time.</summary>
    internal void Start()
    {
        _running = true;
        _startTimestamp = Stopwatch.GetTimestamp();
    }

    /// <summary>Pauses the timer. Time will not advance until <see cref="Start"/> is called again.</summary>
    internal void Stop() => _running = false;

    /// <summary>
    /// Advances in-game time based on real elapsed seconds scaled by <see cref="SpeedOfTime"/>.
    /// Should be called once per server tick.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if at least one in-game second has elapsed since the
    /// last call and time was advanced; <see langword="false"/> if the timer is
    /// stopped, paused, or no real second has passed yet.
    /// </returns>
    internal bool Tick()
    {
        if (!_running || SpeedOfTime == 0)
            return false;

        long elapsed = Stopwatch.GetTimestamp() - _startTimestamp;
        long elapsedSeconds = elapsed / Stopwatch.Frequency;

        if (elapsedSeconds <= _lastIngameSecond)
            return false;

        long delta = elapsedSeconds - _lastIngameSecond;
        _lastIngameSecond = elapsedSeconds;
        Time += TimeSpan.FromSeconds(delta * SpeedOfTime);
        return true;
    }

    /// <summary>
    /// Returns the current time of day expressed as quarter-hour intervals (0–95).
    /// Each unit represents 15 minutes; midnight = 0, noon = 48.
    /// </summary>
    public int GetQuarterHourPartOfDay() => (Time.Hours * 4) + (Time.Minutes / 15);

    /// <summary>Overrides the current in-game time to the given value.</summary>
    internal void Set(TimeSpan time) => Time = time;

    /// <summary>Adds the given duration to the current in-game time.</summary>
    internal void Add(TimeSpan time) => Time += time;
}