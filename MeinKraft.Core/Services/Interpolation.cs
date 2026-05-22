//This solves a problem: remote players look jerky because network packets arrive unevenly. Instead of snapping to each
//received position, the game smooths movement between snapshots.
//NetworkInterpolation is the engine — it keeps a buffer of the last 100 received position snapshots with timestamps,
//and when asked "where should this player be right now?", it finds the two snapshots that bracket the current time
//and blends between them. It also delays playback by ~200ms (or the ping time) so there's always data ahead to interpolate toward.
//ModInterpolatePositions is the driver — every frame it loops over all remote players, feeds new network positions
//into their interpolation buffer when they change, then reads back the smoothed position and applies it to the entity for rendering.
//AngleInterpolation handles the tricky part of rotation — always rotating the short way around (e.g. going from 350° to 10° should rotate 20°, not 340°).

/// <summary>
/// Marker interface for objects that can be interpolated between two states.
/// Using an interface instead of a base class leaves the implementor free to
/// inherit from whatever it needs.
/// </summary>
public interface IInterpolatedObject { }

/// <summary>Stateless interpolation strategy between two <see cref="IInterpolatedObject"/> snapshots.</summary>
public interface IInterpolation
{
    /// <summary>
    /// Returns the state between <paramref name="a"/> and <paramref name="b"/>
    /// at the given <paramref name="progress"/> (0 = a, 1 = b).
    /// </summary>
    IInterpolatedObject Interpolate(IInterpolatedObject a, IInterpolatedObject b, float progress);
}

/// <summary>Timestamped network interpolation interface.</summary>
public interface INetworkInterpolation
{
    /// <summary>Records a received state snapshot with its server timestamp.</summary>
    void AddNetworkPacket(IInterpolatedObject state, int timeMilliseconds);

    /// <summary>Returns the interpolated (or extrapolated) state for the given client time.</summary>
    IInterpolatedObject InterpolatedState(int timeMilliseconds);
}

/// <summary>
/// A timestamped state snapshot received from the network.
/// Stored as a struct so all <see cref="NetworkInterpolation.received"/> entries are
/// embedded inline in the array — no separate heap allocation per packet.
/// </summary>
internal struct Packet
{
    internal int timestampMilliseconds;
    internal IInterpolatedObject content;
}

/// <summary>
/// Buffers incoming network state snapshots and produces smoothly interpolated
/// (or optionally extrapolated) states for rendering, compensating for network jitter
/// via a configurable playback delay.
/// </summary>
/// <remarks>
/// Internally uses a fixed-size ring buffer for O(1) packet insertion.
/// Bracket lookup uses binary search for O(log n) instead of a linear scan.
/// </remarks>
public class NetworkInterpolation : INetworkInterpolation
{
    // ── Constants ─────────────────────────────────────────────────────────────

    /// <summary>Maximum number of snapshots retained in the ring buffer.</summary>
    private const int MaxPackets = 100;

    // ── Configuration ─────────────────────────────────────────────────────────

    /// <summary>Interpolation strategy supplied by the caller.</summary>
    public IInterpolation? Interpolator { get; set; }

    /// <summary>When true, the last two known states are extrapolated beyond the newest snapshot.</summary>
    public bool Extrapolate { get; set; }

    /// <summary>Playback is delayed by this many milliseconds to absorb jitter.</summary>
    public int DelayMilliseconds { get; set; } = 200;

    /// <summary>Maximum duration beyond the newest snapshot that extrapolation may reach.</summary>
    public int ExtrapolationTimeMilliseconds { get; set; } = 200;

    // ── Ring buffer ───────────────────────────────────────────────────────────

    private readonly Packet[] _buffer = new Packet[MaxPackets];

    /// <summary>Index of the oldest packet in the ring.</summary>
    private int _head;

    /// <summary>Number of valid packets currently stored.</summary>
    private int _count;

    // ── INetworkInterpolation ─────────────────────────────────────────────────

    /// <summary>
    /// Appends a new snapshot to the ring buffer in O(1).
    /// When the buffer is full the oldest entry is silently overwritten.
    /// </summary>
    public void AddNetworkPacket(IInterpolatedObject state, int timeMilliseconds)
    {
        int index = (_head + _count) % MaxPackets;
        _buffer[index] = new Packet
        {
            content = state,
            timestampMilliseconds = timeMilliseconds,
        };

        if (_count < MaxPackets)
        {
            _count++;
        }
        else
        {
            _head = (_head + 1) % MaxPackets; // oldest evicted
        }
    }

    /// <summary>
    /// Returns the interpolated state for <paramref name="timeMilliseconds"/>,
    /// applying the playback delay. Returns <see langword="null"/> when no data
    /// has been received yet.
    /// </summary>
    public IInterpolatedObject? InterpolatedState(int timeMilliseconds)
    {
        if (_count == 0)
        {
            return null;
        }

        int interpolationTime = timeMilliseconds - DelayMilliseconds;

        int p1, p2;

        if (interpolationTime < GetPacket(0).timestampMilliseconds)
        {
            // Before any known data — clamp to the first snapshot.
            p1 = p2 = 0;
        }
        else if (Extrapolate
              && _count >= 2
              && interpolationTime > GetPacket(_count - 1).timestampMilliseconds)
        {
            // Beyond the latest snapshot — extrapolate from the last two.
            p1 = _count - 2;
            p2 = _count - 1;
            interpolationTime = Math.Min(
                interpolationTime,
                GetPacket(_count - 1).timestampMilliseconds + ExtrapolationTimeMilliseconds);
        }
        else
        {
            // Normal case: binary search for the last packet at or before interpolationTime.
            p1 = FindBracketIndex(interpolationTime);
            p2 = p1 < _count - 1 ? p1 + 1 : p1;
        }

        if (p1 == p2)
        {
            return GetPacket(p1).content;
        }

        ref readonly Packet before = ref GetPacketRef(p1);
        ref readonly Packet after = ref GetPacketRef(p2);

        float progress =
            (float)(interpolationTime - before.timestampMilliseconds)
            / (after.timestampMilliseconds - before.timestampMilliseconds);

        return Interpolator?.Interpolate(before.content, after.content, progress);
    }

    // ── Ring buffer helpers ───────────────────────────────────────────────────

    /// <summary>Returns the packet at logical index <paramref name="i"/> (0 = oldest).</summary>
    private Packet GetPacket(int i) => _buffer[(_head + i) % MaxPackets];

    /// <summary>Returns a readonly ref to the packet at logical index <paramref name="i"/>.</summary>
    private ref Packet GetPacketRef(int i) => ref _buffer[(_head + i) % MaxPackets];

    /// <summary>
    /// Binary search: returns the logical index of the last packet whose timestamp
    /// is ≤ <paramref name="t"/>. Assumes the buffer is time-ordered (guaranteed by
    /// <see cref="AddNetworkPacket"/>).
    /// </summary>
    private int FindBracketIndex(int t)
    {
        int lo = 0, hi = _count - 1, result = 0;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            if (GetPacket(mid).timestampMilliseconds <= t)
            {
                result = mid;
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }

        return result;
    }
}

/// <summary>
/// Provides shortest-path angle interpolation for both 256-step and 360-degree representations.
/// </summary>
public static class AngleInterpolation
{
    private const int CircleHalf256 = 128;
    private const int CircleFull256 = 256;
    private const float CircleHalf360 = 180f;
    private const float CircleFull360 = 360f;

    /// <summary>
    /// Bias for 256-step normalisation. Must be an exact multiple of 256
    /// so it does not shift the normalised value. (32768 = 128 × 256.)
    /// </summary>
    private const int Bias256 = 256 * 128;

    /// <summary>
    /// Bias for 360° normalisation. Must be an exact multiple of 360.
    /// (36000 = 100 × 360.)
    /// </summary>
    private const float Bias360 = 360f * 100f;

    /// <summary>
    /// Interpolates between two angles in 256-step (byte) space via the shortest arc.
    /// </summary>
    public static int InterpolateAngle256(int a, int b, float progress)
    {
        if (progress != 0 && b != a)
        {
            int diff = NormalizeAngle256(b - a);
            if (diff >= CircleHalf256)
            {
                diff -= CircleFull256;
            }

            a += (int)(progress * diff);
        }

        return NormalizeAngle256(a);
    }

    /// <summary>
    /// Interpolates between two angles in degrees via the shortest arc.
    /// </summary>
    public static float InterpolateAngle360(float a, float b, float progress)
    {
        if (progress != 0 && b != a)
        {
            float diff = NormalizeAngle360(b - a);
            if (diff >= CircleHalf360)
            {
                diff -= CircleFull360;
            }

            a += progress * diff;
        }

        return NormalizeAngle360(a);
    }

    /// <summary>Normalises an angle to [0, 255].</summary>
    private static int NormalizeAngle256(int v) => (v + Bias256) % CircleFull256;

    /// <summary>Normalises an angle to [0, 360).</summary>
    private static float NormalizeAngle360(float v) => (v + Bias360) % CircleFull360;
}