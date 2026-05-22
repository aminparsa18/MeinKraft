using MeinKraft;

public partial class Game
{
    // ── Startup constants ─────────────────────────────────────────────────────

    /// <summary>Default view distance in blocks on fast (desktop) hardware.</summary>
    private const int ViewDistanceFast = 1024;

    /// <summary>Default view distance in blocks on slow (mobile/web) hardware.</summary>
    private const int ViewDistanceSlow = 32;

    /// <summary>Default map dimensions before the server sends its own size.</summary>
    private const int DefaultMapSizeX = 256;
    private const int DefaultMapSizeY = 256;
    private const int DefaultMapSizeZ = 128;

    // ── Entry point ───────────────────────────────────────────────────────────

    public void Start()
    {
        InitSubsystems();
        InitRenderer();
    }

    // ── Start helpers ─────────────────────────────────────────────────────────

    private void InitSubsystems()
    {
        _publisher.Publish(new() { Title = "Initialising SubSystems...", Progress = 0 });

        // ── Text / language ───────────────────────────────────────────────────
        Language.LoadTranslations();

        // ── Core data / config ────────────────────────────────────────────────
        Config3d config3d = new()
        {
            ViewDistance = gameService.IsFastSystem() ? ViewDistanceFast : ViewDistanceSlow
        };
        Config3d = config3d;

        // ── World / map ───────────────────────────────────────────────────────
        _voxelMap.Reset(DefaultMapSizeX, DefaultMapSizeY, DefaultMapSizeZ);
        _voxelMap.Heightmap = new ChunkedMap2d<int>(_voxelMap.MapSizeX, _voxelMap.MapSizeY);

        // ── Inventory ─────────────────────────────────────────────────────────
        Packet_Inventory inventory = new() { RightHand = new InventoryItem[10] };
        InventoryService dataItems = new(this, _blockRegistry);
        Inventory = inventory;
        InventoryUtil = new InventoryUtilClient(inventory, dataItems);

        // ── Misc ──────────────────────────────────────────────────────────────
        rnd = new Random();
        gameService.AddOnCrash(OnCrashHandlerLeave.Create(this));

        // Prevent the loading screen from immediately showing the lag symbol.
        LastReceivedMilliseconds = gameService.TimeMillisecondsFromStart;

        int detectedSize = openGlService.GlGetMaxTextureSize();
        maxTextureSize = Math.Max(detectedSize, 1024);

       // MapLoadingStart();
    }

    private void InitRenderer()
    {
        _publisher.Publish(new() { Title = "Initialising Renderer...", Progress = 0 });

        openGlService.GlClearColorRgbaf(0, 0, 0, 1);

        if (Config3d.EnableBackfaceCulling)
        {
            openGlService.GlDepthMask(true);
            openGlService.GlEnableDepthTest();
            openGlService.GlCullFaceBack();
            openGlService.GlEnableCullFace();
        }
    }
}