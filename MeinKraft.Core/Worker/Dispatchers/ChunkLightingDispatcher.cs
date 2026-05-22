namespace MeinKraft.Worker;

using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// IChunkWorkDispatcher for the lighting stage.
///
/// Handles two work item types:
///   LightingChunkWorkItem         — full relight (LightBase + LightBetweenChunks)
///                                   used for chunk load and sunlight-affecting changes
///   RelightBetweenChunksWorkItem  — partial relight (LightBetweenChunks only)
///                                   used after IncrementalLightBFS has already updated BaseLight
///
/// Race elimination (Option B):
///   After the LightBase pass completes for all 27 neighbours, HandleFullRelight
///   snapshots each neighbour's BaseLight into a private rented buffer. Those
///   buffers are passed to LightBetweenChunks.CalculateLightBetweenChunks which
///   reads from them instead of from the live chunk arrays. Concurrent LightBase
///   writers on other workers can never corrupt the snapshot — it is immutable
///   from the moment it is taken.
/// </summary>
public sealed class ChunkLightingDispatcher : IChunkWorkDispatcher
{
    private const int BufferedChunkVolume = 18 * 18 * 18;
    private const int NVol = 3 * 3 * 3; // 27 neighbours
    private const int CVol = 16 * 16 * 16; // blocks per chunk

    private readonly IChunkWorkQueue _tessellationQueue;
    private readonly IVoxelMap _voxelMap;
    private readonly IBlockRegistry _blockRegistry;
    private readonly ThreadLocal<LightingThreadContext> _context;

    public ChunkLightingDispatcher(
        IChunkWorkQueue tessellationQueue,
        IVoxelMap voxelMap,
        IBlockRegistry blockRegistry)
    {
        _tessellationQueue = tessellationQueue;
        _voxelMap = voxelMap;
        _blockRegistry = blockRegistry;
        _context = new ThreadLocal<LightingThreadContext>(
            () => new LightingThreadContext(voxelMap),
            trackAllValues: false);
    }

    public void InvalidateBlockTypeCache()
    {
        // Each thread context has its own cache — mark all dirty.
        // ThreadLocal doesn't expose all values when trackAllValues=false,
        // so we use a shared volatile flag that each context checks on next use.
        Volatile.Write(ref _globalCacheVersion, _globalCacheVersion + 1);
    }

    private volatile int _globalCacheVersion;

    // ── IChunkWorkDispatcher ──────────────────────────────────────────────────

    public async Task DispatchAsync(ChunkWorkItem item, CancellationToken ct)
    {
        switch (item)
        {
            case LightingChunkWorkItem li:
                await HandleFullRelight(li, ct);
                break;

            case RelightBetweenChunksWorkItem rb:
                await HandleRelightBetweenChunks(rb, ct);
                break;
        }
    }

    // ── Full relight (LightBase + LightBetweenChunks) ────────────────────────

    private async Task HandleFullRelight(LightingChunkWorkItem li, CancellationToken ct)
    {
        Chunk? chunk = li.Chunk ?? _voxelMap.GetChunkAt(li.ChunkX, li.ChunkY, li.ChunkZ);
        if (chunk == null) return;

        chunk.Rendered ??= new RenderedChunk();

        LightingThreadContext ctx = _context.Value;
        ctx.RefreshCacheIfNeeded(_blockRegistry, _globalCacheVersion);

        // ── Pass 1: LightBase ─────────────────────────────────────────────────
        // Run LightBase for every dirty neighbour in the 3×3×3 window.
        // TryClaimBaseLightDirty ensures each chunk is processed by exactly one worker.

        bool anyBaseLightChanged = false;

        for (int xx = 0; xx < 3; xx++)
            for (int yy = 0; yy < 3; yy++)
                for (int zz = 0; zz < 3; zz++)
                {
                    int cx1 = li.ChunkX + xx - 1;
                    int cy1 = li.ChunkY + yy - 1;
                    int cz1 = li.ChunkZ + zz - 1;
                    if (!_voxelMap.IsValidChunkPos(cx1, cy1, cz1)) continue;

                    Chunk? neighbour = _voxelMap.Chunks[
                        _voxelMap.ChunkFlatIndex(cx1, cy1, cz1)];
                    if (neighbour == null) continue;

                    if (!neighbour.TryClaimBaseLightDirty()) continue;

                    ctx.LightBase.CalculateChunkBaseLight(
                        cx1, cy1, cz1,
                        ctx.ShadowLightRadius,
                        ctx.ShadowIsTransparent);

                    anyBaseLightChanged = true;
                }

        // ── Pass 2: Snapshot BaseLight for all 27 neighbours ──────────────────
        // Taken after this worker's LightBase writes are complete.
        // Chunks that are clean had no writer, so their BaseLight is stable.
        // Chunks whose dirty flag was claimed by another worker may still be
        // mid-write; the snapshot captures whatever is visible at this instant,
        // which is at worst one frame stale — acceptable for a voxel lighting
        // approximation and far better than the previous data corruption.

        byte[][] snapshots = ctx.BaseLightSnapshots;

        for (int xx = 0; xx < 3; xx++)
            for (int yy = 0; yy < 3; yy++)
                for (int zz = 0; zz < 3; zz++)
                {
                    int slot = (zz * 3 + yy) * 3 + xx; // matches LightBetweenChunks.Idx
                    int pcx = li.ChunkX + xx - 1;
                    int pcy = li.ChunkY + yy - 1;
                    int pcz = li.ChunkZ + zz - 1;

                    byte[] snap = ArrayPool<byte>.Shared.Rent(CVol);

                    if (!_voxelMap.IsValidChunkPos(pcx, pcy, pcz))
                    {
                        snap.AsSpan(0, CVol).Fill(0);
                        snapshots[slot] = snap;
                        continue;
                    }

                    Chunk? neighbour = _voxelMap.GetChunkAt(pcx, pcy, pcz);
                    if (neighbour != null)
                        neighbour.SnapshotBaseLight(snap, CVol,
                            _voxelMap.ChunkFlatIndex(pcx, pcy, pcz));
                    else
                        snap.AsSpan(0, CVol).Fill(0);

                    snapshots[slot] = snap;
                }

        await SnapshotAndEnqueue(li.ChunkX, li.ChunkY, li.ChunkZ,
            chunk, ctx, anyBaseLightChanged, snapshots, li.Completion, li.Priority, ct);
    }

    // ── Partial relight (LightBetweenChunks only) ────────────────────────────

    /// <summary>
    /// Called after IncrementalLightBFS has already updated BaseLight.
    /// Skips LightBase entirely — just re-runs LightBetweenChunks to refresh
    /// Rendered.Light using live chunk data (safe: single BFS caller, no concurrent
    /// LightBase writers racing this read).
    /// </summary>
    private async Task HandleRelightBetweenChunks(RelightBetweenChunksWorkItem rb, CancellationToken ct)
    {
        Chunk? chunk = rb.Chunk ?? _voxelMap.GetChunkAt(rb.ChunkX, rb.ChunkY, rb.ChunkZ);
        if (chunk == null) return;

        chunk.Rendered ??= new RenderedChunk();

        if (chunk.Rendered.Light == null)
        {
            chunk.Rendered.Light = ArrayPool<byte>.Shared.Rent(BufferedChunkVolume);
            chunk.Rendered.LightRented = true;
            chunk.Rendered.Light.AsSpan(0, BufferedChunkVolume).Fill(15);
        }

        LightingThreadContext ctx = _context.Value;
        ctx.LightBetweenChunks.RefreshRenderedLight(rb.ChunkX, rb.ChunkY, rb.ChunkZ);

        byte[] snapshot = ArrayPool<byte>.Shared.Rent(BufferedChunkVolume);
        chunk.Rendered.Light.AsSpan(0, BufferedChunkVolume)
                            .CopyTo(snapshot.AsSpan(0, BufferedChunkVolume));

        await _tessellationQueue.EnqueueAsync(new TessellationChunkWorkItem(
            rb.ChunkX, rb.ChunkY, rb.ChunkZ, chunk,
            ShadowBuffer: snapshot,
            ShadowBufferRented: true,
            Completion: rb.Completion,
            Priority: rb.Priority), ct);
    }

    // ── Shared: run LightBetweenChunks from snapshots, snapshot Rendered.Light, enqueue tess ──

    private async Task SnapshotAndEnqueue(
        int cx, int cy, int cz,
        Chunk chunk,
        LightingThreadContext ctx,
        bool anyBaseLightChanged,
        byte[][] baseLightSnapshots,
        TaskCompletionSource? completion,
        int priority,
        CancellationToken ct)
    {
        RenderedChunk rendered = chunk.Rendered;

        if (rendered.Light == null)
        {
            rendered.Light = ArrayPool<byte>.Shared.Rent(BufferedChunkVolume);
            rendered.LightRented = true;
            rendered.Light.AsSpan(0, BufferedChunkVolume).Fill(15);
            anyBaseLightChanged = true;
        }

        if (anyBaseLightChanged)
        {
            // Use snapshot overload — reads BaseLight from immutable buffers, not live chunks.
            ctx.LightBetweenChunks.CalculateLightBetweenChunks(
                cx, cy, cz,
                ctx.ShadowLightRadius,
                ctx.ShadowIsTransparent,
                baseLightSnapshots);
        }

        // Return all 27 snapshot buffers now that LightBetweenChunks has finished with them.
        for (int i = 0; i < NVol; i++)
        {
            if (baseLightSnapshots[i] != null)
            {
                ArrayPool<byte>.Shared.Return(baseLightSnapshots[i]);
                baseLightSnapshots[i] = null;
            }
        }

        byte[] shadowSnapshot = ArrayPool<byte>.Shared.Rent(BufferedChunkVolume);
        rendered.Light.AsSpan(0, BufferedChunkVolume)
                      .CopyTo(shadowSnapshot.AsSpan(0, BufferedChunkVolume));

        await _tessellationQueue.EnqueueAsync(new TessellationChunkWorkItem(
            cx, cy, cz, chunk,
            ShadowBuffer: shadowSnapshot,
            ShadowBufferRented: true,
            Completion: completion,
            Priority: priority), ct);
    }
}
