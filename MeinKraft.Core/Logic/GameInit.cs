using MeinKraft;
using MessagePipe;
using OpenTK.Mathematics;

/// <summary>
/// Partial class containing field declarations and constructor initialization
/// for the core Game state. Fields owned by other subsystem partials are
/// initialized via their own partial-class initializer methods called from here.
/// </summary>
public partial class Game : IGame
{
    // -------------------------------------------------------------------------
    // Platform & core subsystems
    // -------------------------------------------------------------------------

    private readonly IGameWindowService gameService;
    private readonly IOpenGlService openGlService;
    private readonly ILightManager _lightManager;
    private readonly IModRegistry modRegistry;
    private readonly IGameLogger _gameLogger;
    private readonly IBlockChangeNotifier _blockChangeNotifier;

    public LanguageService Language { get; set; }
    public Config3d Config3d { get; set; }
    public GameOption options { get; set; }
    public ServerInformation ServerInfo { get; set; }
    private Dictionary<string, string> performanceinfo;

    // -------------------------------------------------------------------------
    // Rendering / textures
    // -------------------------------------------------------------------------
    
    public Dictionary<int, int> TextureIdForInventory { get; set; }

    public int TerrainTexture { get; set; }
    

    /// <summary>Maximum texture size detected at runtime.</summary>
    internal int maxTextureSize;
    internal int Atlas1dheight() => maxTextureSize;

    public int handTexture { get; set; }
    public bool HandRedraw { get; set; }

    private int whitetexture;
    public Dictionary<TextStyle, CachedTexture> CachedTextTextures { get; set; } = [];
    internal Dictionary<string, int> textures;
    internal List<string> AllowedFonts;

    public int ActiveMaterial { get; set; }
    public int Font { get; set; }

    public bool ENABLE_DRAW2D { get; set; }
    public bool EnableDrawTestCharacter { get; set; }
    public bool EnableDrawPosition { get; set; }
    public int EnableLog { get; set; }

    // -------------------------------------------------------------------------
    // World / map
    // -------------------------------------------------------------------------

    private readonly IVoxelMap _voxelMap;

    public int LastplacedblockX { get; set; }
    public int LastplacedblockY { get; set; }
    public int LastplacedblockZ { get; set; }

    public int FillAreaLimit { get; set; }
    public int ReceivedMapLength { get; set; }
    public bool ShouldRedrawAllBlocks { get; set; }
    public MapLoadingProgressEventArgs maploadingprogress { get; set; }
    public TextFont FontMapLoading { get; set; }

    // -------------------------------------------------------------------------
    // Player — identity & position
    // -------------------------------------------------------------------------

    public Entity Player { get; set; }
    public int LocalPlayerId { get; set; }

    public float LocalPositionX { get => Player.Position.X; set => Player.Position.X = value; }
    public float LocalPositionY { get => Player.Position.Y; set => Player.Position.Y = value; }
    public float LocalPositionZ { get => Player.Position.Z; set => Player.Position.Z = value; }

    public float LocalOrientationX { get => Player.Position.RotX; set => Player.Position.RotX = value; }
    public float LocalOrientationY { get => Player.Position.RotY; set => Player.Position.RotY = value; }
    public float LocalOrientationZ { get => Player.Position.RotZ; set => Player.Position.RotZ = value; }

    public float PlayerPositionSpawnX { get; set; }
    public float PlayerPositionSpawnY { get; set; }
    public float PlayerPositionSpawnZ { get; set; }

    public Vector3 PlayerDestination { get; set; }
    public Vector3 playervelocity { get; set; }

    private float lastplayerpositionX;
    private float lastplayerpositionY;
    private float lastplayerpositionZ;

    // -------------------------------------------------------------------------
    // Player — state & stats
    // -------------------------------------------------------------------------

    public bool Spawned { get; set; }
    public bool IsPlayerOnGround { get; set; }
    public bool IsShiftPressed { get; set; }
    public byte LocalStance { get; set; }
    public GameState GuiState { get; set; }
    public AnimationHint LocalPlayerAnimationHint { get; set; }

    public Packet_ServerPlayerStats PlayerStats { get; set; }
    public Dictionary<(int x, int y, int z), float> BlockHealth { get; set; } = new();

    public int CurrentlyAttackedEntity { get; set; }
    public Vector3i? CurrentAttackedBlock { get; set; }
    public int SelectedEntityId { get; set; }
    public int SelectedBlockPositionX { get; set; }
    public int SelectedBlockPositionY { get; set; }
    public int SelectedBlockPositionZ { get; set; }

    public int[] TotalAmmo { get; set; }
    public int[] LoadedAmmo { get; set; }
    public bool AmmoStarted { get; set; }
    public bool AudioEnabled { get; set; }
    public bool AutoJumpEnabled { get; set; }
    public bool IronSights { get; set; }
    public bool DrawBlockInfo { get; set; }

    public Dictionary<int, VisibleDialog> Dialogs { get; set; }
    public List<string> TypingLog { get; set; }
    public int TypingLogPos { get; set; }
    public TypingState GuiTyping { get; set; }

    private int playertexturedefault;
    public const string playertexturedefaultfilename = "mineplayer.png";

    private readonly int lastOxygenTickMilliseconds;
    public int LastReceivedMilliseconds { get; set; }
    public int ReloadBlock { get; set; }
    public int ReloadStartMilliseconds { get; set; }
    public int CurrentTimeMilliseconds { get; set; }
    public int TotalTimeMilliseconds { get; set; }
    public int LastPositionSentMilliseconds { get; set; }

    // -------------------------------------------------------------------------
    // Player — movement & physics
    // -------------------------------------------------------------------------

    public bool EnableMove { get; set; }
    public bool StopPlayerMove { get; set; }
    public float MoveSpeed { get; set; }
    public float Basemovespeed { get; set; }
    public float PICK_DISTANCE { get; set; }
    public float MovedZ { get; set; }
    public bool ReachedWall { get; set; }
    public bool ReachedWall1BlockHigh { get; set; }
    public bool ReachedHalfBlock { get; set; }
    public bool AllowFreeMove { get; set; }

    public float PushX { get; set; }
    public float PushY { get; set; }
    public float PushZ { get; set; }

    public float WallDistance { get; set; }
    internal float constRotationSpeed;
    private float RadiusWhenMoving;
    private float rotationspeed;
    private int PlayerPushDistance;

    public int grenadetime { get; set; }
    public int grenadecookingstartMilliseconds { get; set; }
    public int pistolcycle { get; set; }
    public int lastironsightschangeMilliseconds { get; set; }
    public bool handSetAttackBuild { get; set; }    // already declared above — kept for region clarity
    public bool handSetAttackDestroy { get; set; }  // already declared above — kept for region clarity

    // -------------------------------------------------------------------------
    // Camera
    // -------------------------------------------------------------------------

    public Matrix4 Camera { get; set; }
    public Vector3 CameraEye { get; set; }

    private CameraMatrixProvider CameraMatrix;
    private float fov;
    public CameraType CameraType { get; set; }
    public bool EnableTppView { get; set; }
    public float TppCameraDistance { get; set; }
    public float OverHeadCameraDistance { get; set; }
    private readonly ICameraService OverheadCameraK;
    public bool OverheadCamera { get; set; }
    private bool enableCameraControl;
    private float znear;
    private bool ENABLE_ZFAR;
    private float TPP_CAMERA_DISTANCE_MIN;
    private float TPP_CAMERA_DISTANCE_MAX;
    private int maxdrawdistance;

    // -------------------------------------------------------------------------
    // Input
    // -------------------------------------------------------------------------

    public Controls Controls { get; set; }
    internal bool mouseSmoothing;

    public bool MouseLeftClick { get; set; }
    public bool mouseleftdeclick { get; set; }
    private bool wasmouseleft;
    public bool mouserightclick { get; set; }


    public bool[] KeyboardState { get; set; }
    public bool[] KeyboardStateRaw { get; set; }

    public bool mouseLeft { get; set; }
    public bool mouseMiddle { get; set; }
    public bool mouseRight { get; set; }

    public int MouseCurrentX { get; set; }
    public int MouseCurrentY { get; set; }
    private float mouseDeltaX;
    private float mouseDeltaY;
    private float mouseSmoothingVelX;
    private float mouseSmoothingVelY;
    private float mouseSmoothingAccum;
    private bool mousePointerLockShouldBe;

    public float TouchMoveDx { get; set; }
    public float TouchMoveDy { get; set; }
    public float TouchOrientationDx { get; set; }
    public float TouchOrientationDy { get; set; }

    // -------------------------------------------------------------------------
    // Audio
    // -------------------------------------------------------------------------

    private readonly IAudioService audioService;
    public bool soundnow { get; set; }

    // -------------------------------------------------------------------------
    // Networking / server
    // -------------------------------------------------------------------------

    public ClientPacketHandler[] PacketHandlers { get; set; }
    public NetClient NetClient { get; set; }
    public string ServerGameVersion { get; set; }
    public bool IsSinglePlayer { get; set; }
    public string Follow { get; set; }

    public ConnectionData ConnectData { get; set; }
    public bool IsReconnecting { get; set; }
    public bool IsExitingToMainMenu { get; set; }
    public bool StartedConnecting { get; set; }
    public string BlobDownloadName { get; set; }
    public string BlobDownloadMd5 { get; set; }
    public MemoryStream BlobDownload { get; set; }

    public string InvalidVersionDrawMessage { get; set; }
    public Packet_Server InvalidVersionPacketIdentification { get; set; }

    // -------------------------------------------------------------------------
    // Speculative block placement
    // -------------------------------------------------------------------------

    private Speculative?[] speculative;
    private int speculativeCount;

    // -------------------------------------------------------------------------
    // Subsystems
    // -------------------------------------------------------------------------

    private readonly IFrustumCulling FrustumCulling;

    public List<Entity> Entities { get; set; }
    private IReadOnlyList<IModBase> ClientMods => modRegistry.Mods;
    private readonly ITerrainChunkTesselator TerrainChunkTesselator;
    private readonly IMeshDrawer meshDrawer;
    public InventoryUtilClient InventoryUtil { get; set; }
    private readonly IBlockRegistry _blockRegistry;
    private readonly IAssetManager _assetManager;
    private readonly IPublisher<SetupProgressEventArgs> _publisher;
    public Packet_Inventory Inventory { get; set; }

    // -------------------------------------------------------------------------
    // UI / menus
    // -------------------------------------------------------------------------

    public MenuState MenuState { get; set; }
    public bool EscapeMenuRestart { get; set; }

    private int ChatLinesMax;
    public List<Chatline> ChatLines { get; set; }

    private float accumulator;
    private int lastWidth;
    private int lastHeight;
    private Random rnd;
    private string[] getAsset;

    // -------------------------------------------------------------------------
    // IGameClient — computed / forwarded members
    // -------------------------------------------------------------------------

    public bool EnableCameraControl { set => enableCameraControl = value; }
    public Dictionary<string, string> PerformanceInfo => performanceinfo;

    public float LocalEyeHeight
        => Entities[LocalPlayerId].DrawModel.EyeHeight;

    public FreeMoveLevel FreemoveLevel
    {
        get
        {
            if (!Controls.FreeMove)
            {
                return FreeMoveLevel.None;
            }

            return Controls.NoClip ? FreeMoveLevel.Noclip : FreeMoveLevel.Freemove;
        }
        set
        {
            Controls.FreeMove = value != FreeMoveLevel.None;
            Controls.NoClip = value == FreeMoveLevel.Noclip;
        }
    }

    public float FovOffset { get; set; }

    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    public Game(IGameWindowService platform, IOpenGlService platformOpenGl, ITerrainChunkTesselator terrainChunkTesselator,
        IModRegistry modRegistry, IVoxelMap voxelMap, IAudioService audioService, ICameraService cameraService, IFrustumCulling frustumCulling,
        IMeshDrawer meshDrawer, IBlockRegistry blockTypeRegistry, IAssetManager assetManager, IGameLogger gameLogger, ILightManager lightManager,
       IBlockChangeNotifier blockChangeNotifier, IPublisher<SetupProgressEventArgs> publisher)
    {
        gameService = platform;
        openGlService = platformOpenGl;
        _gameLogger = gameLogger;
        _lightManager = lightManager;
        _blockRegistry = blockTypeRegistry;
        this.modRegistry = modRegistry;
        this._voxelMap = voxelMap;
        OverheadCameraK = cameraService;
        FrustumCulling = frustumCulling;
        _assetManager = assetManager;
        this.meshDrawer = meshDrawer;
        TerrainChunkTesselator = terrainChunkTesselator;
        _blockChangeNotifier = blockChangeNotifier;
        _publisher = publisher;
        InitCore();
        InitMap();
        InitTextures();
        InitPlayer();
        InitCamera();
        InitInput();
        InitOptions();
        InitNetworking();
        InitChat();
        this.audioService = audioService;
    }

    // -------------------------------------------------------------------------
    // Initialisation helpers
    // -------------------------------------------------------------------------

    private void InitCore()
    {
        performanceinfo = [];
        Language = new LanguageService();
        ServerInfo = new ServerInformation();
        options = new GameOption();
        getAsset = new string[1024 * 2];
        PlayerStats = new Packet_ServerPlayerStats();
        Entities = [];
    }

    private void InitMap()
    {
        LastplacedblockX = -1;
        LastplacedblockY = -1;
        LastplacedblockZ = -1;
        FillAreaLimit = 200;
    }

    private void InitTextures()
    {
        TextureIdForInventory = [];
        handTexture = -1;
        whitetexture = -1;
        textures = [];
        CachedTextTextures = [];
        AllowedFonts = ["Verdana"];
    }

    private void InitPlayer()
    {
        Player = new Entity { Position = new EntityPosition() };
        LocalPlayerId = -1;
        CurrentlyAttackedEntity = -1;
        playertexturedefault = -1;

        PlayerPositionSpawnX = 15.5f;
        PlayerPositionSpawnY = 64;
        PlayerPositionSpawnZ = 15.5f;

        playervelocity = new Vector3();
        MovedZ = 0;
        WallDistance = 0.3f;
        constRotationSpeed = 180 / 20;
        RadiusWhenMoving = 3f / 10;
        Basemovespeed = 5;
        MoveSpeed = 5;
        rotationspeed = 15f / 100;
        PICK_DISTANCE = 4.1f;
        grenadetime = 3;
        PlayerPushDistance = 2;

        LocalPlayerAnimationHint = new AnimationHint();
        EnableMove = true;
        AudioEnabled = true;
        AutoJumpEnabled = false;

        TotalAmmo = new int[GameConstants.MAX_BLOCKTYPES];
        LoadedAmmo = new int[GameConstants.MAX_BLOCKTYPES];
        BlockHealth = [];
        Dialogs = [];
        TypingLog = [];
    }

    private void InitCamera()
    {
        Camera = Matrix4.Identity;
        CameraEye = Vector3.Zero;
        CameraMatrix = new CameraMatrixProvider();
        fov = MathF.PI / 3;
        CameraType = CameraType.Fpp;
        EnableTppView = false;
        znear = 1f / 10;
        ENABLE_ZFAR = true;
        OverHeadCameraDistance = 10;
        TppCameraDistance = 3;
        TPP_CAMERA_DISTANCE_MIN = 1;
        TPP_CAMERA_DISTANCE_MAX = 10;
        enableCameraControl = true;
    }

    private void InitInput()
    {
        Controls = new Controls();
        mouseSmoothing = true;
        MouseLeftClick = false;
        mouseleftdeclick = false;
        wasmouseleft = false;
        mouserightclick = false;

        const int KeysMax = 360;
        KeyboardState = new bool[KeysMax];
        KeyboardStateRaw = new bool[KeysMax];
    }

    private void InitOptions()
    {
        ENABLE_DRAW2D = true;
        EnableLog = 0;
        AllowFreeMove = true;
        MenuState = new MenuState();
    }

    private void InitNetworking()
    {
        PacketHandlers = new ClientPacketHandler[256];
        speculativeCount = 0;
        speculative = new Speculative?[GameConstants.speculativeMax];
    }

    private void InitChat()
    {
        ChatLinesMax = 1;
        ChatLines = [];
    }
}