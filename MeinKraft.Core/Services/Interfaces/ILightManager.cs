using MeinKraft.Extensions;
using MeinKraft.Worker;
using MessagePipe;
using OpenTK.Mathematics;

namespace MeinKraft;

public interface ILightManager
{
    // ── Existing rendering properties ─────────────────────────────────────────

    /// <summary>Maps light level (0–15) to a GL colour multiplier.</summary>
    float[] LightLevels { get; set; }

    /// <summary>Current sun light level (0–15).</summary>
    int Sunlight { get; set; }

    /// <summary>Per-level night light multipliers.</summary>
    int[] NightLevels { get; set; }

    /// <summary>World-space position of the sun billboard.</summary>
    Vector3 sunPosition { get; set; }

    /// <summary>World-space position of the moon billboard.</summary>
    Vector3 moonPosition { get; set; }

    /// <summary>Whether it is currently night-time.</summary>
    bool isNight { get; set; }

    /// <summary>Whether the fancy sky-sphere shader is enabled.</summary>
    bool fancySkysphere { get; set; }

    /// <summary>Whether the night sky-sphere variant is active.</summary>
    bool SkySphereNight { get; set; }

    /// <summary>Whether simple (non-smooth) shadows are used.</summary>
    bool ShadowsSimple { get; set; }

    /// <summary>Returns the light level at the given world position.</summary>
    int GetLight(int x, int y, int z);

    // ── Lighting pipeline ─────────────────────────────────────────────────────

    /// <summary>
    /// Full relight for a freshly loaded or generated chunk.
    /// Enqueues a LightingChunkWorkItem so work runs on the lighting thread.
    /// Call this from the chunk streaming path, not from block-update paths.
    /// </summary>
    void OnChunkLoaded(int cx, int cy, int cz, Chunk chunk);
}

public sealed class LightManager : ILightManager, IDisposable
{
    public float[] LightLevels { get; set; }
    public int Sunlight { get; set; }
    public int[] NightLevels { get; set; }
    public Vector3 sunPosition { get; set; }
    public Vector3 moonPosition { get; set; }
    public bool isNight { get; set; }
    public bool fancySkysphere { get; set; }
    public bool SkySphereNight { get; set; }
    public bool ShadowsSimple { get; set; }

    private readonly IVoxelMap _voxelMap;
    private readonly IBlockRegistry _blockRegistry;
    private readonly Lazy<ILightingWorkQueue> _lightingQueue;
    private readonly IncrementalLightBFS _incrementalBFS;
    private readonly IDisposable _subscription;

    // Block-type lookup caches — kept in sync with IBlockRegistry.
    private readonly int[] _lightRadius = new int[GameConstants.MAX_BLOCKTYPES];
    private readonly bool[] _transparent = new bool[GameConstants.MAX_BLOCKTYPES];
    private bool _cacheDirty = true;

    public LightManager(
        IVoxelMap voxelMap,
        IBlockRegistry blockRegistry,
        Lazy<ILightingWorkQueue> lightingQueue,
        ISubscriber<BlockChangedEvent> subscriber)
    {
        _voxelMap = voxelMap;
        _blockRegistry = blockRegistry;
        _lightingQueue = lightingQueue;
        _incrementalBFS = new IncrementalLightBFS(voxelMap);

        Sunlight = 15;
        LightLevels = new float[16];
        for (int i = 0; i < 16; i++)
            LightLevels[i] = 0.15f;

        var bag = DisposableBag.CreateBuilder();
        subscriber.Subscribe(OnBlockChanged).AddTo(bag);
        _subscription = bag.Build();
    }

    // ── ILightManager ─────────────────────────────────────────────────────────

    public int GetLight(int x, int y, int z)
    {
        int light = _voxelMap.MaybeGetLight(x, y, z);
        if (light != -1) return light;

        if (x >= 0 && x < _voxelMap.MapSizeX
         && y >= 0 && y < _voxelMap.MapSizeY
         && z >= _voxelMap.Heightmap.GetBlock(x, y))
            return Sunlight;

        return GameConstants.minlight;
    }

    public void OnChunkLoaded(int cx, int cy, int cz, Chunk chunk)
    {
        chunk.Rendered ??= new RenderedChunk();
        chunk.BaseLightDirty = true;
        _lightingQueue.Value.EnqueueAsync(new LightingChunkWorkItem(cx, cy, cz, chunk));
    }

    // ── Block change routing ──────────────────────────────────────────────────

    private void OnBlockChanged(BlockChangedEvent e)
    {
        RefreshCache();

        bool wasTransparent = _transparent[e.OldBlockId];
        bool isTransparent = _transparent[e.NewBlockId];
        int oldEmission = _lightRadius[e.OldBlockId];
        int newEmission = _lightRadius[e.NewBlockId];

        bool lightAffected = wasTransparent != isTransparent
                          || oldEmission != newEmission;

        if (!lightAffected) return;

        // If the change affects the sunlight column (at or above heightmap),
        // the full LightBase reseed is required — use the existing full relight.
        if (AffectsSunlight(e.WorldX, e.WorldY, e.WorldZ, wasTransparent, isTransparent))
        {
            EnqueueFullRelight(e.WorldX, e.WorldY, e.WorldZ);
            return;
        }

        // Incremental path — update BaseLight only, then refresh Rendered.Light
        // per affected chunk via LightBetweenChunks (no LightBase).
        _incrementalBFS.Update(
            e.WorldX, e.WorldY, e.WorldZ,
            e.OldBlockId, e.NewBlockId,
            _lightRadius, _transparent);

        foreach ((int cx, int cy, int cz) in _incrementalBFS.DirtyChunks)
        {
            Chunk? chunk = _voxelMap.GetChunkAt(cx, cy, cz);
            if (chunk == null) continue;

            _lightingQueue.Value.EnqueueAsync(
                new RelightBetweenChunksWorkItem(cx, cy, cz, chunk));
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true when the block change is at or above the sunlight heightmap
    /// for column (wx, wy), meaning LightBase must reseed the sunlight column.
    /// </summary>
    private bool AffectsSunlight(int wx, int wy, int wz,
                                  bool wasTransparent, bool isTransparent)
    {
        // Only transparency changes can open or close a sunlight column.
        if (wasTransparent == isTransparent) return false;

        int height = _voxelMap.Heightmap.GetBlock(wx, wy);
        return wz >= height;
    }

    private void EnqueueFullRelight(int wx, int wy, int wz)
    {
        int cx = wx / GameConstants.CHUNK_SIZE;
        int cy = wy / GameConstants.CHUNK_SIZE;
        int cz = wz / GameConstants.CHUNK_SIZE;

        Chunk? chunk = _voxelMap.GetChunkAt(cx, cy, cz);
        if (chunk == null) return;

        chunk.BaseLightDirty = true;
        _lightingQueue.Value.EnqueueAsync(new LightingChunkWorkItem(cx, cy, cz, chunk));
    }

    private void RefreshCache()
    {
        if (!_cacheDirty) return;
        foreach ((int id, BlockType blockType) in _blockRegistry.BlockTypes)
        {
            _lightRadius[id] = blockType.LightRadius;
            _transparent[id] = blockType.DrawType
                is not DrawType.Solid
                and not DrawType.ClosedDoor;
        }
        _cacheDirty = false;
    }

    public void InvalidateCache() => _cacheDirty = true;

    public void Dispose() => _subscription.Dispose();
}
