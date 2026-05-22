using MeinKraft;
using OpenTK.Mathematics;
using System.Numerics;
using Vector3 = OpenTK.Mathematics.Vector3;

public partial class Game
{
    // ── Map loading ───────────────────────────────────────────────────────────

    /// <summary>Enters map-loading state, locks the mouse, and initialises progress tracking.</summary>
    public void MapLoadingStart()
    {
        GuiState = GameState.MapLoading;
        maploadingprogress = new MapLoadingProgressEventArgs();
        FontMapLoading = GameFonts.Default;
        SetFreeMouse(true);
    }

    /// <summary>
    /// Updates the map-loading progress in-place rather than allocating a new
    /// <see cref="MapLoadingProgressEventArgs"/> on every incoming chunk.
    /// </summary>
    public void InvokeMapLoadingProgress(int progressPercent, int progressBytes, string status)
    {
        maploadingprogress.ProgressPercent = progressPercent;
        maploadingprogress.ProgressBytes = progressBytes;
        maploadingprogress.ProgressStatus = status;
    }

    // ── Screen / layout helpers ───────────────────────────────────────────────

    /// <summary>Returns the X coordinate that centres a region of <paramref name="width"/> pixels.</summary>
    public int Xcenter(float width) => (gameService.CanvasWidth / 2) - ((int)width / 2);

    /// <summary>Returns the Y coordinate that centres a region of <paramref name="height"/> pixels.</summary>
    public int Ycenter(float height) => (gameService.CanvasHeight / 2) - ((int)height / 2);

    /// <summary>
    /// UI scale factor. Returns a width-relative scale on small screens
    /// (mobile) and 1 on desktop.
    /// </summary>
    public float Scale()
        => gameService.IsSmallScreen() ? gameService.CanvasWidth / 1280f : 1f;

    // ── Projection ────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a perspective projection matrix from <paramref name="fov"/> and
    /// <paramref name="zfar"/>, uploads it to the GPU, and caches it in
    /// <see cref="CameraMatrix"/>.
    /// </summary>
    public void Set3dProjection(float zfar, float fov)
    {
        float aspect = gameService.CanvasWidth / (float)gameService.CanvasHeight;
        Matrix4.CreatePerspectiveFieldOfView(fov, aspect, znear, zfar, out Matrix4 projection);
        CameraMatrix.LastProjectionMatrix = projection;
        meshDrawer.GLMatrixModeProjection();
        meshDrawer.GLLoadMatrix(projection);
        meshDrawer.SetMatrixUniformProjection();
    }

    /// <summary>Returns the far-clip distance for the current view distance setting.</summary>
    public float Zfar()
        => Config3d.ViewDistance >= 256
            ? Config3d.ViewDistance * 2
            : ENABLE_ZFAR ? Config3d.ViewDistance : 99999;

    /// <summary>Sets the 3D projection using the current far-clip and FOV.</summary>
    internal void Set3dProjection1(float zfar_) => Set3dProjection(zfar_, CurrentFov());

    /// <summary>Sets the 3D projection using <see cref="Zfar"/> and the current FOV.</summary>
    internal void Set3dProjection2() => Set3dProjection1(Zfar());

    // ── Texture helpers ───────────────────────────────────────────────────────

    /// <summary>Draws a full-size 2D quad using a named PNG asset.</summary>
    public void Draw2dBitmapFile(string filename, float x, float y, float w, float h)
        => Draw2dTexture(GetTexture(filename), x, y, w, h, null, 0,
            ColorUtils.ColorFromArgb(255, 255, 255, 255), false);

    /// <summary>
    /// Returns <paramref name="family"/> if it is in the allowed font list,
    /// otherwise falls back to the first allowed font.
    /// Uses index access instead of <c>First()</c> to avoid allocating an enumerator.
    /// </summary>
    public string ValidFont(string family)
        => AllowedFonts.Contains(family) ? family : AllowedFonts[0];

    // ── Inventory ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the block ID in the given hotbar slot, or
    /// <see cref="BlockRegistry.BlockIdDirt"/> when the slot is empty.
    /// </summary>
    public int MaterialSlots(int i)
    {
        InventoryItem item = Inventory.RightHand[i];
        if (item != null && item.InventoryItemType == InventoryItemType.Block)
        {
            return item.BlockId;
        }

        return _blockRegistry.BlockIdDirt;
    }

    /// <summary>Replaces the active inventory with the server-sent packet and notifies the util layer.</summary>
    public void UseInventory(Packet_Inventory packet_Inventory)
    {
        Inventory = packet_Inventory;
        InventoryUtil.UpdateInventory(packet_Inventory);
    }

    // ── Dialog helpers ────────────────────────────────────────────────────────

    public int MapSizeX => _voxelMap.MapSizeX;
    public int MapSizeY => _voxelMap.MapSizeY;
    public int MapSizeZ => _voxelMap.MapSizeZ;

    public bool EnableDraw2d { get => ENABLE_DRAW2D; set => ENABLE_DRAW2D = value; }


    /// <summary>
    /// Returns the index of the dialog with key <paramref name="name"/>,
    /// or -1 if no such dialog is active.
    /// </summary>
    public int GetDialogId(string name)
    {
        for (int i = 0; i < Dialogs.Count; i++)
        {
            if (Dialogs[i]?.Key == name)
            {
                return i;
            }
        }

        return -1;
    }

    // ── Entity helpers ────────────────────────────────────────────────────────

    /// <summary>Appends <paramref name="entity"/> to the local entity list.</summary>
    public void EntityAddLocal(Entity entity) => Entities.Add(entity);

    /// <summary>
    /// Returns the entity index of the followed player, or
    /// <see langword="null"/> when no follow target is set or found.
    /// </summary>
    public int? FollowId()
    {
        if (Follow == null)
        {
            return null;
        }

        for (int i = 0; i < Entities.Count; i++)
        {
            if (Entities[i]?.DrawName?.Name == Follow)
            {
                return i;
            }
        }

        return null;
    }



    // ── Lighting / colour ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns the ambient terrain tint colour for the current camera position
    /// (blue underwater, orange in lava, white normally).
    /// </summary>
    internal int Terraincolor()
    {
        if (WaterSwimmingCamera())
        {
            return ColorUtils.ColorFromArgb(255, 78, 95, 140);
        }

        if (LavaSwimmingCamera())
        {
            return ColorUtils.ColorFromArgb(255, 222, 101, 46);
        }

        return ColorUtils.ColorFromArgb(255, 255, 255, 255);
    }

    /// <summary>Uploads <paramref name="color"/> as the OpenGL ambient light value.</summary>
    internal void SetAmbientLight(int color)
        => openGlService.GlLightModelAmbient(
            ColorUtils.ColorR(color),
            ColorUtils.ColorG(color),
            ColorUtils.ColorB(color));

    // ── Sky clear colour ──────────────────────────────────────────────────────

    // These are compile-time constants (black sky, full alpha).
    // UpdateClearColor in GameLoop uses them; the compiler folds the / 255f
    // divisions to 0f / 0f / 0f / 1f at JIT time.
    public const int clearcolorR = 0;
    public const int clearcolorG = 0;
    public const int clearcolorB = 0;
    public const int clearcolorA = 255;

    // ── VSync / lag simulation ────────────────────────────────────────────────

    /// <summary>Applies the current VSync setting (disabled only when lag simulation is active).</summary>
    public void UseVsync() => gameService.SetVSync(EnableLog != 1);

    /// <summary>Cycles through lag-simulation modes (0 = off, 1 = no vsync, 2 = spin-wait).</summary>
    public void ToggleVsync()
    {
        EnableLog = (EnableLog + 1) % 3;
        UseVsync();
    }

    // ── GUI state ─────────────────────────────────────────────────────────────

    /// <summary>
    /// When <see langword="true"/>, the next <see cref="OnResize"/> call will
    /// send the current resolution to the server.
    /// Set to <see langword="true"/> in <c>ProcessServerIdentification</c> once
    /// the connection is established.
    /// </summary>
    private bool sendResize;

    /// <summary>Returns to in-game GUI state and releases the free mouse.</summary>
    public void GuiStateBackToGame()
    {
        GuiState = GameState.Normal;
        SetFreeMouse(false);
    }

    /// <summary>Opens the escape menu and releases the mouse pointer lock.</summary>
    public void EscapeMenuStart()
    {
        MenuState = new MenuState();
        EscapeMenuRestart = true;
        gameService.ExitMousePointerLock();
    }

    /// <summary>Shows the escape menu in free-mouse mode.</summary>
    public void ShowEscapeMenu()
    {
        MenuState = new MenuState();
        SetFreeMouse(true);
    }

    /// <summary>Opens the inventory screen in free-mouse mode.</summary>
    public void ShowInventory()
    {
        GuiState = GameState.Inventory;
        MenuState = new MenuState();
        SetFreeMouse(true);
    }

    // ── Text measurement ──────────────────────────────────────────────────────

    /// <summary>Returns the rendered width of <paramref name="s"/> at the given point size.</summary>
    public int TextSizeWidth(string s, int size)
    {
        var (width, _) = GameTypeface.Measure(s, size);
        return width;
    }

    /// <summary>Returns the rendered height of <paramref name="s"/> at the given point size.</summary>
    public int TextSizeHeight(string s, int size)
    {
        var (_, height) = GameTypeface.Measure(s, size);
        return height;
    }

    // ── Block picking ─────────────────────────────────────────────────────────

    /// <summary>Returns the nearest <see cref="BlockPosSide"/> to <paramref name="target"/>.</summary>
    public BlockPosSide Nearest(ArraySegment<BlockPosSide> pick2, int pick2Count, Vector3 target)
    {
        float minDist = float.MaxValue;
        BlockPosSide nearest = null;
        for (int i = 0; i < pick2Count; i++)
        {
            float dist = Vector3.Distance(pick2[i].BlockPos, target);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = pick2[i];
            }
        }

        return nearest;
    }

    /// <summary>
    /// Performs a ray–block intersection for <paramref name="line"/>, returning
    /// the hit blocks sorted by distance from the ray origin.
    /// </summary>
    public ArraySegment<BlockPosSide> Pick(BlockOctreeSearcher s_, Line3D line, out int retCount)
    {
        int minX = Math.Max((int)Math.Min(line.Start[0], line.End[0]), 0);
        int minY = Math.Max((int)Math.Min(line.Start[1], line.End[1]), 0);
        int minZ = Math.Max((int)Math.Min(line.Start[2], line.End[2]), 0);

        int maxX = Math.Min((int)Math.Max(line.Start[0], line.End[0]), _voxelMap.MapSizeX);
        int maxY = Math.Min((int)Math.Max(line.Start[1], line.End[1]), _voxelMap.MapSizeZ);
        int maxZ = Math.Min((int)Math.Max(line.Start[2], line.End[2]), _voxelMap.MapSizeY);

        int size = (int)BitOperations.RoundUpToPowerOf2(
            (uint)Math.Max(maxX - minX + 1, Math.Max(maxY - minY + 1, maxZ - minZ + 1)));

        s_.StartBox = new Box3(
            new Vector3(minX, minY, minZ),
            new Vector3(minX + size, minY + size, minZ + size));

        ArraySegment<BlockPosSide> pick2 = s_.LineIntersection(
            IsTileEmptyForPhysics, Getblockheight, line, out retCount);

        PickSort(pick2, retCount, line.Start);
        return pick2;
    }

    /// <summary>
    /// Sorts <paramref name="pick"/> by ascending distance from <paramref name="start"/>
    /// using bubble sort. Suitable for the small result sets (typically &lt;10) produced
    /// by block picking; replace with <c>Span.Sort</c> if larger sets arise.
    /// </summary>
    private void PickSort(ArraySegment<BlockPosSide> pick, int pickCount, Vector3 start)
    {
        bool changed;
        do
        {
            changed = false;
            for (int i = 0; i < pickCount - 1; i++)
            {
                if (Vector3.Distance(pick[i].BlockPos, start) >
                    Vector3.Distance(pick[i + 1].BlockPos, start))
                {
                    (pick[i], pick[i + 1]) = (pick[i + 1], pick[i]);
                    changed = true;
                }
            }
        }
        while (changed);
    }

    // ── Dispose ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Disposes all mods and releases all cached GPU texture handles.
    /// </summary>
    public void Dispose()
    {
        for (int i = 0; i < ClientMods.Count; i++)
        {
            ClientMods[i]?.Dispose();
        }

        foreach (int id in textures.Values)
        {
            openGlService.GLDeleteTexture(id);
        }

        foreach (CachedTexture ct in CachedTextTextures.Values)
        {
            openGlService.GLDeleteTexture(ct.TextureId);
        }
    }

    // ── Stubs (candidates for removal) ───────────────────────────────────────

    /// <remarks>
    /// Cito stub — always returns <see langword="true"/>.
    /// All call sites can be replaced with a literal <c>true</c> and this method removed.
    /// </remarks>
    internal static bool EnablePlayerUpdatePosition(int kKey) => true;


    void IGame.SendChat(string message) => SendChat(message);
}