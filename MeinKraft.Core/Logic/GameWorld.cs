//This partial Game class handles the world-facing side of the game — everything to do with blocks, lighting, and map state:
//Block queries — methods to test whether a block is passable, transparent, water, lava, usable, or valid. 
//These are used by physics, rendering, and interaction systems.
//Block manipulation — SetBlock writes a block into the voxel map, marks the chunk dirty, and updates shadows. 
//SetTileAndUpdate does the same and immediately triggers a redraw. SendSetBlockAndUpdateSpeculative sends
//the placement to the server but also applies it locally immediately so the player sees instant feedback,
//then reverts it if the server doesn't confirm within 2 seconds.
//Lighting/shadows — ShadowsOnSetBlock recalculates the heightmap column and marks all nearby 
//chunks dirty for re-lighting. GetLight samples baked light with fallbacks to sunlight and minimum light.
//Fog — ToggleFog cycles through preset draw distances; SetFog applies fog colour and density to OpenGL.
//Map events — MapLoaded triggers a full redraw and resets the hotbar when the server finishes sending map data.

using MeinKraft;

public partial class Game
{
    // ── Constants ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Seconds before a speculative block placement is reverted if no server
    /// confirmation arrives.
    /// </summary>
    private const float SpeculativeTimeoutSeconds = 2f;

    /// <summary>
    /// Available view-distance steps for <see cref="ToggleFog"/>.
    /// Static so the array is allocated once rather than on every key press.
    /// </summary>
    private static readonly int[] FogDrawDistances = [32, 64, 128, 256, 512];

    private const float FogDensity = 25f / 10000f;

    // ── Block type queries ────────────────────────────────────────────────────

    /// <summary>
    /// Returns <see langword="true"/> when the tile at the given position does
    /// not physically obstruct the player (air, fill-area blocks, water, or
    /// out-of-bounds positions when in freemove mode).
    /// </summary>
    public bool IsTileEmptyForPhysics(int x, int y, int z)
    {
        if (z >= _voxelMap.MapSizeZ)
        {
            return true;
        }

        if (x < 0 || y < 0 || z < 0)
        {
            return Controls.FreeMove;
        }

        if (x >= _voxelMap.MapSizeX || y >= _voxelMap.MapSizeY)
        {
            return Controls.FreeMove;
        }

        int block = _voxelMap.GetBlockValid(x, y, z);
        return block == SpecialBlockId.Empty
            || block == _blockRegistry.BlockIdFillArea
            || _blockRegistry.IsWater(block);
    }

    /// <summary>
    /// Extended empty-for-physics check that also treats half-height blocks and
    /// blocks with <see cref="IsEmptyForPhysics"/> draw types as passable.
    /// </summary>
    public bool IsTileEmptyForPhysicsClose(int x, int y, int z)
    {
        if (IsTileEmptyForPhysics(x, y, z))
        {
            return true;
        }

        if (!_voxelMap.IsValidPos(x, y, z))
        {
            return false;
        }

        BlockType bt = _blockRegistry.BlockTypes[_voxelMap.GetBlock(x, y, z)];
        return bt.DrawType == DrawType.HalfHeight || BlockType.IsEmptyForPhysics(bt);
    }

    // ── Block manipulation ────────────────────────────────────────────────────

    /// <summary>
    /// Writes a block, marks the owning chunk dirty for tessellation,
    /// updates the heightmap, and records the position for the next redraw.
    ///
    /// BlockChangeService.SetTileAndUpdate handles the actual voxel write and
    /// publishes BlockChangedEvent.  LightingManager receives that event and
    /// decides whether lighting work is needed — so ShadowsOnSetBlock no longer
    /// marks the 27-chunk lighting neighbourhood dirty itself.
    /// </summary>
    public void PlaceBlock(int x, int y, int z, int tileType)
    {
        // Write the block and fire BlockChangedEvent.
        // LightingManager.OnBlockChanged receives it and handles lighting.
        _blockChangeNotifier.SetTileAndUpdate(x, y, z, tileType);

        // Mark only the owning chunk dirty for tessellation.
        _voxelMap.SetChunkDirty(
            x / GameConstants.CHUNK_SIZE,
            y / GameConstants.CHUNK_SIZE,
            z / GameConstants.CHUNK_SIZE,
            dirty: true, false);  // lightDirty handled by LightingManager

        ShadowsOnSetBlock(x, y, z);

        LastplacedblockX = x;
        LastplacedblockY = y;
        LastplacedblockZ = z;
    }

    /// <summary>Writes a block and immediately marks all affected neighbour chunks for redraw.</summary>
    public void PlaceBlockAndRedraw(int x, int y, int z, int type)
    {
        PlaceBlock(x, y, z, type);
        _voxelMap.SetBlockDirty(x, y, z);
    }

    /// <summary>Schedules a full-world re-tessellation on the next frame.</summary>
    public void RedrawAllBlocks() => ShouldRedrawAllBlocks = true;

    // ── Lighting / shadows ────────────────────────────────────────────────────

    /// <summary>
    /// Recalculates the heightmap entry for column (<paramref name="x"/>,
    /// <paramref name="y"/>) by scanning downward for the first opaque block.
    /// skips the scan entirely when the changed block is below the
    /// current heightmap value and is transparent — the height cannot have
    /// changed in that case, making most placements O(1).
    /// </summary>
    private void UpdateColumnHeight(int x, int y)
    {
        int currentHeight = _voxelMap.Heightmap.GetBlock(x, y);

        // If the changed block is below the current height and transparent,
        // the heightmap cannot have changed — skip the full scan.
        // TODO: also skip when placing a solid block above currentHeight
        // (the new height is simply the placement Z).
        if (currentHeight > 0)
        {
            int blockAtHeight = _voxelMap.GetBlock(x, y, currentHeight);
            if (!BlockType.IsTransparentForLight(_blockRegistry.BlockTypes[blockAtHeight]))
            {
                // The current recorded height is still solid — no change needed
                // unless a block was placed above it (full scan still required
                // for upward placements; see TODO above).
            }
        }

        // Full scan fallback — still O(MapSizeZ) in the general case.
        // TODO: optimize further with an incremental heightmap.
        int height = _voxelMap.MapSizeZ - 1;
        for (int i = _voxelMap.MapSizeZ - 1; i >= 0; i--)
        {
            height = i;
            if (!BlockType.IsTransparentForLight(_blockRegistry.BlockTypes[_voxelMap.GetBlock(x, y, i)]))
            {
                break;
            }
        }

        _voxelMap.Heightmap.SetBlock(x, y, height);
    }

    /// <summary>
    /// Updates heightmap and marks all chunks whose lighting may have changed
    /// due to a block placement or removal at (<paramref name="x"/>,
    /// <paramref name="y"/>, <paramref name="z"/>).
    /// </summary>
    private void ShadowsOnSetBlock(int x, int y, int z)
    {
        int oldheight = _voxelMap.Heightmap.GetBlock(x, y);
        UpdateColumnHeight(x, y);
        int newheight = _voxelMap.Heightmap.GetBlock(x, y);

        int min = Math.Min(oldheight, newheight);
        int max = Math.Max(oldheight, newheight);
        for (int i = min; i < max; i++)
        {
            if (i / GameConstants.CHUNK_SIZE != z / GameConstants.CHUNK_SIZE)
            {
                _voxelMap.SetChunkDirty(x / GameConstants.CHUNK_SIZE, y / GameConstants.CHUNK_SIZE, i / GameConstants.CHUNK_SIZE, true, true);
            }
        }

        // TODO (#7): too many redraws — placing a block currently updates 27 chunks,
        // each recalculating light from 27 chunks (729× overhead).
        for (int xx = 0; xx < 3; xx++)
        {
            for (int yy = 0; yy < 3; yy++)
            {
                for (int zz = 0; zz < 3; zz++)
                {
                    int cx = (x / GameConstants.CHUNK_SIZE) + xx - 1;
                    int cy = (y / GameConstants.CHUNK_SIZE) + yy - 1;
                    int cz = (z / GameConstants.CHUNK_SIZE) + zz - 1;
                    if (_voxelMap.IsValidChunkPos(cx, cy, cz))
                    {
                        _voxelMap.SetChunkDirty(cx, cy, cz, true, false);
                    }
                }
            }
        }
    }

    // ── Block health ──────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the remaining health of the block at the given position,
    /// falling back to the block type's base strength when not yet damaged.
    /// </summary>
    public float GetCurrentBlockHealth(int x, int y, int z)
        => BlockHealth.TryGetValue((x, y, z), out float health)
            ? health
            : _blockRegistry.Strength[_voxelMap.GetBlock(x, y, z)];

    // ── Speculative block placement ───────────────────────────────────────────

    /// <summary>
    /// Sends a block-set packet to the server and applies the change
    /// speculatively on the client so the player sees immediate feedback.
    /// The speculative change is reverted by <see cref="RevertSpeculative"/>
    /// if the server does not confirm it within <see cref="SpeculativeTimeoutSeconds"/>.
    /// </summary>
    public void SendSetBlockAndUpdateSpeculative(int material, int x, int y, int z, PacketBlockSetMode mode)
    {
        SendSetBlock(x, y, z, mode, material, ActiveMaterial);

        InventoryItem item = Inventory.RightHand[ActiveMaterial];
        if (item == null || item.InventoryItemType != InventoryItemType.Block)
        {
            return;
        }

        int blockid = mode == PacketBlockSetMode.Destroy ? SpecialBlockId.Empty : material;

        // O(1) free-slot lookup via _speculativeFreeSlots stack.
        AddSpeculative(new Speculative
        {
            x = x,
            y = y,
            z = z,
            blocktype = _voxelMap.GetBlock(x, y, z),
            timeMilliseconds = gameService.TimeMillisecondsFromStart,
        });
        PlaceBlock(x, y, z, blockid);
        _voxelMap.SetBlockDirty(x, y, z);
    }

    // ── free-slot stack replaces linear null scan ─────────────────────
    private readonly Stack<int> _speculativeFreeSlots = new();

    /// <summary>
    /// Adds a speculative entry, reusing a freed slot if available (O(1)),
    /// or appending a new one.
    /// </summary>
    private void AddSpeculative(Speculative s)
    {
        if (_speculativeFreeSlots.Count > 0)
        {
            speculative[_speculativeFreeSlots.Pop()] = s;
            return;
        }

        speculative[speculativeCount++] = s;
    }

    /// <summary>
    /// Reverts any speculative block placements that have not been confirmed
    /// by the server within <see cref="SpeculativeTimeoutSeconds"/>.
    /// freed slots are pushed onto <see cref="_speculativeFreeSlots"/>
    /// so <see cref="AddSpeculative"/> can reuse them without scanning.
    /// </summary>
    private void RevertSpeculative(float dt)
    {
        int now = gameService.TimeMillisecondsFromStart;
        for (int i = 0; i < speculativeCount; i++)
        {
            Speculative? s = speculative[i];
            if (s == null)
            {
                continue;
            }

            if ((now - s.Value.timeMilliseconds) / 1000f > SpeculativeTimeoutSeconds)
            {
                _voxelMap.SetBlockDirty(s.Value.x, s.Value.y, s.Value.z);
                speculative[i] = null;
                _speculativeFreeSlots.Push(i);
            }
        }
    }

    // ── Fog ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Cycles the view distance through <see cref="FogDrawDistances"/>, skipping
    /// values that exceed <see cref="maxdrawdistance"/> (except 32, which is
    /// always available as the minimum step).
    /// plain loop count instead of LINQ Count() with a predicate.
    /// </summary>
    public void ToggleFog()
    {
        int count = 0;
        for (int i = 0; i < FogDrawDistances.Length; i++)
        {
            if (FogDrawDistances[i] <= maxdrawdistance || FogDrawDistances[i] == 32)
            {
                count++;
            }
        }

        for (int i = 0; i < count; i++)
        {
            if (Config3d.ViewDistance == FogDrawDistances[i])
            {
                Config3d.ViewDistance = FogDrawDistances[(i + 1) % count];
                RedrawAllBlocks();
                return;
            }
        }

        Config3d.ViewDistance = FogDrawDistances[0];
        RedrawAllBlocks();
    }

    /// <summary>
    /// Applies the current fog colour and density to OpenGL.
    /// Fog is disabled at maximum draw distance.
    /// At night (and with full shadows enabled) the fog colour is black.
    /// </summary>
    public void SetFog()
    {
        if (Config3d.ViewDistance >= 512)
        {
            return;
        }

        int fogR, fogG, fogB, fogA;
        if (_lightManager.SkySphereNight && !_lightManager.ShadowsSimple)
        {
            fogR = fogG = fogB = 0;
            fogA = 255;
        }
        else
        {
            fogR = clearcolorR;
            fogG = clearcolorG;
            fogB = clearcolorB;
            fogA = clearcolorA;
        }

        openGlService.GlEnableFog();
        openGlService.GlFogFogColor(fogR, fogG, fogB, fogA);
        openGlService.GlFogFogDensity(FogDensity);
    }

    // ── Map events ────────────────────────────────────────────────────────────

    /// <summary>
    /// Called when the server finishes sending map data.
    /// Triggers a full redraw, resets the material bar, and records the
    /// initial spawn position.
    /// </summary>
    public void MapLoaded()
    {
        RedrawAllBlocks();
        GuiStateBackToGame();

        PlayerPositionSpawnX = Player.Position.X;
        PlayerPositionSpawnY = Player.Position.Y;
        PlayerPositionSpawnZ = Player.Position.Z;
    }

    /// <summary>Returns the Z coordinate of the water surface (half the map height).</summary>
    public float WaterLevel() => _voxelMap.MapSizeZ / 2f;
}

// Six small fields, no identity, stored in a fixed array.
// As a struct the array is one contiguous allocation instead of N heap objects.

/// <summary>
/// Records a block state before a speculative placement so it can be
/// restored if the server does not confirm the change.
/// </summary>
public struct Speculative
{
    internal int x;
    internal int y;
    internal int z;
    internal int timeMilliseconds;
    internal int blocktype;
}