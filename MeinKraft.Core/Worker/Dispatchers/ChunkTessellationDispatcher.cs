using System.Buffers;

namespace MeinKraft.Worker;

/// <summary>
/// Implements <see cref="IChunkWorkDispatcher"/> for chunk tessellation.
///
/// Each worker thread gets its own <see cref="ChunkTessellationContext"/> via
/// <see cref="ThreadLocal{T}"/> — no locking, no shared mutable state.
/// </summary>
public sealed class ChunkTessellationDispatcher : IChunkWorkDispatcher
{
    private const int BufferedChunkEdge = 18;
    private const int BufferedChunkVolume = BufferedChunkEdge * BufferedChunkEdge * BufferedChunkEdge;

    private readonly IVoxelMap _voxelMap;
    private readonly IMeshBatcher _meshBatcher;
    private readonly ILightManager _lightManager;
    private readonly ITerrainChunkTesselator _tesselator;
    private readonly ThreadLocal<ChunkTessellationContext> _context;

    private int _chunkSize;
    private int _bufferedChunkSize;
    private bool _started;

    public ChunkTessellationDispatcher(
        IVoxelMap voxelMap,
        ITerrainChunkTesselator tesselator,
        ILightManager lightManager,
        IMeshBatcher meshBatcher)
    {
        _voxelMap = voxelMap;
        _tesselator = tesselator;
        _meshBatcher = meshBatcher;
        _lightManager = lightManager;

        _context = new ThreadLocal<ChunkTessellationContext>(
            () => _tesselator.CreateContext(),
            trackAllValues: false);
    }

    public void Start()
    {
        _chunkSize = GameConstants.CHUNK_SIZE;
        _bufferedChunkSize = _chunkSize + 2;
        _tesselator.Start();
        _started = true;
    }

    // ── IChunkWorkDispatcher ──────────────────────────────────────────────────

    public Task DispatchAsync(ChunkWorkItem item, CancellationToken ct)
    {
        if (!_started) return Task.CompletedTask;

        switch (item)
        {
            case TessellationChunkWorkItem tessellationItem:
                TessellateChunk(tessellationItem);
                break;
        }

        return Task.CompletedTask;
    }

    // ── Tessellation (runs on worker thread) ──────────────────────────────────

    private void TessellateChunk(TessellationChunkWorkItem item)
    {
        Chunk c = item.Chunk;
        if (c == null) return;

        c.Rendered ??= new RenderedChunk();     // safe: only ever set from null on one thread
                                                // (main thread pre-allocated it before enqueue;
                                                //  see design note below)

        ChunkTessellationContext ctx = _context.Value;

        // Block data read — safe: only the main thread writes block IDs,
        // and it does so between frames, not mid-tessellation.
        GetExtendedChunk(ctx, item.ChunkX, item.ChunkY, item.ChunkZ);

        int meshCount = 0;
        VerticesIndicesToLoad[] meshData = null;
        bool dataRented = false;

        if (!IsUniformChunk(ctx.CurrentChunk, BufferedChunkVolume))
        {
            // Copy the pre-computed lighting snapshot into the per-thread shadow buffer.
            // item.ShadowBuffer is read-only from the worker's perspective.
            item.ShadowBuffer.AsSpan(0, BufferedChunkVolume)
                             .CopyTo(ctx.CurrentChunkShadows.AsSpan(0, BufferedChunkVolume));

            VerticesIndicesToLoad[] meshes = _tesselator.MakeChunk(
                item.ChunkX, item.ChunkY, item.ChunkZ,
                _lightManager.LightLevels,
                ctx,
                out meshCount);

            if (meshCount > 0)
            {
                meshData = ArrayPool<VerticesIndicesToLoad>.Shared.Rent(meshCount);
                dataRented = true;
                for (int i = 0; i < meshCount; i++)
                    meshData[i] = CloneVerticesIndicesToLoad(meshes[i]);
            }
        }

        // Shadow buffer is no longer needed — return it unconditionally here.
        // This is safe even if tessellation was skipped (uniform chunk path above).
        if (item.ShadowBufferRented)
            ArrayPool<byte>.Shared.Return(item.ShadowBuffer);

        _meshBatcher.StageChunk(c, meshData ?? [], meshCount, dataRented);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void GetExtendedChunk(ChunkTessellationContext ctx, int x, int y, int z)
    {
        _voxelMap.GetMapPortion(
            ctx.CurrentChunk,
            (x * _chunkSize) - 1, (y * _chunkSize) - 1, (z * _chunkSize) - 1,
            _bufferedChunkSize, _bufferedChunkSize, _bufferedChunkSize);
    }

    private static bool IsUniformChunk(int[] chunk, int length)
    {
        int first = chunk[0];
        for (int i = 1; i < length; i++)
            if (chunk[i] != first) return false;
        return true;
    }

    private static VerticesIndicesToLoad CloneVerticesIndicesToLoad(VerticesIndicesToLoad source)
        => new()
        {
            ModelData = CloneModelData(source.ModelData),
            PositionX = source.PositionX,
            PositionY = source.PositionY,
            PositionZ = source.PositionZ,
            Texture = source.Texture,
            Transparent = source.Transparent,
        };

    private static GeometryModel CloneModelData(GeometryModel source)
    {
        GeometryModel dest = new()
        {
            Xyz = ArrayPool<float>.Shared.Rent(source.XyzCount),
            Uv = ArrayPool<float>.Shared.Rent(source.UvCount),
            Rgba = ArrayPool<byte>.Shared.Rent(source.RgbaCount),
            Indices = ArrayPool<int>.Shared.Rent(source.IndicesCount)
        };

        source.Xyz.AsSpan(0, source.XyzCount).CopyTo(dest.Xyz);
        source.Uv.AsSpan(0, source.UvCount).CopyTo(dest.Uv);
        source.Rgba.AsSpan(0, source.RgbaCount).CopyTo(dest.Rgba);
        source.Indices.AsSpan(0, source.IndicesCount).CopyTo(dest.Indices);

        dest.VerticesCount = source.VerticesCount;
        dest.IndicesCount = source.IndicesCount;
        return dest;
    }
}
