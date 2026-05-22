namespace MeinKraft;

/// <summary>
/// Incremental cross-chunk lighting engine for runtime block changes.
/// Operates directly on chunk.BaseLight arrays so LightBase is never called.
/// After Update() completes, call DirtyChunks to find which chunks need
/// LightBetweenChunks re-run to refresh Rendered.Light.
///
/// Two-queue algorithm (standard Minecraft incremental light):
///   Remove queue — propagates darkness outward, re-seeds any cell that still
///                  has light from an independent source into the add queue.
///   Add queue    — propagates brightness outward from all seeds.
///
/// Runs on the single lighting thread — no locks needed.
/// </summary>
public sealed class IncrementalLightBFS
{
    private const int CS = GameConstants.CHUNK_SIZE;
    private const int CsMask = CS - 1;
    private const int CsBits = 4;   // log2(CS) — CS must be a power of two

    private readonly IVoxelMap _voxelMap;

    // Reused across calls — allocated once, cleared at start of each Update.
    private readonly Queue<LightNode> _addQueue = new(1024);
    private readonly Queue<LightNode> _removeQueue = new(1024);

    // Chunks whose BaseLight was modified this call.
    private readonly HashSet<(int cx, int cy, int cz)> _dirtyChunks = new();

    private readonly record struct LightNode(int Wx, int Wy, int Wz, byte Value);

    // Neighbour offsets — 6 face-connected directions.
    private static readonly (int dx, int dy, int dz)[] s_dirs =
    [
        ( 1, 0, 0), (-1, 0, 0),
        ( 0, 1, 0), ( 0,-1, 0),
        ( 0, 0, 1), ( 0, 0,-1),
    ];

    public IncrementalLightBFS(IVoxelMap voxelMap)
    {
        _voxelMap = voxelMap;
    }

    /// <summary>
    /// Returns the set of chunk positions whose BaseLight was modified by
    /// the most recent <see cref="Update"/> call.
    /// </summary>
    public IReadOnlySet<(int cx, int cy, int cz)> DirtyChunks => _dirtyChunks;

    // ── Public entry point ────────────────────────────────────────────────────

    /// <summary>
    /// Updates BaseLight across chunk boundaries in response to a single block
    /// change at world position (<paramref name="wx"/>, <paramref name="wy"/>,
    /// <paramref name="wz"/>). Populates <see cref="DirtyChunks"/>.
    ///
    /// Does NOT update Rendered.Light — caller must re-run LightBetweenChunks
    /// for each dirty chunk after this returns.
    /// Does NOT handle sunlight (heightmap changes) — caller must detect that
    /// case and fall back to a full LightBase relight.
    /// </summary>
    public void Update(
        int wx, int wy, int wz,
        int oldBlockId, int newBlockId,
        int[] lightRadius, bool[] transparent)
    {
        _addQueue.Clear();
        _removeQueue.Clear();
        _dirtyChunks.Clear();

        bool wasTransparent = transparent[oldBlockId];
        bool isTransparent = transparent[newBlockId];
        int oldEmission = lightRadius[oldBlockId];
        int newEmission = lightRadius[newBlockId];

        // ── Step 1: seed the remove queue ────────────────────────────────────

        if (oldEmission > newEmission || (wasTransparent && !isTransparent))
        {
            // Block is darker now — record old light and clear position.
            byte old = ReadBaseLight(wx, wy, wz);
            if (old > 0)
            {
                WriteBaseLight(wx, wy, wz, (byte)newEmission);
                _removeQueue.Enqueue(new LightNode(wx, wy, wz, old));
            }
        }

        // ── Step 2: process remove queue ─────────────────────────────────────

        while (_removeQueue.Count > 0)
        {
            LightNode n = _removeQueue.Dequeue();
            byte cur = ReadBaseLight(n.Wx, n.Wy, n.Wz);

            if (cur != 0 && cur < n.Value)
            {
                // This cell was lit by the removed source — darken it.
                WriteBaseLight(n.Wx, n.Wy, n.Wz, 0);

                foreach ((int dx, int dy, int dz) in s_dirs)
                {
                    int nx = n.Wx + dx, ny = n.Wy + dy, nz = n.Wz + dz;
                    byte nLight = ReadBaseLight(nx, ny, nz);
                    if (nLight > 0)
                        _removeQueue.Enqueue(new LightNode(nx, ny, nz, nLight));
                }
            }
            else if (cur >= n.Value)
            {
                // Independent source — re-seed the add queue from here.
                _addQueue.Enqueue(new LightNode(n.Wx, n.Wy, n.Wz, cur));
            }
        }

        // ── Step 3: seed the add queue ────────────────────────────────────────

        if (newEmission > oldEmission)
        {
            // New or stronger emitter — seed from this position.
            WriteBaseLight(wx, wy, wz, (byte)newEmission);
            _addQueue.Enqueue(new LightNode(wx, wy, wz, (byte)newEmission));
        }

        if (!wasTransparent && isTransparent)
        {
            // Path opened — seed from all neighbours that can now shine through.
            foreach ((int dx, int dy, int dz) in s_dirs)
            {
                int nx = wx + dx, ny = wy + dy, nz = wz + dz;
                byte nLight = ReadBaseLight(nx, ny, nz);
                if (nLight > 1)
                    _addQueue.Enqueue(new LightNode(nx, ny, nz, nLight));
            }
        }

        // ── Step 4: process add queue ─────────────────────────────────────────

        while (_addQueue.Count > 0)
        {
            LightNode n = _addQueue.Dequeue();
            if (n.Value == 0) continue;

            byte cur = ReadBaseLight(n.Wx, n.Wy, n.Wz);
            if (cur >= n.Value) continue;   // already bright enough

            // Skip positions that are opaque and non-emissive.
            if (!CanPassLight(n.Wx, n.Wy, n.Wz, transparent, lightRadius)) continue;

            WriteBaseLight(n.Wx, n.Wy, n.Wz, n.Value);

            if (n.Value <= 1) continue;
            byte next = (byte)(n.Value - 1);
            foreach ((int dx, int dy, int dz) in s_dirs)
                _addQueue.Enqueue(new LightNode(n.Wx + dx, n.Wy + dy, n.Wz + dz, next));
        }
    }

    // ── BaseLight read / write ────────────────────────────────────────────────

    private byte ReadBaseLight(int wx, int wy, int wz)
    {
        if (!_voxelMap.IsValidPos(wx, wy, wz)) return 0;

        int cx = wx >> CsBits, cy = wy >> CsBits, cz = wz >> CsBits;
        Chunk? c = _voxelMap.Chunks[_voxelMap.ChunkFlatIndex(cx, cy, cz)];
        if (c?.BaseLight == null) return 0;

        int lx = wx & CsMask, ly = wy & CsMask, lz = wz & CsMask;
        return c.BaseLight[lz * CS * CS + ly * CS + lx];
    }

    private void WriteBaseLight(int wx, int wy, int wz, byte value)
    {
        if (!_voxelMap.IsValidPos(wx, wy, wz)) return;

        int cx = wx >> CsBits, cy = wy >> CsBits, cz = wz >> CsBits;
        Chunk? c = _voxelMap.Chunks[_voxelMap.ChunkFlatIndex(cx, cy, cz)];
        if (c?.BaseLight == null) return;

        int lx = wx & CsMask, ly = wy & CsMask, lz = wz & CsMask;
        c.BaseLight[lz * CS * CS + ly * CS + lx] = value;
        _dirtyChunks.Add((cx, cy, cz));
    }

    private bool CanPassLight(int wx, int wy, int wz, bool[] transparent, int[] lightRadius)
    {
        if (!_voxelMap.IsValidPos(wx, wy, wz)) return false;
        int blockId = _voxelMap.GetBlock(wx, wy, wz);
        return transparent[blockId] || lightRadius[blockId] > 0;
    }
}