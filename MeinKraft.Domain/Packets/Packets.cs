using MeinKraft;
using MemoryPack;

// All packets use public auto-properties so MemoryPack can serialize them.
// Count/Length fields and Set* methods removed — MemoryPack handles arrays natively.
// Shared structures used by both client and server are defined once.

// ---------------------------------------------------------------------------
// Shared structures (used by both client and server)
// ---------------------------------------------------------------------------

[MemoryPackable]
public partial class Packet_PositionAndOrientation
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public int Heading { get; set; }
    public int Pitch { get; set; }
    public int Stance { get; set; }
}

[MemoryPackable]
public partial class Packet_InventoryPosition
{
    public PacketInventoryPositionType Type { get; set; }
    public int AreaX { get; set; }
    public int AreaY { get; set; }
    public int MaterialId { get; set; }
    public int WearPlace { get; set; }
    public int ActiveMaterial { get; set; }
    public int GroundPositionX { get; set; }
    public int GroundPositionY { get; set; }
    public int GroundPositionZ { get; set; }
}

[MemoryPackable]
public partial class Packet_PositionItem
{
    public string Key_ { get; set; }
    public InventoryItem Value_ { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
}

[MemoryPackable]
public partial class Packet_Inventory
{
    public InventoryItem MainArmor { get; set; }
    public InventoryItem Boots { get; set; }
    public InventoryItem Helmet { get; set; }
    public InventoryItem Gauntlet { get; set; }
    public Packet_PositionItem[] Items { get; set; } = [];
    public InventoryItem DragDropItem { get; set; }
    public InventoryItem[] RightHand { get; set; }
}

[MemoryPackable]
public partial class Packet_IntInt
{
    public int Key_ { get; set; }
    public int Value_ { get; set; }
}

// ---------------------------------------------------------------------------
// Shared entity structures
// ---------------------------------------------------------------------------

[MemoryPackable]
public partial class Packet_ServerPlayerStats
{
    public int CurrentHealth { get; set; }
    public int MaxHealth { get; set; }
    public int CurrentOxygen { get; set; }
    public int MaxOxygen { get; set; }
}

[MemoryPackable]
public partial class Packet_ServerEntity
{
    public Packet_PositionAndOrientation Position { get; set; }
    public Packet_ServerEntityAnimatedModel DrawModel { get; set; }
    public Packet_ServerEntityDrawName DrawName_ { get; set; }
    public Packet_ServerEntityDrawText DrawText { get; set; }
    public Packet_ServerEntityDrawBlock DrawBlock { get; set; }
    public Packet_ServerEntityPush Push { get; set; }
    public bool Usable { get; set; }
    public Packet_ServerPlayerStats PlayerStats { get; set; }
    public Packet_ServerEntityDrawArea DrawArea { get; set; }
}

[MemoryPackable]
public partial class Packet_ServerEntityAnimatedModel
{
    public string Model_ { get; set; }
    public string Texture_ { get; set; }
    public int EyeHeight { get; set; }
    public int ModelHeight { get; set; }
    public int DownloadSkin { get; set; }
}

[MemoryPackable]
public partial class Packet_ServerEntityDrawName
{
    public string Name { get; set; }
    public bool OnlyWhenSelected { get; set; }
    public bool ClientAutoComplete { get; set; }
    public string Color { get; set; }
}

[MemoryPackable]
public partial class Packet_ServerEntityDrawText
{
    public string Text { get; set; }
    public int Dx { get; set; }
    public int Dy { get; set; }
    public int Dz { get; set; }
    public int Rotx { get; set; }
    public int Roty { get; set; }
    public int Rotz { get; set; }
}

[MemoryPackable]
public partial class Packet_ServerEntityDrawBlock
{
    public int BlockType { get; set; }
}

[MemoryPackable]
public partial class Packet_ServerEntityPush
{
    public int RangeFloat { get; set; }
}

[MemoryPackable]
public partial class Packet_ServerEntityDrawArea
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public int Sizex { get; set; }
    public int Sizey { get; set; }
    public int Sizez { get; set; }
    public int VisibleToClientId { get; set; }
}

// ---------------------------------------------------------------------------
// Client → Server packets
// ---------------------------------------------------------------------------

[MemoryPackable]
public partial class Packet_ClientIdentification
{
    public string MdProtocolVersion { get; set; }
    public string Username { get; set; }
    public string VerificationKey { get; set; }
    public string ServerPassword { get; set; }
    public Packet_PositionAndOrientation RequestPosition { get; set; }
}

[MemoryPackable]
public partial class Packet_ClientRequestBlob
{
    public string[] RequestedMd5 { get; set; }
}

[MemoryPackable]
public partial class Packet_ClientSetBlock
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public PacketBlockSetMode Mode { get; set; }
    public int BlockType { get; set; }
    public int MaterialSlot { get; set; }
}

[MemoryPackable]
public partial class Packet_ClientFillArea
{
    public int X1 { get; set; }
    public int X2 { get; set; }
    public int Y1 { get; set; }
    public int Y2 { get; set; }
    public int Z1 { get; set; }
    public int Z2 { get; set; }
    public int BlockType { get; set; }
    public int MaterialSlot { get; set; }
}

[MemoryPackable]
public partial class Packet_ClientPositionAndOrientation
{
    public int PlayerId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public int Heading { get; set; }
    public int Pitch { get; set; }
    public int Stance { get; set; }
}

[MemoryPackable]
public partial class Packet_ClientMessage
{
    public string Message { get; set; }
    public int IsTeamchat { get; set; }
}

[MemoryPackable]
public partial class Packet_ClientInventoryAction
{
    public PacketInventoryActionType Action { get; set; }
    public Packet_InventoryPosition A { get; set; }
    public Packet_InventoryPosition B { get; set; }
}

[MemoryPackable]
public partial class Packet_ClientHealth
{
    public int CurrentHealth { get; set; }
}

[MemoryPackable]
public partial class Packet_ClientOxygen
{
    public int CurrentOxygen { get; set; }
}

[MemoryPackable]
public partial class Packet_ClientDialogClick
{
    public string WidgetId { get; set; }
    public string[] TextBoxValue { get; set; }
}

[MemoryPackable]
public partial class Packet_ClientCraft
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public int RecipeId { get; set; }
}

[MemoryPackable]
public partial class Packet_ClientShot
{
    public int FromX { get; set; }
    public int FromY { get; set; }
    public int FromZ { get; set; }
    public int ToX { get; set; }
    public int ToY { get; set; }
    public int ToZ { get; set; }
    public int WeaponBlock { get; set; }
    public int HitPlayer { get; set; }
    public int IsHitHead { get; set; }
    public int ExplodesAfter { get; set; }
}

[MemoryPackable]
public partial class Packet_ClientSpecialKey
{
    public SpecialKey Key_ { get; set; }
}

[MemoryPackable]
public partial class Packet_ClientActiveMaterialSlot
{
    public int ActiveMaterialSlot { get; set; }
}

[MemoryPackable]
public partial class Packet_ClientLeave
{
    public PacketLeaveReason Reason { get; set; }
}

[MemoryPackable]
public partial class Packet_ClientDeath
{
    public DeathReason Reason { get; set; }
    public int SourcePlayer { get; set; }
}

[MemoryPackable]
public partial class Packet_ClientGameResolution
{
    public int Width { get; set; }
    public int Height { get; set; }
}

[MemoryPackable]
public partial class Packet_ClientEntityInteraction
{
    public int EntityId { get; set; }
    public PacketEntityInteractionType InteractionType { get; set; }
}

[MemoryPackable] public partial class Packet_ClientPingReply { }
[MemoryPackable] public partial class Packet_ClientReload { }
[MemoryPackable] public partial class Packet_ClientServerQuery { }

[MemoryPackable]
public partial class Packet_Client
{
    public PacketType Id { get; set; }
    public Packet_ClientIdentification Identification { get; set; }
    public Packet_ClientSetBlock SetBlock { get; set; }
    public Packet_ClientFillArea FillArea { get; set; }
    public Packet_ClientPositionAndOrientation PositionAndOrientation { get; set; }
    public Packet_ClientMessage Message { get; set; }
    public Packet_ClientCraft Craft { get; set; }
    public Packet_ClientRequestBlob RequestBlob { get; set; }
    public Packet_ClientInventoryAction InventoryAction { get; set; }
    public Packet_ClientHealth Health { get; set; }
    public Packet_ClientPingReply PingReply { get; set; }
    public Packet_ClientDialogClick DialogClick_ { get; set; }
    public Packet_ClientShot Shot { get; set; }
    public Packet_ClientSpecialKey SpecialKey_ { get; set; }
    public Packet_ClientActiveMaterialSlot ActiveMaterialSlot { get; set; }
    public Packet_ClientLeave Leave { get; set; }
    public Packet_ClientReload Reload { get; set; }
    public Packet_ClientOxygen Oxygen { get; set; }
    public Packet_ClientDeath Death { get; set; }
    public Packet_ClientServerQuery Query { get; set; }
    public Packet_ClientGameResolution GameResolution { get; set; }
    public Packet_ClientEntityInteraction EntityInteraction { get; set; }
}

// ---------------------------------------------------------------------------
// Server → Client packets
// ---------------------------------------------------------------------------

[MemoryPackable]
public partial class Packet_ServerIdentification
{
    public string MdProtocolVersion { get; set; }
    public int AssignedClientId { get; set; }
    public string ServerName { get; set; }
    public string ServerMotd { get; set; }
    public int MapSizeX { get; set; }
    public int MapSizeY { get; set; }
    public int MapSizeZ { get; set; }
    public int DisableShadows { get; set; }
    public int PlayerAreaSize { get; set; }
    public int RenderHint_ { get; set; }
    public string[] RequiredBlobMd5 { get; set; }
    public string[] RequiredBlobName { get; set; }
}

[MemoryPackable]
public partial class Packet_ServerBlobInitialize
{
    public string Name { get; set; }
    public string Md5 { get; set; }
}

[MemoryPackable]
public partial class Packet_ServerBlobPart
{
    public byte[] Data { get; set; }
    public int Islastpart { get; set; }
}

[MemoryPackable]
public partial class Packet_ServerSetBlock
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public int BlockType { get; set; }
}

[MemoryPackable]
public partial class Packet_ServerFillArea
{
    public int X1 { get; set; }
    public int X2 { get; set; }
    public int Y1 { get; set; }
    public int Y2 { get; set; }
    public int Z1 { get; set; }
    public int Z2 { get; set; }
    public int BlockType { get; set; }
    public int BlockCount { get; set; }
}

[MemoryPackable]
public partial class Packet_ServerFillAreaLimit
{
    public int Limit { get; set; }
}

[MemoryPackable]
public partial class Packet_ServerFreemove
{
    public int IsEnabled { get; set; }
}

[MemoryPackable]
public partial class Packet_ServerMessage
{
    public string Message { get; set; }
}

[MemoryPackable]
public partial class Packet_ServerDisconnectPlayer
{
    public string DisconnectReason { get; set; }
}

[MemoryPackable]
public partial class Packet_ServerSound
{
    public string Name { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
}

[MemoryPackable]
public partial class Packet_ServerFollow
{
    public string Client { get; set; }
    public int Tpp { get; set; }
}

[MemoryPackable]
public partial class Packet_ServerBullet
{
    public int FromXFloat { get; set; }
    public int FromYFloat { get; set; }
    public int FromZFloat { get; set; }
    public int ToXFloat { get; set; }
    public int ToYFloat { get; set; }
    public int ToZFloat { get; set; }
    public int SpeedFloat { get; set; }
}

[MemoryPackable]
public partial class Packet_ServerProjectile
{
    public int FromXFloat { get; set; }
    public int FromYFloat { get; set; }
    public int FromZFloat { get; set; }
    public int VelocityXFloat { get; set; }
    public int VelocityYFloat { get; set; }
    public int VelocityZFloat { get; set; }
    public int BlockId { get; set; }
    public int ExplodesAfterFloat { get; set; }
    public int SourcePlayerID { get; set; }
}

[MemoryPackable]
public partial class Packet_ServerExplosion
{
    public int XFloat { get; set; }
    public int YFloat { get; set; }
    public int ZFloat { get; set; }
    public int IsRelativeToPlayerPosition { get; set; }
    public int RangeFloat { get; set; }
    public int TimeFloat { get; set; }
}

[MemoryPackable]
public partial class Packet_ServerQueryAnswer
{
    public string Name { get; set; }
    public string MOTD { get; set; }
    public int PlayerCount { get; set; }
    public int MaxPlayers { get; set; }
    public string PlayerList { get; set; }
    public int Port { get; set; }
    public string GameMode { get; set; }
    public bool Password { get; set; }
    public string PublicHash { get; set; }
    public string ServerVersion { get; set; }
    public int MapSizeX { get; set; }
    public int MapSizeY { get; set; }
    public int MapSizeZ { get; set; }
    public byte[] ServerThumbnail { get; set; }
}

[MemoryPackable]
public partial class Packet_ServerRedirect
{
    public string IP { get; set; }
    public int Port { get; set; }
}

[MemoryPackable]
public partial class Packet_ServerBlockType
{
    public int Id { get; set; }
    public BlockType Blocktype { get; set; }
}

[MemoryPackable]
public partial class Packet_ServerSunLevels
{
    public int[] Sunlevels { get; set; }
}

[MemoryPackable]
public partial class Packet_ServerLightLevels
{
    public int[] Lightlevels { get; set; }
}

[MemoryPackable]
public partial class Packet_ServerCraftingRecipes
{
    public CraftingRecipe[] CraftingRecipes { get; set; }
}

[MemoryPackable]
public partial class Packet_ServerLevelProgress
{
    public int PercentComplete { get; set; }
    public string Status { get; set; }
    public int PercentCompleteSubitem { get; set; }
}

[MemoryPackable]
public partial class Packet_ServerChunkPart
{
    public byte[] CompressedChunkPart { get; set; }
}

[MemoryPackable]
public partial class Packet_ServerChunk
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public int SizeX { get; set; }
    public int SizeY { get; set; }
    public int SizeZ { get; set; }
}

[MemoryPackable]
public partial class Packet_ServerHeightmapChunk
{
    public int X { get; set; }
    public int Y { get; set; }
    public int SizeX { get; set; }
    public int SizeY { get; set; }
    public byte[] CompressedHeightmap { get; set; }
}

[MemoryPackable]
public partial class Packet_ServerInventory
{
    public Packet_Inventory Inventory { get; set; }
}

[MemoryPackable]
public partial class Packet_ServerMonster
{
    public int Id { get; set; }
    public int MonsterType { get; set; }
    public Packet_PositionAndOrientation PositionAndOrientation { get; set; }
    public int Health { get; set; }
}

[MemoryPackable]
public partial class Packet_ServerMonsters
{
    public Packet_ServerMonster[] Monsters { get; set; }
}

[MemoryPackable]
public partial class Packet_ServerSeason
{
    public int Hour { get; set; }
    public int DayNightCycleSpeedup { get; set; }
    public int Moon { get; set; }
}

[MemoryPackable]
public partial class Packet_ServerDialog
{
    public string DialogId { get; set; }
    public Dialog Dialog { get; set; }
}

[MemoryPackable]
public partial class Packet_ServerAmmo
{
    public Packet_IntInt[] TotalAmmo { get; set; }
}

[MemoryPackable]
public partial class Packet_ServerTranslatedString
{
    public string Lang { get; set; }
    public string Id { get; set; }
    public string Translation { get; set; }
}

[MemoryPackable]
public partial class Packet_ServerPlayerPing
{
    public int ClientId { get; set; }
    public int Ping { get; set; }
}

[MemoryPackable]
public partial class Packet_ServerEntitySpawn
{
    public int Id { get; set; }
    public Packet_ServerEntity Entity_ { get; set; }
}

[MemoryPackable]
public partial class Packet_ServerEntityPositionAndOrientation
{
    public int Id { get; set; }
    public Packet_PositionAndOrientation PositionAndOrientation { get; set; }
}

[MemoryPackable]
public partial class Packet_ServerEntityDespawn
{
    public int Id { get; set; }
}

[MemoryPackable]
public partial class Packet_ServerPlayerSpawnPosition
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
}

[MemoryPackable] public partial class Packet_ServerLevelInitialize { }
[MemoryPackable] public partial class Packet_ServerBlobFinalize { }
[MemoryPackable] public partial class Packet_ServerBlockTypes { }
[MemoryPackable] public partial class Packet_ServerLevelFinalize { }
[MemoryPackable] public partial class Packet_ServerPing { }

[MemoryPackable]
public partial class Packet_Server
{
    public Packet_ServerIdEnum Id { get; set; }
    public Packet_ServerIdentification Identification { get; set; }
    public Packet_ServerLevelInitialize LevelInitialize { get; set; }
    public Packet_ServerLevelProgress LevelDataChunk { get; set; }
    public Packet_ServerLevelFinalize LevelFinalize { get; set; }
    public Packet_ServerSetBlock SetBlock { get; set; }
    public Packet_ServerFillArea FillArea { get; set; }
    public Packet_ServerFillAreaLimit FillAreaLimit { get; set; }
    public Packet_ServerFreemove Freemove { get; set; }
    public Packet_ServerMessage Message { get; set; }
    public Packet_ServerDisconnectPlayer DisconnectPlayer { get; set; }
    public Packet_ServerChunk Chunk_ { get; set; }
    public Packet_ServerInventory Inventory { get; set; }
    public Packet_ServerSeason Season { get; set; }
    public Packet_ServerBlobInitialize BlobInitialize { get; set; }
    public Packet_ServerBlobPart BlobPart { get; set; }
    public Packet_ServerBlobFinalize BlobFinalize { get; set; }
    public Packet_ServerHeightmapChunk HeightmapChunk { get; set; }
    public Packet_ServerPing Ping { get; set; }
    public Packet_ServerPlayerPing PlayerPing { get; set; }
    public Packet_ServerSound Sound { get; set; }
    public Packet_ServerPlayerStats PlayerStats { get; set; }
    public Packet_ServerMonsters Monster { get; set; }
    public Packet_ServerPlayerSpawnPosition PlayerSpawnPosition { get; set; }
    public Packet_ServerBlockTypes BlockTypes { get; set; }
    public Packet_ServerSunLevels SunLevels { get; set; }
    public Packet_ServerLightLevels LightLevels { get; set; }
    public Packet_ServerCraftingRecipes CraftingRecipes { get; set; }
    public Packet_ServerDialog Dialog { get; set; }
    public Packet_ServerFollow Follow { get; set; }
    public Packet_ServerBullet Bullet { get; set; }
    public Packet_ServerAmmo Ammo { get; set; }
    public Packet_ServerBlockType BlockType { get; set; }
    public Packet_ServerChunkPart ChunkPart { get; set; }
    public Packet_ServerExplosion Explosion { get; set; }
    public Packet_ServerProjectile Projectile { get; set; }
    public Packet_ServerTranslatedString Translation { get; set; }
    public Packet_ServerQueryAnswer QueryAnswer { get; set; }
    public Packet_ServerRedirect Redirect { get; set; }
    public Packet_ServerEntitySpawn EntitySpawn { get; set; }
    public Packet_ServerEntityPositionAndOrientation EntityPosition { get; set; }
    public Packet_ServerEntityDespawn EntityDespawn { get; set; }
}

public enum PacketType
{
    PlayerIdentification = 0,
    PingReply = 1,
    SetBlock = 5,
    FillArea = 510,
    PositionAndOrientation = 8,
    Craft = 9,
    Message = 13,
    DialogClick = 14,
    RequestBlob = 50,
    InventoryAction = 51,
    Health = 52,
    MonsterHit = 53,
    Shot = 54,
    SpecialKey = 55,
    ActiveMaterialSlot = 56,
    Leave = 57,
    Reload = 58,
    Oxygen = 59,
    Death = 60,
    EntityInteraction = 61,
    ServerQuery = 64,
    GameResolution = 10,
    ExtendedPacketCommand = 100
}

public enum PacketInventoryActionType
{
    Click = 0,
    WearItem = 1,
    MoveToInventory = 2
}

public enum PacketInventoryPositionType
{
    MainArea = 0,
    Ground = 1,
    MaterialSelector = 2,
    WearPlace = 3
}

public enum PacketBlockSetMode
{
    Destroy = 0,
    Create = 1,
    Use = 2,
    UseWithTool = 3
}

public enum PacketLeaveReason
{
    Leave = 0,
    Crash = 1
}

public enum PacketEntityInteractionType
{
    Use = 0,
    Hit = 1
}

public enum Packet_ServerIdEnum
{
    ServerIdentification = 0,
    Ping = 1,
    PlayerPing = 111,
    LevelInitialize = 2,
    LevelDataChunk = 3,
    LevelFinalize = 4,
    SetBlock = 6,
    FillArea = 61,
    FillAreaLimit = 62,
    Message = 13,
    DisconnectPlayer = 14,
    Chunk_ = 15,
    FiniteInventory = 16,
    Season = 17,
    BlobInitialize = 18,
    BlobPart = 19,
    BlobFinalize = 20,
    HeightmapChunk = 21,
    Sound = 22,
    PlayerStats = 23,
    Monster = 24,
    ActiveMonsters = 25,
    PlayerSpawnPosition = 26,
    BlockTypes = 27,
    SunLevels = 28,
    LightLevels = 29,
    CraftingRecipes = 30,
    RemoveMonsters = 50,
    Freemove = 51,
    Dialog = 52,
    Follow = 53,
    Bullet = 54,
    Ammo = 55,
    BlockType = 56,
    ChunkPart = 57,
    Explosion = 58,
    Projectile = 59,
    Translation = 60,
    QueryAnswer = 64,
    ServerRedirect = 65,
    EntitySpawn = 66,
    EntityPosition = 67,
    EntityDespawn = 68,
    ExtendedPacketCommand = 100,
    ExtendedPacketTick = 101
}