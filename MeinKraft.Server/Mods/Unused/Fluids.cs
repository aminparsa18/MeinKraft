/* ********************************************************
 * Fluids.cs
 * Based on original by Wilfried Elmenreich (2013)
 *
 * Mod for Manic Digger
 * Makes fluid blocks move downwards/sideways to level a pool.
 *
 * Changes from original:
 *  - HashSet<(int,int,int)> replaces Dictionary<int,Vector3i>
 *    — no hash collisions, position is the key
 *  - Timer fires every server tick; throttled internally via
 *    GetCurrentTick() + SimTickInterval
 *  - Pre-allocated _tickSnapshot and _visited — zero allocation
 *    per tick after startup
 *  - RegisterOnLoad used for world-load scan (safer timing than
 *    IModEvents.LoadWorld)
 *  - PopulateChunk scans newly generated chunks immediately
 *  - MoveFluid() centralises the move+notify pattern
 *  - MaxEntries enforced in AddActiveFluid
 *  - Cake easter egg uses Water variable instead of hardcoded 8
 * ******************************************************** */

using OpenTK.Mathematics;

namespace MeinKraft.Mods;

public class Fluids : IMod
{
    private readonly Random _random = new();
    private readonly HashSet<(int x, int y, int z)> _activeFluids = [];
    private readonly List<(int x, int y, int z)> _tickSnapshot = [];
    private readonly HashSet<(int x, int y, int z)> _visited = [];
    private IServerModManager m;
    private int Water, Lava, _chunkSize;

    // Timer fires every server tick — throttle internally with GetCurrentTick().
    // Adjust SimTickInterval to taste: 12 ≈ 5 Hz at a 60 TPS server.
    private long _lastSimTick;
    private const long SimTickInterval = 12;
    public int MaxEntries = 100_000;

    private readonly int[] dx = { -1, 0, 1, 1, 1, 0, -1, -1 };
    private readonly int[] dy = { 1, 1, 1, 0, -1, -1, -1, 0 };

    public void PreStart(IServerModManager m) => m.RequireMod("Default");

    public void Start(IServerModManager m, IModEvents modEvents)
    {
        this.m = m;
        _chunkSize = m.GetChunkSize();
        Water = m.GetBlockId("Water");
        Lava = m.GetBlockId("Lava");

        modEvents.BlockBuild += OnBlockBuild;
        modEvents.BlockDelete += OnBlockDelete;
        modEvents.BlockUpdate += OnBlockUpdate;

        // PopulateChunk fires for freshly generated chunks — scan them so
        // any water placed by the world generator starts simulating immediately.
        modEvents.PopulateChunk += OnPopulateChunk;

        // interval value is irrelevant — fires every tick; throttled inside
        m.RegisterTimer(UpdateFluids, 1);
    }

    // ── New chunk scan (generation only) ─────────────────────────────────────
    // Existing world water is already at rest and needs no startup scan.
    // It will be activated by OnBlockDelete/OnBlockUpdate when a neighbouring
    // block is removed and Check() finds it has somewhere to flow.

    private void OnPopulateChunk(PopulateChunkArgs args)
    {
        int baseX = args.X * _chunkSize;
        int baseY = args.Y * _chunkSize;
        int baseZ = args.Z * _chunkSize;
        for (int lx = 0; lx < _chunkSize; lx++)
            for (int ly = 0; ly < _chunkSize; ly++)
                for (int lz = 0; lz < _chunkSize; lz++)
                {
                    int b = m.GetBlock(baseX + lx, baseY + ly, baseZ + lz);
                    if (b == Water || b == Lava)
                        AddActiveFluid(baseX + lx, baseY + ly, baseZ + lz);
                }
    }

    // ── Block events ──────────────────────────────────────────────────────────

    private void OnBlockBuild(BlockBuildArgs args)
    {
        int b = m.GetBlock(args.X, args.Y, args.Z);
        if (b == Water || b == Lava)
            AddActiveFluid(args.X, args.Y, args.Z);
    }

    private void OnBlockDelete(BlockDeleteArgs args) =>
        OnBlockUpdate(new BlockUpdateArgs { X = args.X, Y = args.Y, Z = args.Z });

    private void OnBlockUpdate(BlockUpdateArgs args)
    {
        for (int xx = args.X - 1; xx <= args.X + 1; xx++)
            for (int yy = args.Y - 1; yy <= args.Y + 1; yy++)
                for (int zz = args.Z - 1; zz <= args.Z + 1; zz++)
                    Check(xx, yy, zz);
    }

    private void Check(int x, int y, int z)
    {
        if (!m.IsValidPos(x, y, z)) return;
        int b = m.GetBlock(x, y, z);

        if (m.GetBlockNameAt(x, y, z) == "Cake")
        {
            m.SetBlock(x, y, z, Water);
            b = Water;
        }

        if ((b != Water && b != Lava) || z <= 0) return;

        if (m.GetBlock(x, y, z - 1) == 0) { AddActiveFluid(x, y, z); return; }

        for (int dd = 0; dd < dx.Length; dd++)
        {
            int xx = x + dx[dd], yy = y + dy[dd];
            if (!m.IsValidPos(xx, yy, z)) continue;
            if (m.GetBlock(xx, yy, z) == 0 && m.GetBlock(xx, yy, z - 1) == 0)
            { AddActiveFluid(x, y, z); return; }
        }

        if (m.GetBlock(x, y, z - 1) != b)
        {
            for (int dd = 1; dd < dx.Length; dd += 2)
            {
                int xx = x + dx[dd], yy = y + dy[dd];
                if (!m.IsValidPos(xx, yy, z - 1)) continue;
                if (m.GetBlock(xx, yy, z) != 0) continue;
                if (m.GetBlock(xx, yy, z - 1) == b)
                { AddActiveFluid(x, y, z); return; }
            }
        }

        for (int dd = 1; dd < dx.Length; dd += 2)
        {
            int xx = x + dx[dd], yy = y + dy[dd];
            if (!m.IsValidPos(xx, yy, z - 1)) continue;
            if (m.GetBlock(xx, yy, z) != 0) continue;
            AddActiveFluid(x, y, z); return;
        }
    }

    // ── Simulation tick ───────────────────────────────────────────────────────

    private void UpdateFluids()
    {
        // Throttle — timer fires every server tick, we only want ~5 Hz
        long tick = m.GetCurrentTick();
        if (tick - _lastSimTick < SimTickInterval) return;
        _lastSimTick = tick;

        if (_activeFluids.Count == 0) return;

        // Snapshot into pre-allocated buffer — no allocation, no mutation-during-iteration
        _tickSnapshot.Clear();
        _tickSnapshot.AddRange(_activeFluids);

        foreach (var (x, y, z) in _tickSnapshot)
        {
            if (!_activeFluids.Contains((x, y, z))) continue;

            if (Update(x, y, z)) continue;

            if (!LowestFreeSpace(x, y, z, z)) { _activeFluids.Remove((x, y, z)); continue; }
            Vector3i b1 = _foundBlock;
            if (!HighestFluidBlock(x, y, z, b1.Z + 1)) { _activeFluids.Remove((x, y, z)); continue; }
            Vector3i b2 = _foundBlock;

            m.SetBlock(b1.X, b1.Y, b1.Z, _searchMedium);
            m.SetBlock(b2.X, b2.Y, b2.Z, 0);
            OnBlockUpdate(new BlockUpdateArgs { X = b2.X, Y = b2.Y, Z = b2.Z });
            AddActiveFluid(b1.X, b1.Y, b1.Z);
        }
    }

    private bool Update(int x, int y, int z)
    {
        int b = m.GetBlock(x, y, z);
        if (b != Water && b != Lava) return false;
        if (z <= 0) return false;

        // Free fall — water skips an extra block if possible
        if (m.GetBlock(x, y, z - 1) == 0)
        {
            int target = (b == Water && z >= 2 && m.GetBlock(x, y, z - 2) == 0) ? z - 2 : z - 1;
            MoveFluid(x, y, z, x, y, target, b);
            return true;
        }

        // Fall over edge (randomised direction to avoid directional bias)
        int r = _random.Next(8);
        for (int d = r; d < r + dx.Length; d++)
        {
            int dd = d % dx.Length;
            int xx = x + dx[dd], yy = y + dy[dd];
            if (!m.IsValidPos(xx, yy, z)) continue;
            if (m.GetBlock(xx, yy, z) == 0 && m.GetBlock(xx, yy, z - 1) == 0)
            {
                MoveFluid(x, y, z, xx, yy, z - 1, b);
                return true;
            }
        }

        // Cohesion — slide toward same fluid on the level below
        if (m.GetBlock(x, y, z - 1) != b)
        {
            r = _random.Next(4);
            for (int d = r; d < r + 4; d++)
            {
                int dd = (1 + 2 * d) % dx.Length;
                int xx = x + dx[dd], yy = y + dy[dd];
                if (!m.IsValidPos(xx, yy, z - 1)) continue;
                if (m.GetBlock(xx, yy, z) != 0) continue;
                if (m.GetBlock(xx, yy, z - 1) == b)
                {
                    MoveFluid(x, y, z, xx, yy, z, b);
                    return true;
                }
            }
        }

        return false;
    }

    // Centralises the move + notify pattern
    private void MoveFluid(int fx, int fy, int fz, int tx, int ty, int tz, int blockType)
    {
        m.SetBlock(tx, ty, tz, blockType);
        m.SetBlock(fx, fy, fz, 0);
        RemoveActiveFluid(fx, fy, fz);
        AddActiveFluid(tx, ty, tz);
        OnBlockUpdate(new BlockUpdateArgs { X = fx, Y = fy, Z = fz });
    }

    private void AddActiveFluid(int x, int y, int z)
    {
        if (_activeFluids.Count >= MaxEntries) return;
        _activeFluids.Add((x, y, z));
    }

    private void RemoveActiveFluid(int x, int y, int z) =>
        _activeFluids.Remove((x, y, z));

    // ── Levelling search ──────────────────────────────────────────────────────

    private Vector3i _foundBlock;
    private int _searchZ, _searchMedium, _searchTarget, _searchDir;
    private bool _found, _stopOnFound;

    private bool LowestFreeSpace(int x, int y, int z, int zReq)
    {
        if (!m.IsValidPos(x, y, z)) return false;
        _searchMedium = m.GetBlock(x, y, z);
        if (!m.IsBlockFluid(_searchMedium)) return false;
        _searchDir = -1; _searchZ = zReq; _found = false; _stopOnFound = false; _searchTarget = 0;
        _visited.Clear();
        RecursiveSearch(_searchMedium == Water ? 25 : 10, x, y, z);
        return _found;
    }

    private bool HighestFluidBlock(int x, int y, int z, int zReq)
    {
        if (!m.IsValidPos(x, y, z)) return false;
        _searchMedium = m.GetBlock(x, y, z);
        if (!m.IsBlockFluid(_searchMedium)) return false;
        _searchDir = 1; _searchZ = zReq; _found = false; _stopOnFound = true; _searchTarget = _searchMedium;
        _visited.Clear();
        RecursiveSearch(_searchMedium == Water ? 25 : 10, x, y, z);
        return _found;
    }

    private void RecursiveSearch(int depth, int x, int y, int z)
    {
        if (depth == 0 || (_found && _stopOnFound)) return;
        if (!m.IsValidPos(x, y, z)) return;

        if (m.GetBlock(x, y, z) == _searchTarget && (z - _searchZ) * _searchDir >= 0)
        {
            int zz = z;
            if (_searchMedium == _searchTarget)
            {
                zz = z + _searchDir;
                while (zz >= 0 && zz < m.GetMapSizeZ() && m.GetBlock(x, y, zz) == _searchMedium)
                    zz += _searchDir;
                zz -= _searchDir;
            }
            if (!_found || (zz - _foundBlock.Z) * _searchDir > 0)
            {
                _foundBlock = new Vector3i(x, y, zz);
                _found = true;
                if (_stopOnFound) return;
            }
        }

        if (m.GetBlock(x, y, z) != _searchMedium) return;
        if (!_visited.Add((x, y, z))) return;

        depth--;
        RecursiveSearch(depth, x, y, z + _searchDir);
        int r = _random.Next(4);
        for (int d = r; d < r + 4; d++)
        {
            int dd = (1 + 2 * d) % dx.Length;
            RecursiveSearch(depth, x + dx[dd], y + dy[dd], z);
        }
        RecursiveSearch(depth, x, y, z - _searchDir);
    }
}