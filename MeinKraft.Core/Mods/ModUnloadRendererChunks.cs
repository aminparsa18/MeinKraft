using OpenTK.Mathematics;

/// <summary>
/// Client-side mod that unloads rendered chunk geometry for chunks that have
/// scrolled outside the player's view distance.
/// Runs on the background thread and queues GPU-side cleanup on the main thread.
/// </summary>
public class ModUnloadRendererChunks : ModBase
{
    /// <summary>Reference to the current game instance, refreshed every background tick.</summary>
    private readonly IVoxelMap _voxelMap;
    private readonly IMeshBatcher meshBatcher;

    private int _backgroundRunning; // 0 = idle, 1 = running (Interlocked)

    /// <summary>Edge length of one chunk in blocks.</summary>
    private int _chunkSize;

    /// <summary>Reciprocal of <see cref="_chunkSize"/>, used to convert block coords to chunk coords.</summary>
    private float _invertedChunk;

    /// <summary>Map width in chunks.</summary>
    private int _mapSizeXChunks;

    /// <summary>Map depth in chunks.</summary>
    private int _mapSizeYChunks;

    /// <summary>Map height in chunks.</summary>
    private int _mapSizeZChunks;

    /// <summary>
    /// Flat chunk index advanced each background tick to walk the full chunk array
    /// in a round-robin fashion, checking a fixed number of chunks per pass.
    /// </summary>
    private int _unloadIterator;

    /// <summary>Reusable output for <see cref="VectorIndexUtil.PosInt"/> to avoid per-frame allocation.</summary>
    private Vector3i _unloadXyzTemp;

    public ModUnloadRendererChunks(IVoxelMap voxelMap, IMeshBatcher meshBatcher, IGame game) : base(game)
    {
        _voxelMap = voxelMap;
        this.meshBatcher = meshBatcher;
        _unloadXyzTemp = new Vector3i();
        _unloadXyzTemp = new Vector3i();
    }

    public override void OnFrame(float dt)
    {
        if (Interlocked.CompareExchange(ref _backgroundRunning, 1, 0) == 0)
        {
            Task.Run(() =>
            {
                try { ScanForUnloads(); }
                finally { Interlocked.Exchange(ref _backgroundRunning, 0); }
            });
        }
    }

    private void ScanForUnloads()
    {
        RefreshChunkGridDimensions();

        int px = (int)(Game.LocalPositionX * _invertedChunk);
        int py = (int)(Game.LocalPositionZ * _invertedChunk);
        int pz = (int)(Game.LocalPositionY * _invertedChunk);

        int halfXY = (int)(MapAreaSize() * _invertedChunk * 0.5f);
        int halfZ = (int)(MapAreaSizeZ() * _invertedChunk * 0.5f);

        int startX = Math.Max(px - halfXY, 0);
        int startY = Math.Max(py - halfXY, 0);
        int startZ = Math.Max(pz - halfZ, 0);
        int endX = Math.Min(px + halfXY, _mapSizeXChunks - 1);
        int endY = Math.Min(py + halfXY, _mapSizeYChunks - 1);
        int endZ = Math.Min(pz + halfZ, _mapSizeZChunks - 1);

        int totalChunks = _mapSizeXChunks * _mapSizeYChunks * _mapSizeZChunks;
        int checksPerTick = Math.Max(100, totalChunks / (5 * 75));

        for (int i = 0; i < checksPerTick; i++)
        {
            if (++_unloadIterator >= totalChunks)
                _unloadIterator = 0;

            VectorIndexUtil.PosInt(_unloadIterator, _mapSizeXChunks, _mapSizeYChunks, ref _unloadXyzTemp);
            int x = _unloadXyzTemp.X;
            int y = _unloadXyzTemp.Y;
            int z = _unloadXyzTemp.Z;

            int flatIndex = VectorIndexUtil.Index3d(x, y, z, _mapSizeXChunks, _mapSizeYChunks);
            Chunk chunk = _voxelMap.Chunks[flatIndex];
            if (chunk == null) continue;

            bool hasRenderedGeometry = chunk.Rendered?.Ids != null;
            bool hasBlockData = chunk.HasData();
            if (!hasRenderedGeometry && !hasBlockData)
                continue;

            if (x < startX || y < startY || z < startZ
             || x > endX || y > endY || z > endZ)
            {
                // Stage geometry removal — main thread flushes it via FlushPendingUploads
                meshBatcher.StageUnload(chunk);
                // Release block data and null the slot immediately — safe from background thread
                chunk.Release();
                _voxelMap.Chunks[flatIndex] = null;

                if (hasRenderedGeometry)
                {
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Refreshes all chunk-grid dimension fields from the current map and chunk size.
    /// Called at the start of every background tick because the map may have changed.
    /// </summary>
    private void RefreshChunkGridDimensions()
    {
        _chunkSize = GameConstants.CHUNK_SIZE;
        _invertedChunk = 1.0f / _chunkSize;
        _mapSizeXChunks = (int)(_voxelMap.MapSizeX * _invertedChunk);
        _mapSizeYChunks = (int)(_voxelMap.MapSizeY * _invertedChunk);
        _mapSizeZChunks = (int)(_voxelMap.MapSizeZ * _invertedChunk);
    }

    /// <summary>View-distance-based side length of the active area in blocks.</summary>
    private int MapAreaSize() => (int)Game.Config3d.ViewDistance * 2;

    /// <summary>Vertical counterpart of <see cref="MapAreaSize"/>.</summary>
    private int MapAreaSizeZ() => MapAreaSize();
}