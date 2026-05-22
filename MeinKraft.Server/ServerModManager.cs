using Microsoft.Extensions.Options;
using OpenTK.Mathematics;
using System.Drawing;
using System.Runtime.InteropServices;

namespace MeinKraft;

public class ServerModManager(IBlockRegistry blockRegistry, IChunkDbCompressed chunkDb, IGameLogger gameLogger,
    IServerMapStorage serverMapStorage, ILanguageService languageService, IOptions<ServerConfig> options, IServerPacketService serverPacketService,
    ServerGameService server, ISaveGameService saveGameService, IClientRegistry serverClientService, IPlayerStatusService playerStatusService) : IServerModManager
{
    private readonly IBlockRegistry _blockRegistry = blockRegistry;
    private readonly IChunkDbCompressed _chunkDb = chunkDb;
    private readonly IServerMapStorage _serverMapStorage = serverMapStorage;
    private readonly ILanguageService _languageService = languageService;
    private readonly ServerConfig _config = options.Value;
    private readonly IGameLogger _gameLogger = gameLogger;
    private readonly IClientRegistry _serverClientService = serverClientService;
    private readonly IPlayerStatusService _playerStatusService = playerStatusService;
    private readonly IServerPacketService _serverPacketService = serverPacketService;

    public int GetMaxBlockTypes() => GameConstants.MAX_BLOCKTYPES;

    public void SetBlockType(int id, string name, BlockType block)
    {
        block.Sounds ??= defaultSounds;
        _server.SetBlockType(id, name, block);
    }

    public void SetBlockType(string name, BlockType block)
    {
        block.Sounds ??= defaultSounds;
        _server.SetBlockType(name, block);
    }

    public int GetBlockId(string name)
    {
        foreach ((int id, BlockType? blockType) in _blockRegistry.BlockTypes)
        {
            if (blockType.Name == name)
            {
                return id;
            }
        }

        throw new Exception(name);
    }

    public void AddToCreativeInventory(string blockType)
    {
        int id = GetBlockId(blockType);
        if (id == -1)
        {
            throw new Exception(blockType);
        }

        _blockRegistry.BlockTypes[id].IsBuildable = true;
        _blockRegistry.RegisterBlockType(id, _blockRegistry.BlockTypes[id]);
    }

    public int GetMapSizeX() => _serverMapStorage.MapSizeX;
    public int GetMapSizeY() => _serverMapStorage.MapSizeY;
    public int GetMapSizeZ() => _serverMapStorage.MapSizeZ;

    public int GetBlock(int x, int y, int z) => _serverMapStorage.GetBlock(x, y, z);

    public string GetBlockName(int blockType) => _blockRegistry.BlockTypes[blockType].Name;

    public string GetBlockNameAt(int x, int y, int z) => GetBlockName(GetBlock(x, y, z));

    public void SetBlock(int x, int y, int z, int tileType) => _server.SetBlockAndNotify(x, y, z, tileType);

    private ServerGameService _server => server;

    public void SetSunLevels(int[] sunLevels) => _server.SetSunLevels(sunLevels);

    public void SetLightLevels(float[] lightLevels) => _server.SetLightLevels(lightLevels);

    private const string recipeError = "Recipe error:";

    public void AddCraftingRecipe(string output, int outputAmount, string Input0, int Input0Amount)
    {
        if (GetBlockId(output) == -1)
        {
            Console.WriteLine(recipeError + output);
            return;
        }

        if (GetBlockId(Input0) == -1)
        {
            Console.WriteLine(recipeError + Input0);
            return;
        }

        CraftingRecipe r = new()
        {
            Ingredients =
            [
                    new Ingredient(){Type=GetBlockId(Input0), Amount=Input0Amount},
            ],
            Output = new Ingredient() { Type = GetBlockId(output), Amount = outputAmount }
        };
        _server.CraftingRecipes.Add(r);
    }

    public void AddCraftingRecipe2(string output, int outputAmount, string Input0, int Input0Amount, string Input1, int Input1Amount)
    {
        if (GetBlockId(output) == -1)
        {
            Console.WriteLine(recipeError + output);
            return;
        }

        if (GetBlockId(Input0) == -1)
        {
            Console.WriteLine(recipeError + Input0);
            return;
        }

        if (GetBlockId(Input1) == -1)
        {
            Console.WriteLine(recipeError + Input1);
            return;
        }

        CraftingRecipe r = new()
        {
            Ingredients =
            [
                    new Ingredient(){Type=GetBlockId(Input0), Amount=Input0Amount},
                    new Ingredient(){Type=GetBlockId(Input1), Amount=Input1Amount},
            ],
            Output = new Ingredient() { Type = GetBlockId(output), Amount = outputAmount }
        };
        _server.CraftingRecipes.Add(r);
    }

    public void AddCraftingRecipe3(string output, int outputAmount, string Input0, int Input0Amount, string Input1, int Input1Amount, string Input2, int Input2Amount)
    {
        if (GetBlockId(output) == -1)
        {
            Console.WriteLine(recipeError + output);
            return;
        }

        if (GetBlockId(Input0) == -1)
        {
            Console.WriteLine(recipeError + Input0);
            return;
        }

        if (GetBlockId(Input1) == -1)
        {
            Console.WriteLine(recipeError + Input1);
            return;
        }

        if (GetBlockId(Input2) == -1)
        {
            Console.WriteLine(recipeError + Input2);
            return;
        }

        CraftingRecipe r = new()
        {
            Ingredients =
            [
                    new Ingredient(){Type=GetBlockId(Input0), Amount=Input0Amount},
                    new Ingredient(){Type=GetBlockId(Input1), Amount=Input1Amount},
                    new Ingredient(){Type=GetBlockId(Input2), Amount=Input2Amount},
            ],
            Output = new Ingredient() { Type = GetBlockId(output), Amount = outputAmount }
        };
        _server.CraftingRecipes.Add(r);
    }

    public void SetString(string language, string id, string translation) => _languageService.Override(language, id, translation);

    public bool IsValidPos(int x, int y, int z) => VectorUtils.IsValidPos(_serverMapStorage, x, y, z);

    public void RegisterTimer(Action a, double interval) => _server.Timers.Add(new ServerTimerRegistration(
        new ServerTimer { Interval = TimeSpan.FromSeconds(interval) }, a));

    public void PlaySoundAt(int posx, int posy, int posz, string sound) => _server.PlaySoundAt(posx, posy, posz, sound);

    public int NearestPlayer(int x, int y, int z)
    {
        int closeplayer = -1;
        int closedistance = -1;
        foreach (KeyValuePair<int, ServerPlayer> k in _serverClientService.Clients)
        {
            int distance = VectorUtils.DistanceSquared(new Vector3i(k.Value.PositionMul32GlX / 32, k.Value.PositionMul32GlZ / 32, k.Value.PositionMul32GlY / 32), new Vector3i(x, y, z));
            if (closedistance == -1 || distance < closedistance)
            {
                closedistance = distance;
                closeplayer = k.Key;
            }
        }

        return closeplayer;
    }

    public void GrabBlock(int player, int block) => GrabBlocks(player, block, 1);

    public void GrabBlocks(int player, int block, int amount)
    {
        Inventory inventory = _server.GetPlayerInventory(_serverClientService.GetClient(player).PlayerName);

        InventoryItem item = new()
        {
            InventoryItemType = InventoryItemType.Block,
            BlockCount = amount,
            BlockId = _blockRegistry.WhenPlayerPlacesGetsConvertedTo[block]
        };
        _server.GetInventoryUtil(inventory).GrabItem(item, 0);
    }

    public bool PlayerHasPrivilege(int player, string privilege) => _server.PlayerHasPrivilege(player, privilege);

    public bool IsCreative => _config.IsCreative;

    public bool IsBlockFluid(int block) => _blockRegistry.BlockTypes[block].IsFluid();

    public void NotifyInventory(int player)
    {
        _serverClientService.GetClient(player).IsInventoryDirty = true;
        _server.NotifyInventory(player);
    }

    public void SendMessage(int player, string p) => _serverPacketService.SendMessage(player, p);

    public void RegisterPrivilege(string p)
    {
        // Add to list of all available privileges on server
        if (!_server.AllPrivileges.Contains(p))
        {
            _server.AllPrivileges.Add(p);
        }
        // Direct modification of console client as mods are loaded after privileges are assigned
        if (!_serverClientService.ServerConsoleClient.Privileges.Contains(p))
        {
            _serverClientService.ServerConsoleClient.Privileges.Add(p);
        }
    }

    public int GetChunkSize() => GameConstants.ServerChunkSize;

    public int Seed => saveGameService.Seed;

    public void SetDefaultSounds(SoundSet defaultSounds) => this.defaultSounds = defaultSounds;
    private SoundSet defaultSounds;

    public byte[] GetGlobalData(string name) => saveGameService.ModData.TryGetValue(name, out byte[]? value) ? value : null;

    public void SetGlobalData(string name, byte[] value) => saveGameService.ModData[name] = value;

    public void RegisterOnLoad(Action f) => _server.OnLoad.Add(f);

    public void RegisterOnSave(Action f) => _server.OnSave.Add(f);

    public string GetPlayerIp(int player) => _serverClientService.GetClient(player).Socket.RemoteEndPoint().AddressToString();

    public string GetPlayerName(int player) => _serverClientService.GetClient(player).PlayerName;

    public List<string> required { get; set; } = [];

    public void RequireMod(string modname) => required.Add(modname);

    public void SetGlobalDataNotSaved(string name, object value) => _notsaved[name] = value;

    public object GetGlobalDataNotSaved(string name) => !_notsaved.TryGetValue(name, out object? value) ? null : value;

    private readonly Dictionary<string, object> _notsaved = [];

    public void SendMessageToAll(string message) => _server.SendMessageToAll(message);

    public void RegisterCommandHelp(string command, string help) => _server.commandhelps[command] = help;

    public void AddToStartInventory(string blocktype, int amount) => _blockRegistry.StartInventoryAmount[GetBlockId(blocktype)] = amount;

    public long GetCurrentTick() => saveGameService.SimulationCurrentFrame;

    public void SetDaysPerYear(int days) => _server.GetTimer().DaysPerYear = days > 0
        ? days 
        : throw new ArgumentOutOfRangeException("The number of days per year must be greater than 0!");

    public int GetDaysPerYear() => _server.GetTimer().DaysPerYear;

    public int GetSeason() => _server.GetTimer().Season;

    public void UpdateBlockTypes()
    {
        foreach (KeyValuePair<int, ServerPlayer> k in _serverClientService.Clients)
        {
            _server.SendBlockTypes(k.Key);
        }
    }

    public double GetGameDayRealHours()
    {
        double nSecondsPerDay = TimeSpan.FromDays(1).TotalSeconds;

        int nSpeed = _server.GetTimer().SpeedOfTime;
        double nSeconds = nSecondsPerDay / nSpeed;

        return TimeSpan.FromSeconds(nSeconds).TotalHours;
    }

    public void SetGameDayRealHours(double hours)
    {
        double nSecondsPerDay = TimeSpan.FromDays(1).TotalSeconds;

        double nSecondsGiven = TimeSpan.FromHours(hours).TotalSeconds;

        _server.GetTimer().SpeedOfTime = (int)(nSecondsPerDay / nSecondsGiven);
    }

    public float GetPlayerPositionX(int player) => (float)_serverClientService.GetClient(player).PositionMul32GlX / 32;

    public float GetPlayerPositionY(int player) => (float)_serverClientService.GetClient(player).PositionMul32GlZ / 32;

    public float GetPlayerPositionZ(int player) => (float)_serverClientService.GetClient(player).PositionMul32GlY / 32;

    public void SetPlayerPosition(int player, float x, float y, float z)
    {
        ServerEntityPositionAndOrientation pos;
        if (_serverClientService.Clients[player].PositionOverride == null)
        {
            //No position override so far. Clone from player position
            pos = _serverClientService.Clients[player].Entity.Position.Clone();
        }
        else
        {
            //Position has already been modified. Clone from override to prevent data loss
            pos = _serverClientService.Clients[player].PositionOverride.Clone();
        }

        pos.X = x;
        pos.Y = z;
        pos.Z = y;
        _serverClientService.Clients[player].PositionOverride = pos;
    }

    public int GetPlayerHeading(int player) => _serverClientService.GetClient(player).PositionHeading;

    public int GetPlayerPitch(int player) => _serverClientService.GetClient(player).PositionPitch;

    public void SetPlayerOrientation(int player, int heading, int pitch, int stance)
    {
        ServerEntityPositionAndOrientation pos;
        if (_serverClientService.Clients[player].PositionOverride == null)
        {
            //No position override so far. Clone from player position
            pos = _serverClientService.Clients[player].Entity.Position.Clone();
        }
        else
        {
            //Position has already been modified. Clone from override to prevent data loss
            pos = _serverClientService.Clients[player].PositionOverride.Clone();
        }

        pos.Heading = (byte)heading;
        pos.Pitch = (byte)pitch;
        pos.Stance = (byte)stance;
        _serverClientService.Clients[player].PositionOverride = pos;
    }

    public int[] AllPlayers()
    {
        List<int> players = [];
        foreach (KeyValuePair<int, ServerPlayer> k in _serverClientService.Clients)
        {
            players.Add(k.Key);
        }

        return [.. players];
    }

    public void SetPlayerAreaSize(int size)
    {
        _server.playerareasize = size;
        _server.centerareasize = size / 2;
        _server.DrawDistance = size / 2;
    }

    public void AddPermissionArea(int x1, int y1, int z1, int x2, int y2, int z2, int permissionLevel)
    {
        AreaConfig area = new()
        {
            Level = permissionLevel,
            Coords = string.Format("{0},{1},{2},{3},{4},{5}", x1, y1, z1, x2, y2, z2)
        };
        _config.Areas.Add(area);
      //  _config.ConfigNeedsSaving = true;
    }

    public void RemovePermissionArea(int x1, int y1, int z1, int x2, int y2, int z2)
    {
        for (int i = _config.Areas.Count - 1; i >= 0; i--)
        {
            string coords = string.Format("{0},{1},{2},{3},{4},{5}", x1, y1, z1, x2, y2, z2);
            if (_config.Areas[i].Coords == coords)
            {
                _config.Areas.RemoveAt(i);
               // _config.ConfigNeedsSaving = true;
            }
        }
    }

    public int GetPlayerPermissionLevel(int player) => _serverClientService.Clients[player].ClientGroup.Level;

    public void SetCreative(bool value) => _config.IsCreative = value;

    public void SetWorldSize(int x, int y, int z) => _serverMapStorage.Reset(x, y, z);

    public int[] GetScreenResolution(int player) => _serverClientService.Clients[player].WindowSize;

    public void SendDialog(int player, string id, Dialog dialog) => _server.SendDialog(player, id, dialog);

    public void SetPlayerModel(int player, string model, string texture)
    {
        _serverClientService.Clients[player].Model = model;
        _serverClientService.Clients[player].Texture = texture;
        _server.PlayerEntitySetDirty(player);
    }

    public void RenderHint(RenderHint hint) => _server.RenderHint = hint;

    public void EnableFreemove(int player, bool enable) => _server.SendFreemoveState(player, enable);

    public int GetPlayerHealth(int player)
    {
        string name = GetPlayerName(player);
        return _playerStatusService.GetPlayerStats(name).CurrentHealth;
    }

    public int GetPlayerMaxHealth(int player)
    {
        string name = GetPlayerName(player);
        return _playerStatusService.GetPlayerStats(name).MaxHealth;
    }

    public void SetPlayerHealth(int player, int health, int maxhealth)
    {
        string name = GetPlayerName(player);
        _playerStatusService.GetPlayerStats(name).CurrentHealth = health;
        _playerStatusService.GetPlayerStats(name).MaxHealth = maxhealth;
        _serverClientService.Clients[player].IsPlayerStatsDirty = true;
        _playerStatusService.NotifyPlayerStats(player);
    }

    public float[] GetDefaultSpawnPosition(int player)
    {
        Vector3i pos = _server.GetPlayerSpawnPositionMul32(player);
        return [(float)pos.X / 32, (float)pos.Z / 32, (float)pos.Y / 32];
    }

    public string ServerName => _config.Name;

    public string ServerMotd => _config.Motd;

    [DllImport("libc")]
    private static extern int uname(IntPtr buf);

    private static bool checkedIsArm;
    private static bool isArm;

    public static bool IsArm
    {
        get
        {
            if (Environment.OSVersion.Platform != PlatformID.Unix)
            {
                return false;
            }

            if (!checkedIsArm)
            {
                IntPtr buf = Marshal.AllocHGlobal(8192);
                if (uname(buf) == 0)
                {
                    // todo
                    for (int i = 0; i < 8192 - 3; i++)
                    {
                        if (Marshal.ReadByte(new IntPtr(buf.ToInt64() + i + 0)) == 'a'
                            && Marshal.ReadByte(new IntPtr(buf.ToInt64() + i + 1)) == 'r'
                            && Marshal.ReadByte(new IntPtr(buf.ToInt64() + i + 2)) == 'm')
                        {
                            isArm = true;
                        }
                    }
                }

                Marshal.FreeHGlobal(buf);
                checkedIsArm = true;
            }

            return isArm;
        }
    }

    public float[] MeasureTextSize(string text, DialogFont font)
    {
        if (IsArm)
        {
            // fixes crash
            return [text.Length * 1f * font.Size, 1.7f * font.Size];
        }
        else
        {
            using Bitmap bmp = new(1, 1);
            using Graphics g = Graphics.FromImage(bmp);
            SizeF size = g.MeasureString(text, new Font(font.FamilyName, font.Size, (FontStyle)font.FontStyle), new PointF(0, 0), new StringFormat(StringFormatFlags.MeasureTrailingSpaces));
            return [size.Width, size.Height];
        }
    }

    public string ServerIp => "!SERVER_IP!";

    public string ServerPort => "!SERVER_PORT!";

    public float GetPlayerPing(int player) => _serverClientService.Clients[player].LastPing;

    public int AddBot(string name)
    {
        int id = _serverClientService.GenerateClientId();
        ServerPlayer c = new()
        {
            Id = id,
            IsBot = true,
            PlayerName = name
        };
        _serverClientService.Clients[id] = c;
        c.State = ClientStateOnServer.Playing;
       // c.Socket = new DummyNetConnection(network);
        c.Ping.Timeout = TimeSpan.MaxValue;
        c.chunksseen = new bool[_serverMapStorage.MapSizeX / GameConstants.ServerChunkSize
                                * _serverMapStorage.MapSizeY / GameConstants.ServerChunkSize * _serverMapStorage.MapSizeZ / GameConstants.ServerChunkSize];
        c.AssignGroup(_server.DefaultGroupRegistered);
        _server.PlayerEntitySetDirty(id);
        return id;
    }

    public bool IsBot(int player) => _serverClientService.Clients[player].IsBot;

    public void SetPlayerHeight(int player, float eyeheight, float modelheight)
    {
        _serverClientService.Clients[player].EyeHeight = eyeheight;
        _serverClientService.Clients[player].ModelHeight = modelheight;
        _server.PlayerEntitySetDirty(player);
    }

    public void DisablePrivilege(string privilege) => _server.Disabledprivileges[privilege] = true;

    public Inventory GetInventory(int player) => _server.GetPlayerInventory(_serverClientService.Clients[player].PlayerName);

    public int GetActiveMaterialSlot(int player) => _serverClientService.Clients[player].ActiveMaterialSlot;

    public void FollowPlayer(int player, int target, bool tpp) => _server.SendPacketFollow(player, target, tpp);

    public void SetPlayerSpectator(int player, bool isSpectator) => _serverClientService.Clients[player].IsSpectator = isSpectator;

    public BlockType GetBlockType(int block) => _blockRegistry.BlockTypes[block];

    public void NotifyAmmo(int player, Dictionary<int, int> totalAmmo) => _server.SendAmmo(player, totalAmmo);

    public void EnableExtraPrivilegeToAll(string privilege, bool enable)
    {
        if (enable)
        {
            _server.ExtraPrivileges[privilege] = true;
        }
        else
        {
            _server.ExtraPrivileges.Remove(privilege);
        }
    }

    public void LogServerEvent(string serverEvent) => _gameLogger.Server.Debug(serverEvent);

    public void SetWorldDatabaseReadOnly(bool readOnly) => _chunkDb.ReadOnly = readOnly;

    public void SendExplosion(int player, float x, float y, float z, bool relativeposition, float range, float time) => _server.SendExplosion(player, x, y, z, relativeposition, range, time);

    public string GetGroupColor(int player) => _server.GetGroupColor(player);

    public string GetGroupName(int player) => _server.GetGroupName(player);
}
