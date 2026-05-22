using OpenTK.Mathematics;

public partial class Game
{
    // ── Fixed-timestep constants ──────────────────────────────────────────────

    private const int FixedTickRate = 75;
    private const float FixedTickDt = 1f / FixedTickRate;
    private const float MouseSmoothingDt = 1f / 75f;
    private const int EnableLogSimulateLag = 2;
    private const int MaxTicksPerFrame = 3;
    private const int MaxCommitsPerFrame = 32;

    // ── Clear colour cache ────────────────────────────────────────────────────

    private float _clearColorRf, _clearColorGf, _clearColorBf, _clearColorAf;
    private int _lastClearColorR = -1, _lastClearColorG = -1,
                _lastClearColorB = -1, _lastClearColorA = -1;

    // ── Entry point ───────────────────────────────────────────────────────────

    /// <summary>
    /// Top-level per-frame entry point called by <c>Window.RenderFrame</c>.
    /// Drives fixed update, drains background commits, then renders.
    /// No scheduler, no mod indirection — just a straight call sequence.
    /// </summary>
    public void OnRenderFrame(float dt)
    {
        UpdateResize();
        UpdateClearColor();
        UpdateMouseSmoothing(dt);
        TryInitialiseConnection();

        gameService.ApplicationDoEvents();

        // Fixed timestep — capped at 1 s to prevent spiral-of-death.
        accumulator = Math.Min(accumulator + dt, 1f);
        int ticks = MaxTicksPerFrame;
        while (accumulator >= FixedTickDt && ticks-- > 0)
        {
            FixedUpdate(FixedTickDt);
            accumulator -= FixedTickDt;
        }

        // Per-frame mod hook (variable dt).
        for (int i = 0; i < ClientMods.Count; i++)
        {
            ClientMods[i]?.OnFrame(dt);
        }

        if (GuiState == GameState.MapLoading)
        {
            return;
        }

        if (EnableLog == EnableLogSimulateLag)
            Thread.SpinWait(20_000_000);

        Render3d(dt);
        Render2d(dt);
    }

    // ── Fixed update ──────────────────────────────────────────────────────────

    /// <summary>
    /// One fixed timestep tick — mod logic, entity scripts, physics, audio.
    /// </summary>
    private void FixedUpdate(float dt)
    {
        for (int i = 0; i < ClientMods.Count; i++)
            ClientMods[i]?.OnUpdate(dt);

        for (int i = 0; i < Entities.Count; i++)
        {
            Entity e = Entities[i];
            if (e == null) continue;
            for (int k = 0; k < e.Scripts.Count; k++)
                e.Scripts[k].OnNewFrameFixed(i, dt);
        }

        RevertSpeculative(dt);

        if (GuiState == GameState.MapLoading)
            return;

        Vector3 vel;
        vel.X = (Player.Position.X - lastplayerpositionX) * FixedTickRate;
        vel.Y = (Player.Position.Y - lastplayerpositionY) * FixedTickRate;
        vel.Z = (Player.Position.Z - lastplayerpositionZ) * FixedTickRate;
        playervelocity = vel;

        lastplayerpositionX = Player.Position.X;
        lastplayerpositionY = Player.Position.Y;
        lastplayerpositionZ = Player.Position.Z;
    }

    // ── Render passes ─────────────────────────────────────────────────────────

    private void Render3d(float dt)
    {
        SetAmbientLight(Terraincolor());
        openGlService.GlClearColorBufferAndDepthBuffer();
        openGlService.BindTexture2d(TerrainTexture);

        for (int i = 0; i < ClientMods.Count; i++)
            ClientMods[i]?.OnBeforeRender3d(dt);

        meshDrawer.GLMatrixModeModelView();
        meshDrawer.GLLoadMatrix(Camera);
        CameraMatrix.LastModelViewMatrix = Camera;
        FrustumCulling.CalcFrustumEquations();

        openGlService.GlEnableDepthTest();

        for (int i = 0; i < ClientMods.Count; i++)
            ClientMods[i]?.OnRender3d(dt);
    }

    private void Render2d(float dt)
    {
        SetAmbientLight(ColorUtils.ColorFromArgb(255, 255, 255, 255));
        Draw2d(dt);

        //for (int i = 0; i < ClientMods.Count; i++)
        //    ClientMods[i]?.OnRender2d(dt);

        MouseLeftClick = false;
        mouserightclick = false;
        mouseleftdeclick = false;
    }

    // ── Connection init ───────────────────────────────────────────────────────

    private void TryInitialiseConnection()
    {
        if (StartedConnecting)
        {
            return;
        }

        StartedConnecting = true;
        Connect();
    }

    // ── Resize / viewport ─────────────────────────────────────────────────────

    private void UpdateResize()
    {
        int w = gameService.CanvasWidth;
        int h = gameService.CanvasHeight;
        if (lastWidth == w && lastHeight == h) return;
        lastWidth = w;
        lastHeight = h;
        OnResize();
    }

    private void OnResize()
    {
        openGlService.GlViewport(0, 0, gameService.CanvasWidth, gameService.CanvasHeight);
        Set3dProjection2();
        if (sendResize)
        {
            SendGameResolution();
        }
    }

    // ── Per-frame helpers ─────────────────────────────────────────────────────

    private void UpdateClearColor()
    {
        if (GuiState == GameState.MapLoading)
        {
            openGlService.GlClearColorRgbaf(0f, 0f, 0f, 1f);
            return;
        }

        if (clearcolorR != _lastClearColorR || clearcolorG != _lastClearColorG
         || clearcolorB != _lastClearColorB || clearcolorA != _lastClearColorA)
        {
            _clearColorRf = clearcolorR / 255f;
            _clearColorGf = clearcolorG / 255f;
            _clearColorBf = clearcolorB / 255f;
            _clearColorAf = clearcolorA / 255f;
            _lastClearColorR = clearcolorR;
            _lastClearColorG = clearcolorG;
            _lastClearColorB = clearcolorB;
            _lastClearColorA = clearcolorA;
        }

        openGlService.GlClearColorRgbaf(_clearColorRf, _clearColorGf, _clearColorBf, _clearColorAf);
    }

    private void UpdateMouseSmoothing(float dt)
    {
        mouseSmoothingAccum += dt;
        while (mouseSmoothingAccum > MouseSmoothingDt)
        {
            mouseSmoothingAccum -= MouseSmoothingDt;
            UpdateMouseViewportControl(MouseSmoothingDt);
        }
    }
}