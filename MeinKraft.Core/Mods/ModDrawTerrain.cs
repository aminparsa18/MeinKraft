using MeinKraft.Extensions;
using MeinKraft.Worker;
using OpenTK.Mathematics;

/// <summary>
/// Client-side mod responsible for drawing the voxel terrain.
/// Finds dirty chunks and enqueues LightingChunkWorkItems to the lighting pool.
/// Lighting → tessellation handoff happens entirely inside the worker pipeline.
/// </summary>
public class ModDrawTerrain : ModBase
{
    public const int MaxLight = 15;
    private const int NoChunk = -1;

    private readonly IGameWindowService _gameService;
    private readonly IVoxelMap _voxelMap;
    private readonly IMeshBatcher _meshBatcher;
    private readonly ILightingWorkQueue _lightingQueue;
    private readonly ChunkLightingDispatcher _lightingDispatcher;
    private readonly ChunkTessellationDispatcher _tessellationDispatcher;

    private bool _terrainStarted;
    private int _chunkUpdates;
    private int _lastPerfUpdateMs;
    private int _lastChunkUpdatesSnapshot;

    private readonly Vector3i[] _blocksAround7Buffer = new Vector3i[7];

    public ModDrawTerrain(
        IGameWindowService platform,
        IVoxelMap voxelMap,
        IMeshBatcher meshBatcher,
        ILightingWorkQueue lightingQueue,
        ChunkLightingDispatcher lightingDispatcher,
        ChunkTessellationDispatcher tessellationDispatcher,
        IGame game) : base(game)
    {
        _gameService = platform;
        _voxelMap = voxelMap;
        _meshBatcher = meshBatcher;
        _lightingQueue = lightingQueue;
        _lightingDispatcher = lightingDispatcher;
        _tessellationDispatcher = tessellationDispatcher;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public int TrianglesCount() => _meshBatcher.TotalTriangleCount();
    private static int InvertChunk(int num) => (int)(num * (1.0f / GameConstants.CHUNK_SIZE));

    // ── ModBase overrides ─────────────────────────────────────────────────────

    public override void OnRender3d(float _)
    {
        if (Game.ShouldRedrawAllBlocks)
        {
            Game.ShouldRedrawAllBlocks = false;
            RedrawAllBlocks();
        }

        _meshBatcher.FlushPendingUploads();
        _meshBatcher.Draw(Game.LocalPositionX, Game.LocalPositionY, Game.LocalPositionZ);
        UpdatePerformanceInfo();
    }

    public override void OnFrame(float dt)
    {
        if (!_terrainStarted) return;

        RedrawChunksAroundLastPlacedBlock();

        int mxc = _voxelMap.Mapsizexchunks;
        int myc = _voxelMap.Mapsizeychunks;
        int slots = ChunkWorkerPool.DefaultWorkerCount;

        for (int i = 0; i < slots; i++)
        {
            (int cx, int cy, int cz)? nearest = NearestDirty();
            if (!nearest.HasValue) break;

            (int cx, int cy, int cz) = nearest.Value;
            Chunk c = _voxelMap.Chunks[VectorIndexUtil.Index3d(cx, cy, cz, mxc, myc)];
            if (c == null) continue;

            c.Rendered ??= new RenderedChunk();

            _chunkUpdates++;
            _lightingQueue.EnqueueAsync(new LightingChunkWorkItem(cx, cy, cz, c));
        }
    }

    // ── Initialisation ────────────────────────────────────────────────────────

    private void StartTerrain()
    {
        _tessellationDispatcher.Start();
        _terrainStarted = true;
    }

    private void RedrawAllBlocks()
    {
        if (!_terrainStarted)
        {
            StartTerrain();
        }

        int chunksLength = InvertChunk(Game.MapSizeX)
                         * InvertChunk(Game.MapSizeY)
                         * InvertChunk(Game.MapSizeZ);

        for (int i = 0; i < chunksLength; i++)
        {
            Chunk c = _voxelMap.Chunks[i];
            if (c == null)
            {
                continue;
            }

            c.Rendered ??= new RenderedChunk();
            c.Rendered.Dirty = true;
            c.BaseLightDirty = true;
        }

        _lightingDispatcher.InvalidateBlockTypeCache();
    }

    // ── Dirty chunk detection ─────────────────────────────────────────────────

    private void RedrawChunksAroundLastPlacedBlock()
    {
        if (Game.LastplacedblockX == NoChunk
         && Game.LastplacedblockY == NoChunk
         && Game.LastplacedblockZ == NoChunk)
        {
            return;
        }

        int mapSizeX = InvertChunk(_voxelMap.MapSizeX);
        int mapSizeY = InvertChunk(_voxelMap.MapSizeY);
        int mapSizeZ = InvertChunk(_voxelMap.MapSizeZ);

        BlocksAround7Inplace(
            new(Game.LastplacedblockX, Game.LastplacedblockY, Game.LastplacedblockZ),
            _blocksAround7Buffer);

        for (int i = 0; i < 7; i++)
        {
            Vector3i a = _blocksAround7Buffer[i];
            int cx = InvertChunk(a.X);
            int cy = InvertChunk(a.Y);
            int cz = InvertChunk(a.Z);

            if (cx < 0 || cy < 0 || cz < 0
             || cx >= mapSizeX || cy >= mapSizeY || cz >= mapSizeZ)
            {
                continue;
            }

            Chunk c = _voxelMap.Chunks[VectorIndexUtil.Index3d(cx, cy, cz, mapSizeX, mapSizeY)];
            if (c?.Rendered != null)
            {
                c.Rendered.Dirty = true;
            }
        }

        Game.LastplacedblockX = NoChunk;
        Game.LastplacedblockY = NoChunk;
        Game.LastplacedblockZ = NoChunk;
    }

    private (int x, int y, int z)? NearestDirty()
    {
        if (_voxelMap?.Chunks == null)
        {
            return null;
        }

        int px = InvertChunk((int)Game.LocalPositionX);
        int py = InvertChunk((int)Game.LocalPositionZ);
        int pz = InvertChunk((int)Game.LocalPositionY);
        int half = InvertChunk((int)Game.Config3d.ViewDistance);

        int mxc = _voxelMap.Mapsizexchunks;
        int myc = _voxelMap.Mapsizeychunks;

        int startX = Math.Max(px - half, 0);
        int startY = Math.Max(py - half, 0);
        int startZ = Math.Max(pz - half, 0);
        int endX = Math.Min(px + half, mxc - 1);
        int endY = Math.Min(py + half, myc - 1);
        int endZ = Math.Min(pz + half, _voxelMap.Mapsizezchunks - 1);

        int bestIdx = -1;
        int bestDist = int.MaxValue;

        for (int ix = startX; ix <= endX; ix++)
            for (int iy = startY; iy <= endY; iy++)
                for (int iz = startZ; iz <= endZ; iz++)
                {
                    int i = VectorIndexUtil.Index3d(ix, iy, iz, mxc, myc);
                    Chunk c = _voxelMap.Chunks[i];
                    if (c?.Rendered == null || !c.Rendered.Dirty) continue;

                    int dx = px - ix, dy = py - iy, dz = pz - iz;
                    int dist = (dx * dx) + (dy * dy) + (dz * dz);
                    if (dist < bestDist) { bestDist = dist; bestIdx = i; }
                }

        if (bestIdx == -1)
        {
            return null;
        }

        _voxelMap.Chunks[bestIdx].Rendered.Dirty = false;

        int biz = bestIdx / (mxc * myc);
        int biy = bestIdx % (mxc * myc) / mxc;
        int bix = bestIdx % mxc;
        return (bix, biy, biz);
    }

    private static void BlocksAround7Inplace(Vector3i pos, Vector3i[] buffer)
    {
        buffer[0] = pos;
        buffer[1] = new(pos.X + 1, pos.Y, pos.Z);
        buffer[2] = new(pos.X - 1, pos.Y, pos.Z);
        buffer[3] = new(pos.X, pos.Y + 1, pos.Z);
        buffer[4] = new(pos.X, pos.Y - 1, pos.Z);
        buffer[5] = new(pos.X, pos.Y, pos.Z + 1);
        buffer[6] = new(pos.X, pos.Y, pos.Z - 1);
    }

    // ── Performance info ──────────────────────────────────────────────────────

    private void UpdatePerformanceInfo()
    {
        const float MsToSeconds = 1f / 1000f;
        float elapsed = (_gameService.TimeMillisecondsFromStart - _lastPerfUpdateMs) * MsToSeconds;
        if (elapsed < 1f) return;

        _lastPerfUpdateMs = _gameService.TimeMillisecondsFromStart;
        int updatesThisPeriod = _chunkUpdates - _lastChunkUpdatesSnapshot;
        _lastChunkUpdatesSnapshot = _chunkUpdates;

        Game.PerformanceInfo["chunk updates"] = string.Format(
            Game.Language.ChunkUpdates(), updatesThisPeriod.ToString());
        Game.PerformanceInfo["triangles"] = string.Format(
            Game.Language.Triangles(), TrianglesCount().ToString());
    }
}