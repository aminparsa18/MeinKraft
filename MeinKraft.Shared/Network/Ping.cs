/// <summary>
/// Tracks round-trip latency for a single outstanding ping.
/// Only one ping may be in-flight at a time; call <see cref="Send"/> only when
/// <see cref="IsReady"/> is <see langword="true"/>.
/// </summary>
public class Ping
{
    /// <summary>Gets or sets how long to wait before declaring a ping timed out.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Gets the measured round-trip time of the last completed ping.</summary>
    public int RoundtripMilliseconds { get; private set; }

    /// <summary>Gets whether the tracker is ready to send a new ping.</summary>
    public bool IsReady { get; private set; } = true;

    private int _sentAtMilliseconds;

    /// <summary>
    /// Begins a new ping. Records the send timestamp and marks the tracker as in-flight.
    /// </summary>
    /// <returns><see langword="true"/> if the ping was sent; <see langword="false"/> if one is already in-flight.</returns>
    public bool Send(int milliSeconds)
    {
        if (!IsReady)
        {
            return false;
        }

        IsReady = false;
        _sentAtMilliseconds = milliSeconds;
        return true;
    }

    /// <summary>
    /// Completes the in-flight ping and records the round-trip time.
    /// </summary>
    /// <returns><see langword="true"/> if the ping was received; <see langword="false"/> if no ping was in-flight.</returns>
    public bool Receive(int milliseconds)
    {
        if (IsReady)
        {
            return false;
        }

        RoundtripMilliseconds = milliseconds - _sentAtMilliseconds;
        IsReady = true;
        return true;
    }

    /// <summary>
    /// Checks whether the in-flight ping has exceeded <see cref="Timeout"/>.
    /// If so, resets the tracker so a new ping can be sent.
    /// </summary>
    /// <returns><see langword="true"/> if the ping timed out; otherwise <see langword="false"/>.</returns>
    public bool CheckTimeout(int milliSeconds)
    {
        int elapsedMs = milliSeconds - _sentAtMilliseconds;
        if (!IsReady && elapsedMs > Timeout.TotalMilliseconds)
        {
            IsReady = true;
            return true;
        }

        return false;
    }
}