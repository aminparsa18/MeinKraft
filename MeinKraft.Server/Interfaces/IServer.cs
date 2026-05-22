using MeinKraft;
using OpenTK.Mathematics;
using static MeinKraft.ServerPacketService;

public interface IServer
{
    public List<ServerSystem> Systems { get; set; }
    double InvertedChunkSize { get; set; }
    List<string> AllPrivileges { get; set; }
    int ChunkDrawDistance { get; }
    List<CraftingRecipe> CraftingRecipes { get; set; }
    Group? DefaultGroupGuest { get; set; }
    Group DefaultGroupRegistered { get; set; }
    Vector3i DefaultPlayerSpawn { get; set; }
    Dictionary<string, bool> Disabledprivileges { get; set; }
    int DrawDistance { get; set; }
    bool EnableShadows { get; set; }
    Dictionary<string, bool> ExtraPrivileges { get; set; }
    string GameMode { get; set; }
    List<ActiveHttpModule> HttpModules { get; set; }
    Dictionary<string, Inventory> Inventory { get; set; }
    NetServer[] MainSockets { get; set; }
    List<Action> OnLoad { get; set; }
    List<Action> OnSave { get; set; }
    int Port { get; set; }
    string ReceivedKey { get; set; }
    RenderHint RenderHint { get; set; }
    List<ServerTimerRegistration> Timers { get; }
    long TotalReceivedBytes { get; set; }
    abstract int InvertChunk(int num);
    abstract IEnumerable<byte[]> Parts(byte[] blob, int partsize);
    abstract Vector3i PlayerBlockPosition(ServerPlayer c);
    abstract byte[] Serialize(Packet_Server p);
    abstract int SerializeFloat(float p);
    void AddEntity(int x, int y, int z, ServerEntity e);
    bool Announcement(int sourceClientId, string message);
    bool AnswerMessage(int sourceClientId, string message);
    bool AreaAdd(int sourceClientId, int id, string coords, string[] permittedGroups, string[] permittedUsers, int? level);
    bool AreaDelete(int sourceClientId, int id);
    bool ChangeGroup(int sourceClientId, string target, string newGroupName);
    bool ChangeGroupOffline(int sourceClientId, string target, string newGroupName);
    bool CheckBuildPrivileges(int player, int x, int y, int z, PacketBlockSetMode mode);
    bool ClearInterpreter(int sourceClientId);
    bool ClientSeenChunk(int clientid, int vx, int vy, int vz);
    void ClientSeenChunkRemove(int clientid, int vx, int vy, int vz);
    void ClientSeenChunkSet(int clientid, int vx, int vy, int vz, int time);
    void CommandInterpreter(int sourceClientId, string command, string argument);
    byte[] CompressChunkNetwork(ushort[] chunk);
    void DeleteChunk(int x, int y, int z);
    void DeleteChunks(List<Vector3i> chunkPositions);
    void DespawnEntity(ServerEntityId id);
    Vector3i DontSpawnPlayerInWater(Vector3i initialSpawn);
    void Exit();
    int GetBlock(int x, int y, int z);
    ushort[] GetChunk(int x, int y, int z);
    ushort[] GetChunkFromDatabase(int x, int y, int z, string filename);
    Dictionary<Vector3i, ushort[]> GetChunksFromDatabase(List<Vector3i> chunks, string filename);
    ServerEntity GetEntity(int chunkx, int chunky, int chunkz, int id);
    string GetGroupColor(int playerid);
    string GetGroupName(int playerid);
    int GetHeight(int x, int y);
    InventoryUtil GetInventoryUtil(Inventory inventory);
    int[] GetMapSize();
    Inventory GetPlayerInventory(string playername);
    Vector3i GetPlayerSpawnPositionMul32(int clientid);
    GameTimer GetTimer();
    bool Give(int sourceClientId, string target, string blockname, int amount);
    bool GiveAll(int sourceClientId, string target);
    void Help(int sourceClientId);
    void InstallHttpModule(string name, Func<string> description, IHttpModule module);
    bool Kick(int sourceClientId, int targetClientId);
    bool Kick(int sourceClientId, int targetClientId, string reason);
    bool Kick(int sourceClientId, string target);
    bool Kick(int sourceClientId, string target, string reason);
    void KillPlayer(int clientid);
    bool List(int sourceClientId, string type);
    bool Login(int sourceClientId, string targetGroupString, string password);
    bool Monsters(int sourceClientId, string option);
    void NotifyInventory(int clientid);
    void OnConfigLoaded();
    void PlayerEntitySetDirty(int player);
    bool PlayerHasPrivilege(int player, string privilege);
    void PlaySoundAt(int posx, int posy, int posz, string sound);
    void PlaySoundAt(int posx, int posy, int posz, string sound, int range);
    bool PrivateMessage(int sourceClientId, string recipient, string message);
    bool PrivilegeAdd(int sourceClientId, string target, string privilege);
    bool PrivilegeRemove(int sourceClientId, string target, string privilege);
    void Process(float dt);
    void ProcessMain();
    void BroadcastSeason();
    void ReceiveServerConsole(string message);
    bool RemoveClientFromConfig(int sourceClientId, string target);
    bool ResetInventory(int sourceClientId, string target);
    void ResetPlayerInventory(ServerPlayer client);
    void Restart();
    bool RestartServer(int sourceClientId);
    void SaveChunksToDatabase(List<Vector3i> chunkPositions, string filename);
    void SendAmmo(int playerid, Dictionary<int, int> totalAmmo);
    void SendBlockTypes(int clientid);
    void SendDialog(int player, string id, Dialog dialog);
    void SendExplosion(int player, float x, float y, float z, bool relativeposition, float range, float time);
    void SendFreemoveState(int clientid, bool isEnabled);
    void SendMessageToAll(string message);
    void SendPacketFollow(int player, int target, bool tpp);
    void SendServerRedirect(int clientid, string ip_, int port_);
    void SendSound(int clientid, string name, int x, int y, int z);
    void ServerMessageToAll(string message, MessageType color);
    void SetBlock(int x, int y, int z, int blocktype);
    void SetBlockAndNotify(int x, int y, int z, int blocktype);
    void SetBlockType(int id, string name, BlockType block);
    void SetBlockType(string name, BlockType block);
    void SetChunk(int x, int y, int z, ushort[] data);
    void SetChunks(Dictionary<Vector3i, ushort[]> chunks);
    void SetChunks(int offsetX, int offsetY, int offsetZ, Dictionary<Vector3i, ushort[]> chunks);
    void SetEntityDirty(ServerEntityId id);
    bool SetFillAreaLimit(int sourceClientId, string targetType, string target, int maxFill);
    void SetLightLevels(float[] lightLevels);
    bool SetLogging(int sourceClientId, string type, string option);
    bool SetSpawnPosition(int sourceClientId, int x, int y, int? z);
    bool SetSpawnPosition(int sourceClientId, string targetType, string target, int x, int y, int? z);
    void SetSunLevels(int[] sunLevels);
    bool ShutdownServer(int sourceClientId);
    void Stop();
    bool TeleportPlayer(int sourceClientId, string target, int x, int y, int? z);
    bool TeleportToPlayer(int sourceClientId, int clientTo);
    bool TeleportToPosition(int sourceClientId, int x, int y, int? z);
    bool TimeCommand(int sourceClientId, string argument);
    bool WelcomeMessage(int sourceClientId, string welcomeMessage);
}