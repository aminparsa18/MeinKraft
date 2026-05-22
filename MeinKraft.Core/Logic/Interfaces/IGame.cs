using MeinKraft;
using OpenTK.Mathematics;

/// <summary>
/// The primary contract between <see cref="Game"/> and every mod, screen, and
/// subsystem that needs to interact with game state. Covers rendering, player
/// state, world access, networking, audio, UI, and platform services.
/// Implemented by <see cref="Game"/>.
/// </summary>
public interface IGame : IDisposable
{
    // =========================================================================
    // Platform & core
    // =========================================================================

    /// <summary>Active localisation data.</summary>
    LanguageService Language { get; set; }

    /// <summary>3-D rendering configuration (culling, mipmaps, view distance).</summary>
    Config3d Config3d { get; set; }

    /// <summary>User-facing game options (graphics, sound, controls).</summary>
    GameOption options { get; set; }

    /// <summary>Live performance counters (FPS, chunk updates, triangles, etc.).</summary>
    Dictionary<string, string> PerformanceInfo { get; }

    // =========================================================================
    // Frame / update loop
    // =========================================================================

    void OnRenderFrame(float deltaTime);

    // =========================================================================
    // Rendering — 2-D
    // =========================================================================

    /// <summary>When <c>false</c>, the 2-D HUD layer is suppressed.</summary>
    bool ENABLE_DRAW2D { get; set; }

    /// <summary>When <c>false</c>, the 2-D HUD is hidden (interface alias).</summary>
    bool EnableDraw2d { get; set; }

    /// <summary>Logging verbosity level (0 = off).</summary>
    int EnableLog { get; set; }

    /// <summary>Whether to render the debug test character.</summary>
    bool EnableDrawTestCharacter { get; set; }

    /// <summary>Whether to render the debug position overlay.</summary>
    bool EnableDrawPosition { get; set; }

    /// <summary>Returns the OpenGL ID of the engine's built-in 1×1 white texture.</summary>
    int GetOrCreateWhiteTexture();

    /// <summary>Draws a textured rectangle in screen space.</summary>
    void Draw2dTexture(int textureid, float x, float y, float width, float height,
                       int? inAtlasId, int atlasIndex, int color, bool blend);

    /// <summary>Draws a portion of a texture into a screen-space rectangle.</summary>
    void Draw2dTexturePart(int textureid, float srcwidth, float srcheight,
                           float dstx, float dsty, float dstwidth, float dstheight,
                           int color, bool enabledepthtest);

    /// <summary>Batches multiple textured quads in a single draw call.</summary>
    void Draw2dTextures(Draw2dData[] todraw, int todrawLength, int textureId);

    /// <summary>Draws a named bitmap asset at the given screen position.</summary>
    void Draw2dBitmapFile(string filename, float x, float y, float w, float h);

    /// <summary>Renders a string using the given font at the given screen position.</summary>
    void Draw2dText(string text, TextFont font, float x, float y, int? color, bool shadow);

    /// <summary>Renders a string using a point-size font.</summary>
    void Draw2dText1(string text, int x, int y, int fontsize, int? color, bool enabledepthtest);

    /// <summary>Returns the pixel width of <paramref name="s"/> rendered at <paramref name="size"/> pt.</summary>
    int TextSizeWidth(string s, int size);

    /// <summary>Returns the pixel height of <paramref name="s"/> rendered at <paramref name="size"/> pt.</summary>
    int TextSizeHeight(string s, int size);

    /// <summary>Returns the first font name from <paramref name="family"/> that is available.</summary>
    string ValidFont(string family);

    /// <summary>Active font handle used for general UI text.</summary>
    int Font { get; set; }

    /// <summary>Per-style cache of rasterised text textures.</summary>
    Dictionary<TextStyle, CachedTexture> CachedTextTextures { get; set; }

    // =========================================================================
    // Rendering — 3-D / GL matrix stack
    // =========================================================================

    /// <summary>The resolved camera matrix for the current frame.</summary>
    Matrix4 Camera { get; set; }

    /// <summary>World-space position of the camera eye point.</summary>
    Vector3 CameraEye { get; set; }

    /// <summary>Sets the perspective projection with the given far-clip and field-of-view.</summary>
    void Set3dProjection(float zfar, float fov);

    /// <summary>Returns the current far-clip distance.</summary>
    float Zfar();

    /// <summary>Returns the current field of view (radians).</summary>
    float CurrentFov();

    /// <summary>Additive FOV offset in radians applied on top of the base FOV. Used for sprint zoom.</summary>
    float FovOffset { get; set; }

    float MoveSpeedNow();

    void SetFog();
    void ToggleFog();
    void ToggleVsync();
    void UseVsync();

    /// <summary>Returns a UI scale factor appropriate for the current canvas size.</summary>
    float Scale();

    /// <summary>Returns the horizontal canvas centre minus half of <paramref name="width"/>.</summary>
    int Xcenter(float width);

    /// <summary>Returns the vertical canvas centre minus half of <paramref name="height"/>.</summary>
    int Ycenter(float height);

    /// <summary>Draws a circle outline in 3-D space.</summary>
    void Circle3i(float x, float y, float radius);

    // =========================================================================
    // Rendering — textures
    // =========================================================================

    /// <summary>Per-block-type texture ID used in inventory rendering.</summary>
    Dictionary<int, int> TextureIdForInventory { get; set; }

    /// <summary>OpenGL ID of the composite terrain texture atlas.</summary>
    int TerrainTexture { get; set; }

    /// <summary>The hand/held-item texture ID.</summary>
    int handTexture { get; set; }

    /// <summary>Whether the hand model needs to be redrawn.</summary>
    bool HandRedraw { get; set; }

    /// <summary>Loads or returns a cached GPU texture by name.</summary>
    int GetTexture(string p);

    /// <summary>Returns the name of the texture asset for the given GPU texture ID.</summary>
    string GetTextureNameById(int id);

    /// <summary>Loads a texture by name, or uploads <paramref name="bmp"/> if not yet cached.</summary>
    int GetTextureOrLoad(string name, byte[] rgba, int width, int height);

    /// <summary>Releases the GPU texture registered under <paramref name="name"/>.</summary>
    bool DeleteTexture(string name);

    /// <summary>Returns the raw bytes of the named asset file.</summary>
    byte[] GetAssetFile(string p);

    /// <summary>Returns the byte length of the named asset file.</summary>
    int GetAssetFileLength(string p);

    /// <summary>Applies a new set of terrain textures received from the server.</summary>
    void UseTerrainTextures(string[] textureIds, int textureIdsCount);

    // =========================================================================
    // Rendering — terrain & mesh
    // =========================================================================

    /// <summary>When <c>true</c>, all chunk meshes are rebuilt next frame.</summary>
    bool ShouldRedrawAllBlocks { get; set; }

    /// <summary>Schedules a full rebuild of all visible chunk meshes.</summary>
    void RedrawAllBlocks();

    // =========================================================================
    // Rendering — camera
    // =========================================================================

    /// <summary>Current camera mode (first-person, third-person, overhead).</summary>
    CameraType CameraType { get; set; }

    /// <summary>Whether third-person view is enabled.</summary>
    bool EnableTppView { get; set; }

    /// <summary>Distance from the player in third-person mode.</summary>
    float TppCameraDistance { get; set; }

    /// <summary>Whether the overhead camera mode is active.</summary>
    bool OverheadCamera { get; set; }

    /// <summary>Returns the current aim spread radius.</summary>
    float CurrentAimRadius();

    /// <summary>Applies the active camera type setting.</summary>
    void SetCamera(CameraType type);

    /// <summary>Cycles to the next available camera mode.</summary>
    void CameraChange();

    // =========================================================================
    // World / map
    // =========================================================================

    /// <summary>X dimension of the voxel map in blocks.</summary>
    int MapSizeX { get; }

    /// <summary>Y dimension of the voxel map in blocks.</summary>
    int MapSizeY { get; }

    /// <summary>Z dimension of the voxel map in blocks.</summary>
    int MapSizeZ { get; }

    /// <summary>World X of the most recently placed block.</summary>
    int LastplacedblockX { get; set; }

    /// <summary>World Y of the most recently placed block.</summary>
    int LastplacedblockY { get; set; }

    /// <summary>World Z of the most recently placed block.</summary>
    int LastplacedblockZ { get; set; }

    /// <summary>Maximum fill-area operation size (blocks).</summary>
    int FillAreaLimit { get; set; }

    /// <summary>Number of map bytes received so far during initial download.</summary>
    int ReceivedMapLength { get; set; }

    /// <summary>Whether the map has finished loading.</summary>
    MapLoadingProgressEventArgs maploadingprogress { get; set; }

    /// <summary>Font used on the map-loading screen.</summary>
    TextFont FontMapLoading { get; set; }

    /// <summary>Sets a block type and propagates lighting/mesh updates.</summary>
    void PlaceBlockAndRedraw(int x, int y, int z, int type);

    /// <summary>Sets a block type without triggering full map updates.</summary>
    void PlaceBlock(int x, int y, int z, int tileType);

    /// <summary>Returns the surface height at the given column.</summary>
    int Blockheight(int x, int y, int z);

    /// <summary>Returns the fractional block height used for sloped surfaces.</summary>
    float Getblockheight(int x, int y, int z);

    /// <summary>Returns <c>true</c> if the block at the given position allows player movement through it.</summary>
    bool IsTileEmptyForPhysics(int x, int y, int z);

    /// <summary>Returns <c>true</c> if the block at the given position allows player movement (close-range variant).</summary>
    bool IsTileEmptyForPhysicsClose(int x, int y, int z);

    /// <summary>Fires the map-loaded event and transitions out of the loading state.</summary>
    void MapLoaded();

    /// <summary>Raises map-loading progress with the given percentage, bytes, and status message.</summary>
    void InvokeMapLoadingProgress(int progressPercent, int progressBytes, string status);

    // =========================================================================
    // Block registry
    // =========================================================================

    /// <summary>Returns the block type ID currently held in hand, or <c>null</c> if empty.</summary>
    int? BlockInHand();

    // =========================================================================
    // Player — identity & position
    // =========================================================================

    /// <summary>The local player entity.</summary>
    Entity Player { get; set; }

    /// <summary>The local player's entity ID.</summary>
    int LocalPlayerId { get; }

    /// <summary>Local player's world-space X position.</summary>
    float LocalPositionX { get; set; }

    /// <summary>Local player's world-space Y position.</summary>
    float LocalPositionY { get; set; }

    /// <summary>Local player's world-space Z position.</summary>
    float LocalPositionZ { get; set; }

    /// <summary>Local player's X orientation component.</summary>
    float LocalOrientationX { get; set; }

    /// <summary>Local player's Y orientation component.</summary>
    float LocalOrientationY { get; set; }

    /// <summary>Local player's Z orientation component.</summary>
    float LocalOrientationZ { get; set; }

    /// <summary>Eye height of the local player's draw model.</summary>
    float LocalEyeHeight { get; }

    /// <summary>World X of the block at the player's eye level.</summary>
    int PlayerEyesBlockX { get; }

    /// <summary>World Y of the block at the player's eye level.</summary>
    int PlayerEyesBlockY { get; }

    /// <summary>World Z of the block at the player's eye level.</summary>
    int PlayerEyesBlockZ { get; }

    /// <summary>World X of the player's spawn point.</summary>
    float PlayerPositionSpawnX { get; set; }

    /// <summary>World Y of the player's spawn point.</summary>
    float PlayerPositionSpawnY { get; set; }

    /// <summary>World Z of the player's spawn point.</summary>
    float PlayerPositionSpawnZ { get; set; }

    /// <summary>Navigation waypoint the player is moving toward.</summary>
    Vector3 PlayerDestination { get; set; }

    /// <summary>Current physics velocity of the player.</summary>
    Vector3 playervelocity { get; set; }

    // =========================================================================
    // Player — state & stats
    // =========================================================================

    /// <summary>Whether the player has spawned into the world.</summary>
    bool Spawned { get; set; }

    /// <summary>Whether the player is standing on solid ground.</summary>
    bool IsPlayerOnGround { get; set; }

    /// <summary>Whether Shift is currently held.</summary>
    bool IsShiftPressed { get; set; }

    /// <summary>Current player stance byte (standing, crouching, etc.).</summary>
    byte LocalStance { get; set; }

    /// <summary>Current GUI / game-phase state.</summary>
    GameState GuiState { get; set; }

    /// <summary>Hint data driving local player animations.</summary>
    AnimationHint LocalPlayerAnimationHint { get; set; }

    /// <summary>Live player statistics received from the server.</summary>
    Packet_ServerPlayerStats PlayerStats { get; set; }

    /// <summary>Per-block-position health remaining before the block breaks.</summary>
    Dictionary<(int x, int y, int z), float> BlockHealth { get; set; }

    /// <summary>Returns the health remaining for the block at the given world position.</summary>
    float GetCurrentBlockHealth(int x, int y, int z);

    /// <summary>The entity ID currently being attacked, or -1.</summary>
    int CurrentlyAttackedEntity { get; set; }

    /// <summary>The world position of the block currently being attacked, or <c>null</c>.</summary>
    Vector3i? CurrentAttackedBlock { get; set; }

    /// <summary>The entity ID currently selected by the cursor.</summary>
    int SelectedEntityId { get; set; }

    /// <summary>World X of the block currently highlighted by the cursor.</summary>
    int SelectedBlockPositionX { get; set; }

    /// <summary>World Y of the block currently highlighted by the cursor.</summary>
    int SelectedBlockPositionY { get; set; }

    /// <summary>World Z of the block currently highlighted by the cursor.</summary>
    int SelectedBlockPositionZ { get; set; }

    /// <summary>Whether the block-info overlay is shown.</summary>
    bool DrawBlockInfo { get; set; }

    // =========================================================================
    // Player — movement & physics
    // =========================================================================

    /// <summary>Whether player movement input is processed.</summary>
    bool EnableMove { get; set; }

    bool StopPlayerMove { get; set; }

    /// <summary>Current movement speed multiplier.</summary>
    float MoveSpeed { get; set; }

    /// <summary>Base movement speed before any multipliers.</summary>
    float Basemovespeed { get; set; }

    /// <summary>Maximum block-pick distance.</summary>
    float PICK_DISTANCE { get; set; }

    /// <summary>Vertical distance moved this frame.</summary>
    float MovedZ { get; set; }

    /// <summary>Whether the player is blocked by a wall.</summary>
    bool ReachedWall { get; set; }

    /// <summary>Whether the player is blocked by a wall exactly one block high (auto-jump candidate).</summary>
    bool ReachedWall1BlockHigh { get; set; }

    /// <summary>Whether the player is blocked by a half-height block.</summary>
    bool ReachedHalfBlock { get; set; }

    /// <summary>Whether server permits freemove/noclip.</summary>
    bool AllowFreeMove { get; set; }

    /// <summary>
    /// Gets or sets the current freemove level.
    /// See <see cref="FreemoveLevelEnum"/> for valid values.
    /// </summary>
    FreeMoveLevel FreemoveLevel { get; set; }

    /// <summary>Whether auto-jump is enabled.</summary>
    bool AutoJumpEnabled { get; set; }

    /// <summary>Impulse force applied to the player on X this frame.</summary>
    float PushX { get; set; }

    /// <summary>Impulse force applied to the player on Y this frame.</summary>
    float PushY { get; set; }

    /// <summary>Impulse force applied to the player on Z this frame.</summary>
    float PushZ { get; set; }

    /// <summary>Block type under the player's feet.</summary>
    int BlockUnderPlayer();

    /// <summary>Returns <c>true</c> if any player entity occupies the given block position.</summary>
    bool IsAnyPlayerInPos(int blockposX, int blockposY, int blockposZ);

    float WallDistance { get; set; }

    // =========================================================================
    // Player — combat & weapons
    // =========================================================================

    /// <summary>Whether the player is in iron-sights / ADS mode.</summary>
    bool IronSights { get; set; }

    /// <summary>Whether the player's hand is set to attack/build.</summary>
    bool handSetAttackBuild { get; set; }

    /// <summary>Whether the player's hand is set to attack/destroy.</summary>
    bool handSetAttackDestroy { get; set; }

    /// <summary>Per-block-type total ammo counts.</summary>
    int[] TotalAmmo { get; set; }

    /// <summary>Per-block-type currently loaded ammo counts.</summary>
    int[] LoadedAmmo { get; set; }

    /// <summary>Whether the ammo system has been initialised.</summary>
    bool AmmoStarted { get; set; }

    /// <summary>Grenade cook time in seconds.</summary>
    int grenadetime { get; set; }

    /// <summary>Millisecond timestamp when grenade cooking started.</summary>
    int grenadecookingstartMilliseconds { get; set; }

    /// <summary>Pistol fire-mode cycle index.</summary>
    int pistolcycle { get; set; }

    /// <summary>Millisecond timestamp of the last reload start.</summary>
    int ReloadStartMilliseconds { get; set; }

    /// <summary>Block type being reloaded, or 0.</summary>
    int ReloadBlock { get; set; }

    /// <summary>Millisecond timestamp of the last iron-sights toggle.</summary>
    int lastironsightschangeMilliseconds { get; set; }

    /// <summary>Returns the current weapon recoil spread radius.</summary>
    float CurrentRecoil();

    /// <summary>Applies damage to the local player.</summary>
    void ApplyDamageToPlayer(int damage, DeathReason reason, int sourceBlock);

    // =========================================================================
    // Player — inventory
    // =========================================================================

    /// <summary>The player's current inventory packet.</summary>
    Packet_Inventory Inventory { get; set; }

    /// <summary>Inventory utility helper.</summary>
    InventoryUtilClient InventoryUtil { get; set; }

    /// <summary>Currently selected material/block slot.</summary>
    int ActiveMaterial { get; set; }

    /// <summary>Returns the material in slot <paramref name="i"/>.</summary>
    int MaterialSlots(int i);

    /// <summary>Processes an inventory use action.</summary>
    void UseInventory(Packet_Inventory packet_Inventory);

    /// <summary>Handles a click on an inventory cell.</summary>
    void InventoryClick(Packet_InventoryPosition pos);

    /// <summary>Moves an item to the main inventory.</summary>
    void MoveToInventory(Packet_InventoryPosition from);

    /// <summary>Wears an item (equip from inventory to equipment slot).</summary>
    void WearItem(Packet_InventoryPosition from, Packet_InventoryPosition to);

    // =========================================================================
    // Player — swimming / environment
    // =========================================================================

    /// <summary>Returns <c>true</c> if the player's eyes are submerged in water.</summary>
    bool WaterSwimmingEyes();

    /// <summary>Returns <c>true</c> if the player's eyes are in any fluid.</summary>
    bool SwimmingEyes();

    bool SwimmingBody();

    // =========================================================================
    // Entities
    // =========================================================================

    /// <summary>All active entities in the scene.</summary>
    List<Entity> Entities { get; }

    /// <summary>Adds a locally simulated entity to the scene.</summary>
    void EntityAddLocal(Entity entity);

    /// <summary>
    /// Returns the entity ID being followed in spectator mode,
    /// or <c>null</c> if not spectating.
    /// </summary>
    int? FollowId();

    // =========================================================================
    // Block picking / raycasting
    // =========================================================================

    /// <summary>Casts <paramref name="line"/> through the octree and returns all hit block faces.</summary>
    ArraySegment<BlockPosSide> Pick(BlockOctreeSearcher s_, Line3D line, out int retCount);

    /// <summary>Returns the closest <see cref="BlockPosSide"/> to <paramref name="target"/>.</summary>
    BlockPosSide Nearest(ArraySegment<BlockPosSide> pick2, int pick2Count, Vector3 target);

    // =========================================================================
    // Input
    // =========================================================================

    /// <summary>Keyboard and movement control state.</summary>
    Controls Controls { get; set; }

    /// <summary>Per-key state array (processed, with repeat).</summary>
    bool[] KeyboardState { get; set; }

    /// <summary>Per-key raw state array (no repeat).</summary>
    bool[] KeyboardStateRaw { get; set; }

    /// <summary>Returns the integer key code for <paramref name="key"/>.</summary>
    int GetKey(OpenTK.Windowing.GraphicsLibraryFramework.Keys key);

    void KeyDown(KeyEventArgs eKey);

    void KeyUp(KeyEventArgs eKey);
    void KeyPress(KeyPressEventArgs eKeyChar);
    void OnTouchStart(TouchEventArgs e);
    void OnTouchMove(TouchEventArgs e);
    void OnTouchEnd(TouchEventArgs e);
    void MouseWheelChanged(float delta);
    void MouseDown(MouseEventArgs args);
    void MouseMove(MouseEventArgs e);
    void MouseUp(MouseEventArgs e);

    /// <summary>Left mouse button held this frame.</summary>
    bool mouseLeft { get; set; }

    /// <summary>Middle mouse button held this frame.</summary>
    bool mouseMiddle { get; set; }

    /// <summary>Right mouse button held this frame.</summary>
    bool mouseRight { get; set; }

    /// <summary>Whether the left button was clicked (edge trigger) this frame.</summary>
    bool MouseLeftClick { get; set; }

    /// <summary>Whether the left button was released (de-click) this frame.</summary>
    bool mouseleftdeclick { get; set; }

    /// <summary>Whether the right button was clicked this frame.</summary>
    bool mouserightclick { get; set; }

    /// <summary>Current cursor X in canvas pixels.</summary>
    int MouseCurrentX { get; set; }

    /// <summary>Current cursor Y in canvas pixels.</summary>
    int MouseCurrentY { get; set; }

    /// <summary>Touch horizontal movement delta.</summary>
    float TouchMoveDx { get; set; }

    /// <summary>Touch vertical movement delta.</summary>
    float TouchMoveDy { get; set; }

    /// <summary>Touch horizontal orientation delta.</summary>
    float TouchOrientationDx { get; set; }

    /// <summary>Touch vertical orientation delta.</summary>
    float TouchOrientationDy { get; set; }

    /// <summary>Returns <c>true</c> if the mouse pointer is in free (unlocked) mode.</summary>
    bool GetFreeMouse();

    /// <summary>Sets whether the mouse pointer is free (unlocked).</summary>
    void SetFreeMouse(bool value);

    // =========================================================================
    // Audio
    // =========================================================================

    /// <summary>Audio control subsystem.</summary>
    //AudioControl Audio { get; set; }

    /// <summary>Whether audio is enabled.</summary>
    bool AudioEnabled { get; set; }

    /// <summary>Whether a sound is playing this frame.</summary>
    bool soundnow { get; set; }

    /// <summary>Plays a named audio file at a world-space position.</summary>
    void PlayAudio(string name, float x, float y, float z);

    /// <summary>Plays a named audio file without spatial positioning.</summary>
    void PlayAudio(string file);

    /// <summary>Plays a named audio file at a world-space position (alias).</summary>
    void PlayAudioAt(string file, float x, float y, float z);

    /// <summary>Starts or stops a looping audio clip, optionally restarting it.</summary>
    void AudioPlayLoop(string file, bool play, bool restart);

    // =========================================================================
    // Chat & typing
    // =========================================================================

    /// <summary>The visible chat line history.</summary>
    List<Chatline> ChatLines { get; set; }

    /// <summary>Number of chat lines currently visible.</summary>
    int ChatLinesCount { get; set; }

    /// <summary>Appends a line to the local chat display.</summary>
    void AddChatLine(string message);

    /// <summary>Writes a line to the chat log file.</summary>
    void ChatLog(string p);

    /// <summary>Sends a chat message string to the server.</summary>
    void SendChat(string message);

    /// <summary>Executes a chat string as if the player typed and sent it.</summary>
    void ExecuteChat(string s_);

    /// <summary>Current text in the typing/chat input buffer.</summary>
    string GuiTypingBuffer { get; set; }

    /// <summary>Typing state (idle, chat, command, etc.).</summary>
    TypingState GuiTyping { get; set; }

    /// <summary>Whether the player is in team-chat mode.</summary>
    bool IsTeamchat { get; set; }

    /// <summary>Whether the player is currently typing.</summary>
    bool IsTyping { get; set; }

    /// <summary>History of previously typed lines.</summary>
    List<string> TypingLog { get; set; }

    /// <summary>Current scroll position in the typing history.</summary>
    int TypingLogPos { get; set; }

    /// <summary>Opens the typing/chat input.</summary>
    void StartTyping();

    /// <summary>Closes the typing/chat input.</summary>
    void StopTyping();

    // =========================================================================
    // UI / menus
    // =========================================================================

    /// <summary>Enables or disables the player camera control.</summary>
    bool EnableCameraControl { set; }

    /// <summary>Current menu/escape-menu state.</summary>
    MenuState MenuState { get; set; }

    /// <summary>Whether the escape menu triggered a full restart.</summary>
    bool EscapeMenuRestart { get; set; }

    /// <summary>Active dialog overlays.</summary>
    Dictionary<int, VisibleDialog> Dialogs { get; set; }

    /// <summary>Returns the dialog ID for the given dialog name.</summary>
    int GetDialogId(string name);

    /// <summary>Opens the escape menu.</summary>
    void EscapeMenuStart();

    /// <summary>Shows the escape menu in free-mouse mode.</summary>
    void ShowEscapeMenu();

    /// <summary>Shows the inventory screen in free-mouse mode.</summary>
    void ShowInventory();

    /// <summary>Returns the game to the in-game state from any menu.</summary>
    void GuiStateBackToGame();

    /// <summary>Interprets a command argument string as a boolean.</summary>
    bool BoolCommandArgument(string arguments);

    // =========================================================================
    // Networking
    // =========================================================================

    /// <summary>The active network client.</summary>
    NetClient NetClient { get; set; }

    /// <summary>Current server version string.</summary>
    string ServerGameVersion { get; set; }

    /// <summary>Whether this is a single-player (local server) session.</summary>
    bool IsSinglePlayer { get; set; }

    /// <summary>Spectator follow target username, or <c>null</c>.</summary>
    string Follow { get; set; }

    ConnectionData ConnectData { get; set; }
    bool IsReconnecting { get; set; }
    bool IsExitingToMainMenu { get; set; }
    bool StartedConnecting { get; set; }

    /// <summary>Information about the connected server.</summary>
    ServerInformation ServerInfo { get; set; }

    /// <summary>Per-opcode packet handler table.</summary>
    ClientPacketHandler[] PacketHandlers { get; set; }

    /// <summary>Message displayed when connecting to a server with a mismatched version.</summary>
    string InvalidVersionDrawMessage { get; set; }

    /// <summary>The identification packet received from a version-mismatched server.</summary>
    Packet_Server InvalidVersionPacketIdentification { get; set; }

    /// <summary>Sends a pre-built packet to the server.</summary>
    void SendPacketClient(Packet_Client packet);

    /// <summary>Sends a ping reply to the server.</summary>
    void SendPingReply();

    /// <summary>Processes the initial server identification packet.</summary>
    void ProcessServerIdentification(Packet_Server packet);

    /// <summary>Sends a block-set action to the server and adds a speculative local update.</summary>
    void SendSetBlockAndUpdateSpeculative(int material, int x, int y, int z, PacketBlockSetMode mode);

    /// <summary>Sends a block-set action to the server.</summary>
    void SendSetBlock(int x, int y, int z, PacketBlockSetMode mode, int type, int materialslot);

    /// <summary>Sends a fill-area action to the server.</summary>
    void SendFillArea(int startx, int starty, int startz, int endx, int endy, int endz, int blockType);

    /// <summary>Sends a leave notification to the server.</summary>
    void SendLeave(PacketLeaveReason reason);

    /// <summary>Number of milliseconds since the last server packet was received.</summary>
    int LastReceivedMilliseconds { get; set; }

    /// <summary>Millisecond timestamp of the last position packet sent.</summary>
    int LastPositionSentMilliseconds { get; set; }

    /// <summary>Current simulation time in milliseconds.</summary>
    int CurrentTimeMilliseconds { get; set; }

    /// <summary>Total elapsed session time in milliseconds.</summary>
    int TotalTimeMilliseconds { get; set; }

    // =========================================================================
    // Networking — blob download
    // =========================================================================

    /// <summary>Filename of the asset blob being downloaded.</summary>
    string BlobDownloadName { get; set; }

    /// <summary>Expected MD5 hash of the asset blob.</summary>
    string BlobDownloadMd5 { get; set; }

    /// <summary>Stream receiving the blob download data.</summary>
    MemoryStream BlobDownload { get; set; }

    /// <summary>Finalises a completed blob download and registers the asset.</summary>
    void SetFile(string name, string md5, byte[] downloaded, int downloadedLength);

    // =========================================================================
    // Networking — server redirect & exit
    // =========================================================================>

    /// <summary>Gracefully exits to the main menu.</summary>
    void ExitToMainMenu();

    /// <summary>Disconnects and reconnects to a different server.</summary>
    void ExitAndSwitchServer(Packet_ServerRedirect newServer);

    Packet_ServerRedirect Redirect { get; }

    /// <summary>Sets the player character's eye height.</summary>
    void SetCharacterEyesHeight(float value);

    /// <summary>Returns the player character's current eye height.</summary>
    float GetCharacterEyesHeight();

    void Start();
}