//This class is a render batch manager — it groups 3D models by texture so the GPU doesn't have to switch textures 
//constantly (which is expensive). You register models with Add, they get a slot ID, and every frame Draw renders
//everything in two passes: solid geometry first (to fill the depth buffer), then transparent geometry 
//(water, glass, etc.) on top with back-face culling disabled so both sides of surfaces show.

using System.Buffers;
using System.Collections.Concurrent;

/// <inheritdoc/>
public class MeshBatcher : IMeshBatcher
{
    /// <summary>Maximum number of model slots in the pool.</summary>
    private const int ModelsMax = 1024 * 16;

    /// <summary>Maximum number of distinct textures that can be batched.</summary>
    private const int MaxTextures = 10;
      
    private readonly IOpenGlService _openGlService;
    private readonly IMeshDrawer _meshDrawer;
    private readonly IGameLogger _gameLogger;

    /// <summary>
    /// When <c>true</c>, <see cref="Draw"/> will bind each texture before issuing
    /// draw calls. Set to <c>false</c> when the caller manages texture binding externally.
    /// </summary>
    private readonly bool BindTexture;

    /// <summary>Flat array of all model slots. Index == model ID.</summary>
    private readonly BatchEntry[] _models;

    /// <summary>One-past-the-last used slot index; grows on <see cref="Add"/> when free-list is empty.</summary>
    private int _modelsCount;

    /// <summary>Free-list of previously released slot indices available for reuse.</summary>
    private readonly Stack<int> _freeSlots;

    /// <summary>
    /// Maps OpenGL texture handles to their logical texture index.
    /// Replaces the linear <c>IndexOf</c> scan with an O(1) lookup and removes
    /// the "value 0 = unoccupied" convention that treated handle 0 as invalid.
    /// </summary>
    private readonly Dictionary<int, int> _textureIndexMap;

    /// <summary>Ordered list of registered texture handles, indexed by logical texture ID.</summary>
    private readonly List<int> _glTextures;

    private readonly List<GeometryModel>[] _tocallSolid;
    private readonly List<GeometryModel>[] _tocallTransparent;

    /// <summary>
    /// Initialises a new <see cref="MeshBatcher"/> with pre-allocated model slots.
    /// </summary>
    public MeshBatcher(IOpenGlService platform, IMeshDrawer meshDrawer, IGameLogger gameLogger)
    {
        _openGlService = platform;
        this._meshDrawer = meshDrawer;
        _gameLogger = gameLogger;
        _models = new BatchEntry[ModelsMax];

        _modelsCount = 0;
        _freeSlots = new Stack<int>();
        _textureIndexMap = new Dictionary<int, int>(MaxTextures);
        _glTextures = new List<int>(MaxTextures);

        _tocallSolid = new List<GeometryModel>[MaxTextures];
        _tocallTransparent = new List<GeometryModel>[MaxTextures];
        for (int i = 0; i < MaxTextures; i++)
        {
            _tocallSolid[i] = [];
            _tocallTransparent[i] = [];
        }

        BindTexture = true;
    }

    /// <inheritdoc/>
    public int Add(GeometryModel modelData, bool transparent, int texture,
                   float centerX, float centerY, float centerZ, float radius)
    {
        int id = _freeSlots.Count > 0
            ? _freeSlots.Pop()
            : _modelsCount++;

        _models[id] = new BatchEntry
        {
            IndicesCount = modelData.IndicesCount,
            Transparent = transparent,
            Empty = false,
            Texture = GetTextureId(texture),
            Model = _openGlService.CreateModel(modelData),
        };

        return id;
    }

    /// <inheritdoc/>
    public void Remove(int id)
    {
        _openGlService.DeleteModel(_models[id].Model);
        _models[id] = _models[id] with { Empty = true };
        _freeSlots.Push(id);
    }

    /// <inheritdoc/>
    public void Draw(float playerPositionX, float playerPositionY, float playerPositionZ)
    {
        SortListsByTexture();

        // Solid pass: fills the depth buffer before any transparency is drawn.
        for (int i = 0; i < _glTextures.Count; i++)
        {
            if (_tocallSolid[i].Count == 0)
            {
                continue;
            }

            if (BindTexture)
            {
                _openGlService.BindTexture2d(_glTextures[i]);
            }

            _meshDrawer.DrawModels(_tocallSolid[i], _tocallSolid[i].Count);
        }

        // Transparent pass: back-face culling disabled so water surfaces etc. render correctly.
        _openGlService.GlDisableCullFace();
        for (int i = 0; i < _glTextures.Count; i++)
        {
            if (_tocallTransparent[i].Count == 0)
            {
                continue;
            }

            if (BindTexture)
            {
                _openGlService.BindTexture2d(_glTextures[i]);
            }

            _meshDrawer.DrawModels(_tocallTransparent[i], _tocallTransparent[i].Count);
        }

        _openGlService.GlEnableCullFace();
    }

    /// <summary>
    /// Returns the logical texture index for <paramref name="glTexture"/>,
    /// registering it if not already present.
    /// </summary>
    /// <param name="glTexture">The OpenGL texture handle to look up or register.</param>
    /// <returns>The logical index of <paramref name="glTexture"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when adding a new texture would exceed <see cref="MaxTextures"/>.
    /// </exception>
    private int GetTextureId(int glTexture)
    {
        if (_textureIndexMap.TryGetValue(glTexture, out int id))
        {
            return id;
        }

        if (_glTextures.Count >= MaxTextures)
        {
            throw new InvalidOperationException(
                $"MeshBatcher exceeded the maximum of {MaxTextures} distinct textures.");
        }

        id = _glTextures.Count;
        _glTextures.Add(glTexture);
        _textureIndexMap[glTexture] = id;
        return id;
    }

    /// <summary>
    /// Clears and repopulates the per-texture solid and transparent draw lists
    /// by iterating active model slots and bucketing them by texture index.
    /// </summary>
    private void SortListsByTexture()
    {
        for (int i = 0; i < _glTextures.Count; i++)
        {
            _tocallSolid[i].Clear();
            _tocallTransparent[i].Clear();
        }

        for (int i = 0; i < _modelsCount; i++)
        {
            ref readonly BatchEntry entry = ref _models[i];

            if (entry.Empty)
            {
                continue;
            }

            List<GeometryModel> bucket = entry.Transparent
                ? _tocallTransparent[entry.Texture]
                : _tocallSolid[entry.Texture];

            bucket.Add(entry.Model);
        }
    }

    /// <inheritdoc/>
    public int TotalTriangleCount()
    {
        int sum = 0;
        for (int i = 0; i < _modelsCount; i++)
        {
            ref readonly BatchEntry entry = ref _models[i];
            if (!entry.Empty)
            {
                sum += entry.IndicesCount;
            }
        }

        return sum / 3;
    }

    /// <inheritdoc/>
    public void Clear()
    {
        for (int i = 0; i < _modelsCount; i++)
        {
            if (!_models[i].Empty)
            {
                Remove(i);
            }
        }

        _glTextures.Clear();
        _textureIndexMap.Clear();
    }

    private readonly ConcurrentQueue<PendingUpload> _pendingUploads = new();
    private readonly ConcurrentQueue<Chunk> _pendingUnloads = new();

    public void StageChunk(Chunk chunk, VerticesIndicesToLoad[] meshes, int meshCount, bool dataRented)
        => _pendingUploads.Enqueue(new PendingUpload(chunk, meshes, meshCount, dataRented));

    public void StageUnload(Chunk chunk)
    => _pendingUnloads.Enqueue(chunk);

    // Modify FlushPendingUploads to also drain unloads first
    public void FlushPendingUploads(int maxUploadsPerFrame = 512)
    {
        // Unloads first — free slots before filling them
        while (_pendingUnloads.TryDequeue(out Chunk chunk))
        {
            RenderedChunk rendered = chunk.Rendered;
            if (rendered == null) continue;

            for (int k = 0; k < rendered.IdsCount; k++)
                Remove(rendered.Ids[k]);

            rendered.Ids = null;
            rendered.Dirty = true;

            if (rendered.LightRented && rendered.Light != null)
            {
                ArrayPool<byte>.Shared.Return(rendered.Light);
                rendered.LightRented = false;
            }

            rendered.Light = null;
        }

        // Then uploads
        int count = 0;
        while (count++ < maxUploadsPerFrame && _pendingUploads.TryDequeue(out PendingUpload p))
        {
            DoRedraw(p.Chunk, p.Meshes, p.MeshCount);
            if (p.DataRented)
                ArrayPool<VerticesIndicesToLoad>.Shared.Return(p.Meshes);
        }
    }

    private readonly float _sqrt3Half = MathF.Sqrt(3) * 0.5f;

    private void DoRedraw(Chunk chunk, VerticesIndicesToLoad[] meshes, int meshCount)
    {
        RenderedChunk rendered = chunk.Rendered;

        if (rendered?.Ids != null)
            for (int i = 0; i < rendered.IdsCount; i++)
                Remove(rendered.Ids[i]);

        int count = 0;
        Span<int> ids = stackalloc int[meshCount];

        for (int i = 0; i < meshCount; i++)
        {
            VerticesIndicesToLoad submesh = meshes[i];
            if (submesh.ModelData.IndicesCount == 0) continue;

            float cx = submesh.PositionX + GameConstants.CHUNK_SIZE * 0.5f;
            float cy = submesh.PositionZ + GameConstants.CHUNK_SIZE * 0.5f;
            float cz = submesh.PositionY + GameConstants.CHUNK_SIZE * 0.5f;

            ids[count++] = Add(submesh.ModelData, submesh.Transparent, submesh.Texture,
                               cx, cy, cz, _sqrt3Half * GameConstants.CHUNK_SIZE);
        }

        rendered.Ids = ids[..count].ToArray();
        rendered.IdsCount = count;
    }

    private readonly record struct PendingUpload(
    Chunk Chunk,
    VerticesIndicesToLoad[] Meshes,
    int MeshCount,
    bool DataRented);
}

/// <summary>
/// Represents a single occupied slot in a <see cref="MeshBatcher"/> pool,
/// storing all data needed to cull and draw one model.
/// Stored as a <c>readonly record struct</c> so all slots are embedded inline
/// in the <c>_models[]</c> array — no per-slot heap allocation.
/// </summary>
public readonly record struct BatchEntry
{
    /// <summary>
    /// Whether this slot is free and available for reuse.
    /// <c>true</c> after <see cref="MeshBatcher.Remove"/> is called.
    /// </summary>
    public bool Empty { get; init; }

    /// <summary>Total index count of the model's geometry (triangle count × 3).</summary>
    public int IndicesCount { get; init; }

    /// <summary>
    /// Whether this model belongs to the transparent render pass.
    /// <c>false</c> = solid pass (drawn first); <c>true</c> = transparent pass.
    /// </summary>
    public bool Transparent { get; init; }

    /// <summary>Logical index into the batcher's texture slot list.</summary>
    public int Texture { get; init; }

    /// <summary>The GPU model handle issued by the platform layer.</summary>
    public GeometryModel Model { get; init; }
}