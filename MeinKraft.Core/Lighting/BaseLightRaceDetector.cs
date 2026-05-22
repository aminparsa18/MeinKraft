#if DEBUG
using System.Collections.Concurrent;

namespace MeinKraft;

/// <summary>
/// Debug-only instrumentation that detects concurrent BaseLight access.
///
/// Usage — wrap every BaseLight read and write:
///
///   // Before LightBase writes BaseLight:
///   BaseLightRaceDetector.BeginWrite(chunkIndex, "LightBase");
///   _lightBase.CalculateChunkBaseLight(...);
///   BaseLightRaceDetector.EndWrite(chunkIndex);
///
///   // Before LightBetweenChunks reads BaseLight:
///   BaseLightRaceDetector.BeginRead(chunkIndex, "LightBetweenChunks.Input");
///   Array.Copy(chunk.BaseLight, lightSlot, CVol);
///   BaseLightRaceDetector.EndRead(chunkIndex);
///
/// A race is confirmed when BeginRead or BeginWrite finds the chunk already
/// owned by a different thread.
/// </summary>
public static class BaseLightRaceDetector
{
    // chunkFlatIndex → (ownerThreadId, operation)
    private static readonly ConcurrentDictionary<int, (int threadId, string op)>
        _active = new();

    private static IGameLogger? _logger;
    private static int _raceCount;

    public static void Init(IGameLogger logger) => _logger = logger;

    public static int RaceCount => _raceCount;

    // ── Write region (LightBase) ──────────────────────────────────────────────

    public static void BeginWrite(int chunkIndex, string operation = "Write")
    {
        int me = Thread.CurrentThread.ManagedThreadId;
        var entry = (me, operation);

        if (!_active.TryAdd(chunkIndex, entry))
        {
            if (_active.TryGetValue(chunkIndex, out (int otherId, string otherOp) other)
                && other.otherId != me)
            {
                int count = Interlocked.Increment(ref _raceCount);
                _logger.Client?.Warning(
                    "[RACE #{Count}] chunk {Chunk}: thread {Me} starting {Op} " +
                    "while thread {Other} is in {OtherOp}",
                    count, chunkIndex, me, operation, other.otherId, other.otherOp);
            }
        }
    }

    public static void EndWrite(int chunkIndex) =>
        _active.TryRemove(chunkIndex, out _);

    // ── Read region (LightBetweenChunks.Input) ────────────────────────────────

    public static void BeginRead(int chunkIndex, string operation = "Read")
        => BeginWrite(chunkIndex, operation);   // same detection logic

    public static void EndRead(int chunkIndex)
        => EndWrite(chunkIndex);

    // ── Summary ───────────────────────────────────────────────────────────────

    public static void LogSummary()
    {
        if (_raceCount == 0)
            _logger?.Client.Information("[RaceDetector] No races detected.");
        else
            _logger?.Client.Error("[RaceDetector] {Count} races detected — BaseLight is NOT thread-safe with multiple lighting workers.", _raceCount);
    }
}
#endif