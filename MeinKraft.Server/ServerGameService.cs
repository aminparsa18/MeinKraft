using MeinKraft;
using MemoryPack;
using Microsoft.Extensions.Options;
using OpenTK.Mathematics;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using static MeinKraft.ServerPacketService;

public partial class ServerGameService : IServer, IDropItem, IDisposable
{
    private readonly IBlockRegistry _blockRegistry;
    private readonly IAssetManager _assetManager;
    private readonly IModEvents _modEvents;
    private readonly ICompression _networkCompression;
    private readonly IChunkDbCompressed _chunkDb;
    private readonly IServerMapStorage _serverMapStorage;
    private readonly ILanguageService _languageService;
    private readonly ServerConfig _config;
    private readonly ISaveGameService _saveGameService;
    private readonly IPlayerStatusService _playerStatusService;
    private readonly IClientRegistry _serverClientService;
    private readonly IServerPacketService _serverPacketService;
    private readonly ISessionConfig _sessionConfig;
    private readonly IGameLogger _gameLogger;

    public List<ServerSystem> Systems { get; set; }

    public ServerGameService(IBlockRegistry blockRegistry, IChunkDbCompressed chunkDb, ILanguageService languageService, ISessionConfig sessionConfig,
    IAssetManager assetManager, IModEvents modEvents, ICompression compression, IServerMapStorage serverMapStorage, IServerPacketService serverPacketService,
    IOptions<ServerConfig> options, ISaveGameService saveGameService, IPlayerStatusService playerStatusService, IClientRegistry serverClientService,
    IGameLogger gameLogger)
    {
        _blockRegistry = blockRegistry;
        _assetManager = assetManager;
        _config = options.Value;
        _networkCompression = compression;
        _modEvents = modEvents;
        _chunkDb = chunkDb;
        _serverMapStorage = serverMapStorage;
        _languageService = languageService;
        _saveGameService = saveGameService;
        _playerStatusService = playerStatusService;
        _serverClientService = serverClientService;
        _serverPacketService = serverPacketService;
        _gameLogger = gameLogger;
        _sessionConfig = sessionConfig;

        _languageService.LoadTranslations();

        MainSockets = new NetServer[3];
    }

    private CraftingTableTool _craftingTableTool;
    public NetServer[] MainSockets { get; set; }

    public bool EnableShadows { get; set; } = true;

    public void Process(float dt)
    {
        for (int i = 0; i < Systems.Count; i++)
        {
            Systems[i].Update(this, dt);
        }

        //Do server stuff
        ProcessMain();
    }

    private readonly GameTimer _gameTimer = new();

    public void ProcessMain()
    {
        if (MainSockets == null)
        {
            return;
        }

        long tickStart = Stopwatch.GetTimestamp();

        // Advance in-game time. SeasonBroadcastTask handles the client notification
        // when the quarter-hour changes — no broadcast check needed here.
        _gameTimer.Tick();

        // SimulationLoop already provides fixed-step timing — one frame per tick.
        _saveGameService.SimulationCurrentFrame++;

        //Process client packets
        for (int i = 0; i < MainSockets.Length; i++)
        {
            NetServer mainSocket = MainSockets[i];
            if (mainSocket == null)
            {
                continue;
            }

            NetIncomingMessage msg;
            while ((msg = mainSocket.ReadMessage()) != null)
            {
                ProcessNetMessage(msg, mainSocket);
            }
        }

        _gameLogger.Server.Debug($"{_serverClientService.Clients.Count} client socket is gonna update");
        foreach (KeyValuePair<int, ServerPlayer> k in _serverClientService.Clients)
        {
            k.Value.Socket.Update();
        }

        //Send updates to player
        foreach (KeyValuePair<int, ServerPlayer> k in _serverClientService.Clients)
        {
           // k.Value.NotifyMapTimer.Update(delegate { NotifyMapChunks(k.Key, 1); });
            NotifyInventory(k.Key);
            _playerStatusService.NotifyPlayerStats(k.Key);
        }

        //Process Mod timers
        foreach (ServerTimerRegistration t in Timers)
            t.Timer.Update(t.Callback);

        //Reset data displayed in /stat
        if ((DateTime.UtcNow - statsupdate).TotalSeconds >= 2)
        {
            statsupdate = DateTime.UtcNow;
            _serverPacketService.StatTotalPackets = 0;
            _serverPacketService.StatTotalPacketsLength = 0;
        }

        //Determine how long it took all operations to finish
        lastServerTick = (Stopwatch.GetTimestamp() - tickStart) * 1000 / Stopwatch.Frequency;
        if (lastServerTick > 500)
        {
            //Print an error if the value gets too big - TODO: Adjust
            _gameLogger.Server.Debug("Server process takes too long! Overloaded? ({0}ms)", lastServerTick);
        }
    }

    public void BroadcastSeason()
    {
        _gameLogger.Server.Debug($"{_serverClientService.Clients.Count} clients notified Season");
        foreach (KeyValuePair<int, ServerPlayer> c in _serverClientService.Clients)
            NotifySeason(c.Key);
    }

    /// <summary>
    /// Tell the clients the time
    /// </summary>
    private void NotifySeason(int clientid)
    {
        if (_serverClientService.Clients[clientid].State == ClientStateOnServer.Connecting)
        {
            return;
        }

        Packet_ServerSeason p = new()
        {
            Hour = _gameTimer.GetQuarterHourPartOfDay(),

            //DayNightCycleSpeedup is used by the client like this:
            //day_length_in_seconds = SecondsInADay / packet.Season.DayNightCycleSpeedup;

            //Set it to 1 if we froze the time, to prevent a division by zero
            DayNightCycleSpeedup = (_gameTimer.SpeedOfTime != 0) ? _gameTimer.SpeedOfTime : 1,
            Moon = 0,
        };
        _serverPacketService.SendPacket(clientid, Serialize(new Packet_Server() { Id = Packet_ServerIdEnum.Season, Season = p }));
    }

    public async void OnConfigLoaded()
    {
        //Initialize server map
        _serverMapStorage.Heightmap = new ChunkedMap2d<ushort>(_serverMapStorage.MapSizeX, _serverMapStorage.MapSizeY);
        _serverMapStorage.Reset(_config.MapSizeX, _config.MapSizeY, _config.MapSizeZ);

        await _assetManager.LoadAssetsAsync();

        //Initialize game components
        _craftingTableTool = new CraftingTableTool() { d_Map = _serverMapStorage, d_Data = _blockRegistry };
        _dataItems = new GameDataItemsBlocks() { d_Data = _blockRegistry };
        if (MainSockets.Length != 0 && MainSockets.All(x => x == null))
        {
            _gameLogger.Server.Debug($"main socket is empty so initalized by enet");
            MainSockets = new NetServer[3];
            MainSockets[0] = new EnetNetServer(new NetworkService(_gameLogger));
        }

        AllPrivileges.AddRange(Privilege.All());

        //Load the savegame file
        if (!Directory.Exists(GameStorePath.gamepathsaves))
        {
            Directory.CreateDirectory(GameStorePath.gamepathsaves);
        }

        _gameLogger.Server.Information(_languageService.ServerLoadingSavegame());

        _gameLogger.Server.Debug($"socket starting on port {_sessionConfig.Port}");

        Start(_sessionConfig.Port);

        _gameLogger.Server.Debug($"socket started on port {_sessionConfig.Port}");

        _saveGameService.Load();

        // server monitor
        if (_config.ServerMonitor)
        {
            serverMonitor = new ServerMonitor(this, _languageService, _serverClientService, _serverPacketService);
            serverMonitor.Start();
        }

        // set up server console interpreter
        MeinKraft.Group serverGroup = new()
        {
            Name = "Server",
            Level = 255,
            GroupPrivileges = []
        };
        serverGroup.GroupPrivileges = AllPrivileges;
        serverGroup.GroupColor = ClientColor.Red;
        _serverClientService.ServerConsoleClient.AssignGroup(serverGroup);

        if (_config.AutoRestartCycle > 0)
        {
            _gameLogger.Server.Debug("AutoRestartInterval: {0}", _config.AutoRestartCycle);
        }
        else
        {
            _gameLogger.Server.Debug("AutoRestartInterval: DISABLED");
        }
    }

    private void Start(int port)
    {
        Port = port;
        MainSockets[0].SetPort(port);
        MainSockets[0].Start();
        if (MainSockets[1] != null)
        {
            MainSockets[1].SetPort(port);
            MainSockets[1].Start();
        }

        if (MainSockets[2] != null)
        {
            MainSockets[2].SetPort(port + 2);
            MainSockets[2].Start();
        }
    }

    public int Port { get; set; }
    public void Stop()
    {
        _gameLogger.Server.Debug("[SERVER] Doing last tick...");
        ProcessMain();
        //Maybe inform mods about shutdown?
        _gameLogger.Server.Debug("[SERVER] Saving data...");
        DateTime start = DateTime.UtcNow;
        _saveGameService.SaveGlobalData();
        _gameLogger.Server.Information(_languageService.ServerGameSaved(), DateTime.UtcNow - start);
        _gameLogger.Server.Information("[SERVER] Stopped the server!");
    }

    public void Restart()
    {
        //Server shall exit and be restarted
    }

    public void Exit()
    {
        //Server shall be shutdown
    }

    private ServerMonitor serverMonitor;

    public List<string> AllPrivileges { get; set; } = [];
    public string GameMode { get; set; } = "Fortress";

    public void ReceiveServerConsole(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        if (message.StartsWith('/'))
        {
            int spaceIndex = message.IndexOf(' ');
            string command = spaceIndex < 0 ? message[1..] : message[1..spaceIndex];
            string argument = spaceIndex < 0 ? "" : message[(spaceIndex + 1)..];
            CommandInterpreter(GameConstants.ServerConsoleId, command, argument);
            return;
        }

        if (message.StartsWith('.'))
        {
            // client commands not handled server-side
            return;
        }

        // chat message
        SendMessageToAll(string.Format("{0}: {1}", _serverClientService.ServerConsoleClient.ColoredPlayername(colorNormal), message));
        _gameLogger.Server.Debug($"{_serverClientService.ServerConsoleClient.PlayerName}: {message}");
    }

    public List<Action> OnLoad { get; set; } = [];
    public List<Action> OnSave { get; set; } = [];
    public Dictionary<string, Inventory> Inventory { get; set; } = new(StringComparer.InvariantCultureIgnoreCase);

    public void Dispose()
    {
        if (!disposed)
        {
            //d_MainSocket.Disconnect(false);
        }

        disposed = true;
    }

    private bool disposed = false;

    private float lastServerTick;
    private int lastClientId;

    private void ProcessNetMessage(NetIncomingMessage msg, NetServer mainSocket)
    {
        if (msg.SenderConnection == null)
        {
            return;
        }

        int clientid = -1;
        foreach (KeyValuePair<int, ServerPlayer> k in _serverClientService.Clients)
        {
            if (k.Value.MainSocket != mainSocket)
            {
                continue;
            }

            if (k.Value.Socket.EqualsConnection(msg.SenderConnection))
            {
                clientid = k.Key;
            }
        }

        switch (msg.Type)
        {
            case NetworkMessageType.Connect:
                //new connection
                NetConnection client1 = msg.SenderConnection;

                ServerPlayer c = new()
                {
                    MainSocket = mainSocket,
                    Socket = client1
                };

                c.Ping.Timeout = TimeSpan.FromSeconds(_config?.ClientConnectionTimeout ?? 30);
                c.chunksseen = new bool[_serverMapStorage.MapSizeX / GameConstants.ServerChunkSize * _serverMapStorage.MapSizeY
                    / GameConstants.ServerChunkSize * _serverMapStorage.MapSizeZ / GameConstants.ServerChunkSize];
                lock (_serverClientService.Clients)
                {
                    lastClientId = _serverClientService.GenerateClientId();
                    c.Id = lastClientId;
                    _serverClientService.Clients[lastClientId] = c;
                    _gameLogger.Server.Debug("Client added.");

                }
                //clientid = c.Id;
                c.NotifyMapTimer = new ServerTimer
                {
                    Interval = TimeSpan.FromSeconds(1.0 / SEND_CHUNKS_PER_SECOND),
                };
                c.NotifyMonstersTimer = new ServerTimer
                {
                    Interval = TimeSpan.FromSeconds(1.0 / SEND_MONSTER_UDAPTES_PER_SECOND),
                };

                break;
            case NetworkMessageType.Data:
                if (clientid == -1)
                {
                    break;
                }
                // process packet
                TotalReceivedBytes += msg.Payload.Length;
                TryReadPacket(clientid, msg.Payload.ToArray());
                break;
            case NetworkMessageType.Disconnect:
                _gameLogger.Server.Debug("Client disconnected.");
                KillPlayer(clientid);
                break;
        }
    }

    private DateTime statsupdate;

    public List<ServerTimerRegistration> Timers { get; } = [];

    private void NotifyPing(int targetClientId, int ping)
    {
        foreach (KeyValuePair<int, ServerPlayer> k in _serverClientService.Clients)
        {
            SendPlayerPing(k.Key, targetClientId, ping);
        }
    }

    private void SendPlayerPing(int recipientClientId, int targetClientId, int ping)
    {
        Packet_ServerPlayerPing p = new()
        {
            ClientId = targetClientId,
            Ping = ping
        };
        _serverPacketService.SendPacket(recipientClientId, Serialize(new Packet_Server() { Id = Packet_ServerIdEnum.PlayerPing, PlayerPing = p }));
    }

    private const int SEND_CHUNKS_PER_SECOND = 10;
    private const int SEND_MONSTER_UDAPTES_PER_SECOND = 3;



    public void NotifyInventory(int clientid)
    {
        ServerPlayer c = _serverClientService.Clients[clientid];
        if (c.IsInventoryDirty && c.PlayerName != GameConstants.InvalidPlayerName && !c.UsingFill)
        {
            Packet_ServerInventory p = ConvertInventory(GetPlayerInventory(c.PlayerName));
            _serverPacketService.SendPacket(clientid, Serialize(new Packet_Server() { Id = Packet_ServerIdEnum.FiniteInventory, Inventory = p }));
            c.IsInventoryDirty = false;
        }
    }

    private static Packet_ServerInventory ConvertInventory(Inventory inv)
    {
        if (inv == null)
        {
            return null;
        }

        Packet_ServerInventory p = new();
        if (inv != null)
        {
            p.Inventory = new Packet_Inventory
            {
                Boots = inv.Boots,
                DragDropItem = inv.DragDropItem,
                Gauntlet = inv.Gauntlet,
                Helmet = inv.Helmet,
                // todo
                //p.Inventory.Items = inv.Inventory.Items;
                Items = new Packet_PositionItem[inv.Items.Count],
            };
            {
                int i = 0;
                foreach (KeyValuePair<GridPoint, InventoryItem> k in inv.Items)
                {
                    Packet_PositionItem item = new()
                    {
                        Key_ = $"{k.Key.X} {k.Key.Y}",
                        Value_ = k.Value,
                        X = k.Key.X,
                        Y = k.Key.Y
                    };
                    p.Inventory.Items[i++] = item;
                }
            }

            p.Inventory.MainArmor = inv.MainArmor;
            p.Inventory.RightHand = new InventoryItem[10];
            for (int i = 0; i < inv.RightHand.Length; i++)
            {
                if (inv.RightHand[i] == null)
                {
                    p.Inventory.RightHand[i] = new InventoryItem();
                }
                else
                {
                    p.Inventory.RightHand[i] = inv.RightHand[i];
                }
            }
        }

        return p;
    }

    private void HitMonsters(int clientid, int health)
    {
        ServerPlayer c = _serverClientService.Clients[clientid];
        int mapx = c.PositionMul32GlX / 32;
        int mapy = c.PositionMul32GlZ / 32;
        int mapz = c.PositionMul32GlY / 32;
        //3x3x3 chunks
        for (int xx = -1; xx < 2; xx++)
        {
            for (int yy = -1; yy < 2; yy++)
            {
                for (int zz = -1; zz < 2; zz++)
                {
                    int cx = (mapx / GameConstants.ServerChunkSize) + xx;
                    int cy = (mapy / GameConstants.ServerChunkSize) + yy;
                    int cz = (mapz / GameConstants.ServerChunkSize) + zz;
                    if (!VectorUtils.IsValidChunkPos(_serverMapStorage, cx, cy, cz, GameConstants.ServerChunkSize))
                    {
                        continue;
                    }

                    ServerChunk chunk = _serverMapStorage.GetChunkValid(cx, cy, cz);
                    if (chunk == null || chunk.Monsters == null)
                    {
                        continue;
                    }

                    foreach (Monster m in chunk.Monsters)
                    {
                        Vector3i mpos = new() { X = m.X, Y = m.Y, Z = m.Z };
                        Vector3i ppos = new()
                        {
                            X = _serverClientService.Clients[clientid].PositionMul32GlX / 32,
                            Y = _serverClientService.Clients[clientid].PositionMul32GlZ / 32,
                            Z = _serverClientService.Clients[clientid].PositionMul32GlY / 32
                        };
                        if (VectorUtils.DistanceSquared(mpos, ppos) < 15)
                        {
                            m.Health -= health;
                            //DiagLog.Write("HIT! -2 = " + m.Health);
                            if (m.Health <= 0)
                            {
                                chunk.Monsters.Remove(m);
                                SendSound(clientid, "death.wav", m.X, m.Y, m.Z);
                                break;
                            }

                            SendSound(clientid, "grunt2.wav", m.X, m.Y, m.Z);
                            break;
                        }
                    }
                }
            }
        }
    }

    public Inventory GetPlayerInventory(string playername)
    {
        Inventory ??= new Dictionary<string, Inventory>(StringComparer.InvariantCultureIgnoreCase);
        if (!Inventory.TryGetValue(playername, out Inventory? value))
        {
            value = StartInventory();
            Inventory[playername] = value;
        }

        return value;
    }

    public void ResetPlayerInventory(ServerPlayer client)
    {
        Inventory ??= new Dictionary<string, Inventory>(StringComparer.InvariantCultureIgnoreCase);
        Inventory[client.PlayerName] = StartInventory();
        client.IsInventoryDirty = true;
        NotifyInventory(client.Id);
    }

    private Inventory StartInventory()
    {
        Inventory inv = MeinKraft.Inventory.Create();
        int x = 0;
        int y = 0;
        InventoryUtil util = GetInventoryUtil(inv);

        foreach ((int id, BlockType? blockType) in _blockRegistry.BlockTypes)
        {
            _blockRegistry.StartInventoryAmount.TryGetValue(id, out int amount);

            bool shouldAdd = _config.IsCreative
                ? amount > 0 || blockType.IsBuildable
                : amount > 0;

            if (!shouldAdd)
            {
                continue;
            }

            inv.Items.Add(new GridPoint(x, y), new InventoryItem
            {
                InventoryItemType = InventoryItemType.Block,
                BlockId = id,
                BlockCount = _config.IsCreative ? 0 : amount
            });

            x++;
            if (x >= util.CellCountX)
            {
                x = 0;
                y++;
            }
        }

        return inv;
    }

    public Vector3i PlayerBlockPosition(ServerPlayer c) => new(c.PositionMul32GlX / 32, c.PositionMul32GlZ / 32, c.PositionMul32GlY / 32);

    public void KillPlayer(int clientid)
    {
        if (!_serverClientService.Clients.TryGetValue(clientid, out ServerPlayer? value))
        {
            return;
        }

        if (value.QueryClient)
        {
            _serverClientService.Clients.Remove(clientid);
            serverMonitor.RemoveMonitorClient(clientid);
            return;
        }

        _modEvents.RaisePlayerLeave(clientid);
        _modEvents.RaisePlayerDisconnect(clientid);

        string coloredName = _serverClientService.Clients[clientid].ColoredPlayername(colorNormal);
        string name = _serverClientService.Clients[clientid].PlayerName;
        _serverClientService.Clients.Remove(clientid);
        if (_config.ServerMonitor)
        {
            serverMonitor.RemoveMonitorClient(clientid);
        }

        foreach (KeyValuePair<int, ServerPlayer> k in _serverClientService.Clients)
        {
            _serverPacketService.SendPacket(k.Key, ServerPackets.EntityDespawn(clientid));
        }

        if (name != "invalid")
        {
            SendMessageToAll(string.Format(_languageService.ServerPlayerDisconnect(), coloredName));
            _gameLogger.Server.Information(string.Format("{0} disconnects.", name));
        }
    }

    public string ReceivedKey { get; set; }
    private DateTime lastQuery = DateTime.UtcNow;
    private int pistolcycle;

    private static readonly Stopwatch _uptime = Stopwatch.StartNew();
    public int TimeMillisecondsFromStart => (int)_uptime.ElapsedMilliseconds;

    private void TryReadPacket(int clientid, byte[] data)
    {
        ServerPlayer c = _serverClientService.Clients[clientid];
        Packet_Client packet = MemoryPackSerializer.Deserialize<Packet_Client>(data.AsSpan(0, data.Length));
        _gameLogger.Server.Debug($"Received packet {packet.Id} from client {clientid} (QueryClient={c.QueryClient})");

        if (c.QueryClient)
        {
            if (packet.Id is not (
                PacketType.ServerQuery or
                PacketType.PlayerIdentification or
                PacketType.PingReply))
            {
                _gameLogger.Server.Debug($"Rejected packet {packet.Id} from unauthenticated client {clientid}");
                _serverPacketService.SendPacket(clientid, ServerPackets.DisconnectPlayer("Either send PlayerIdentification or ServerQuery!"));
                KillPlayer(clientid);
                return;
            }

            if (packet.Id == PacketType.PingReply)
                return;
        }

        int realPlayers = 0;
        switch (packet.Id)
        {
            case PacketType.PingReply:
                _serverClientService.Clients[clientid].Ping.Receive(TimeMillisecondsFromStart);
                _serverClientService.Clients[clientid].LastPing = (float)_serverClientService.Clients[clientid].Ping.RoundtripMilliseconds / 1000;
                NotifyPing(clientid, _serverClientService.Clients[clientid].Ping.RoundtripMilliseconds);
                break;
            case PacketType.PlayerIdentification:
                {
                    foreach (KeyValuePair<int, ServerPlayer> cl in _serverClientService.Clients)
                    {
                        if (cl.Value.IsBot)
                        {
                            continue;
                        }

                        realPlayers++;
                    }

                    if (realPlayers > _config.MaxClients)
                    {
                        _serverPacketService.SendPacket(clientid, ServerPackets.DisconnectPlayer(_languageService.ServerTooManyPlayers()));
                        KillPlayer(clientid);
                        break;
                    }

                    if (_config.IsPasswordProtected() && packet.Identification.ServerPassword != _config.Password)
                    {
                        _gameLogger.Server.Information(string.Format("{0} fails to join (invalid server password).", packet.Identification.Username));
                        _gameLogger.Server.Information(string.Format("{0} fails to join (invalid server password).", packet.Identification.Username));
                        _serverPacketService.SendPacket(clientid, ServerPackets.DisconnectPlayer(_languageService.ServerPasswordInvalid()));
                        KillPlayer(clientid);
                        break;
                    }

                    SendServerIdentification(clientid);
                    string username = packet.Identification.Username;

                    // allowed characters in username: a-z,A-Z,0-9,-,_ length: 1-16
                    Regex allowedUsername = new(@"^(\w|-){1,16}$");

                    if (string.IsNullOrEmpty(username) || !allowedUsername.IsMatch(username))
                    {
                        _serverPacketService.SendPacket(clientid, ServerPackets.DisconnectPlayer(_languageService.ServerUsernameInvalid()));
                        _gameLogger.Server.Information(string.Format("{0} can't join (invalid username: {1}).", c.Socket.RemoteEndPoint().AddressToString(), username));
                        KillPlayer(clientid);
                        break;
                    }

                    bool isClientLocalhost = c.Socket.RemoteEndPoint().AddressToString() == "127.0.0.1";
                    bool verificationFailed = false;

                    if ((ComputeMd5(_config.Key.Replace("-", "") + username) != packet.Identification.VerificationKey)
                        && (!isClientLocalhost))
                    {
                        //Account verification failed.
                        username = $"~{username}";
                        verificationFailed = true;
                    }

                    if (!_config.AllowGuests && verificationFailed)
                    {
                        _serverPacketService.SendPacket(clientid, ServerPackets.DisconnectPlayer(_languageService.ServerNoGuests()));
                        KillPlayer(clientid);
                        break;
                    }

                    //When a duplicate user connects, append a number to name.
                    foreach (KeyValuePair<int, ServerPlayer> k in _serverClientService.Clients)
                    {
                        if (k.Value.PlayerName.Equals(username, StringComparison.InvariantCultureIgnoreCase))
                        {
                            // If duplicate is a registered user, kick duplicate. It is likely that the user lost connection before.
                            if (!verificationFailed && !isClientLocalhost)
                            {
                                KillPlayer(k.Key);
                                break;
                            }

                            // Duplicates are handled as guests.
                            username = GenerateUsername(username);
                            if (!username.StartsWith('~'))
                            {
                                username = $"~{username}";
                            }

                            break;
                        }
                    }

                    _serverClientService.Clients[clientid].PlayerName = username;

                    // Assign group to new client
                    //Check if client is in ServerClient.txt and assign corresponding group.
                    bool exists = false;
                    foreach (Client client in _serverClientService.ServerClient.Clients)
                    {
                        if (client.Name.Equals(username, StringComparison.InvariantCultureIgnoreCase))
                        {
                            foreach (MeinKraft.Group clientGroup in _serverClientService.ServerClient.Groups)
                            {
                                if (clientGroup.Name.Equals(client.Group))
                                {
                                    exists = true;
                                    _serverClientService.Clients[clientid].AssignGroup(clientGroup);
                                    break;
                                }
                            }

                            break;
                        }
                    }

                    if (!exists)
                    {
                        //Assign admin group if client connected from localhost
                        if (isClientLocalhost)
                        {
                            _serverClientService.Clients[clientid].AssignGroup(_serverClientService.ServerClient.Groups.Find(v => v.Name == "Admin"));
                        }
                        else if (_serverClientService.Clients[clientid].PlayerName.StartsWith("~"))
                        {
                            _serverClientService.Clients[clientid].AssignGroup(DefaultGroupGuest);
                        }
                        else
                        {
                            _serverClientService.Clients[clientid].AssignGroup(DefaultGroupRegistered);
                        }
                    }

                    SetFillAreaLimit(clientid);
                    SendFreemoveState(clientid, _serverClientService.Clients[clientid].Privileges.Contains(Privilege.freemove));
                    c.QueryClient = false;
                    _gameLogger.Server.Debug($"Client {clientid} authenticated successfully in TryReadPacket() case: PacketType.PlayerIdentification");
                    _serverClientService.Clients[clientid].Entity.DrawName.Name = username;
                    if (_config.EnablePlayerPushing)
                    {
                        // Player pushing
                        _serverClientService.Clients[clientid].Entity.Push = new ServerEntityPush
                        {
                            Range = 1
                        };
                    }

                    PlayerEntitySetDirty(clientid);
                }

                break;
            case PacketType.RequestBlob:
                {
                    // Set player's spawn position
                    Vector3i position = GetPlayerSpawnPositionMul32(clientid);

                    _serverClientService.Clients[clientid].PositionMul32GlX = position.X;
                    _serverClientService.Clients[clientid].PositionMul32GlY = position.Y + (int)(0.5 * 32);
                    _serverClientService.Clients[clientid].PositionMul32GlZ = position.Z;

                    string ip = _serverClientService.Clients[clientid].Socket.RemoteEndPoint().AddressToString();
                    SendMessageToAll(string.Format(_languageService.ServerPlayerJoin(), _serverClientService.Clients[clientid].ColoredPlayername(colorNormal)));
                    _gameLogger.Server.Information(string.Format("{0} {1} joins.", _serverClientService.Clients[clientid].PlayerName, ip));
                    _serverPacketService.SendMessage(clientid, colorSuccess + _config.WelcomeMessage);
                    SendBlobs(clientid, packet.RequestBlob.RequestedMd5);
                    SendBlockTypes(clientid);
                    SendTranslations(clientid);
                    SendSunLevels(clientid);
                    SendLightLevels(clientid);
                    SendCraftingRecipes(clientid);

                    _modEvents.RaisePlayerJoin(clientid);

                    _serverPacketService.SendPacket(clientid, ServerPackets.LevelFinalize());
                    _serverClientService.Clients[clientid].State = ClientStateOnServer.Playing;
                    NotifySeason(clientid);
                }

                break;
            case PacketType.SetBlock:
                {
                    int x = packet.SetBlock.X;
                    int y = packet.SetBlock.Y;
                    int z = packet.SetBlock.Z;
                    if (packet.SetBlock.Mode == PacketBlockSetMode.Use)	//Check if player only uses block
                    {
                        if (!CheckUsePrivileges(clientid, x, y, z))
                        {
                            break;
                        }

                        DoCommandBuild(clientid, true, packet.SetBlock);
                    }
                    else	//Player builds, deletes or uses block with tool
                    {
                        if (!CheckBuildPrivileges(clientid, x, y, z, packet.SetBlock.Mode))
                        {
                            SendSetBlock(clientid, x, y, z, _serverMapStorage.GetBlock(x, y, z)); //revert
                            break;
                        }

                        if (!DoCommandBuild(clientid, true, packet.SetBlock))
                        {
                            SendSetBlock(clientid, x, y, z, _serverMapStorage.GetBlock(x, y, z)); //revert
                        }
                        //Only log when building/destroying blocks. Prevents VandalFinder entries
                        if (packet.SetBlock.Mode != PacketBlockSetMode.UseWithTool)
                        {
                            _gameLogger.Server.Debug($"{x} {y} {z} {c.PlayerName} {c.Socket.RemoteEndPoint().AddressToString()} {_serverMapStorage.GetBlock(x, y, z)}");
                        }
                    }
                }

                break;
            case PacketType.FillArea:
                {
                    if (!_serverClientService.Clients[clientid].Privileges.Contains(Privilege.build))
                    {
                        _serverPacketService.SendMessage(clientid, colorError + _languageService.ServerNoBuildPrivilege());
                        break;
                    }

                    if (_serverClientService.Clients[clientid].IsSpectator && !_config.AllowSpectatorBuild)
                    {
                        _serverPacketService.SendMessage(clientid, colorError + _languageService.ServerNoSpectatorBuild());
                        break;
                    }

                    Vector3i a = new(packet.FillArea.X1, packet.FillArea.Y1, packet.FillArea.Z1);
                    Vector3i b = new(packet.FillArea.X2, packet.FillArea.Y2, packet.FillArea.Z2);

                    int blockCount = (Math.Abs(a.X - b.X) + 1) * (Math.Abs(a.Y - b.Y) + 1) * (Math.Abs(a.Z - b.Z) + 1);

                    if (blockCount > _serverClientService.Clients[clientid].FillLimit)
                    {
                        _serverPacketService.SendMessage(clientid, colorError + _languageService.ServerFillAreaTooLarge());
                        break;
                    }

                    if (!IsFillAreaValid(_serverClientService.Clients[clientid], a, b))
                    {
                        _serverPacketService.SendMessage(clientid, colorError + _languageService.ServerFillAreaInvalid());
                        break;
                    }

                    DoFillArea(clientid, packet.FillArea, blockCount);
                    _gameLogger.Server.Debug($"{a.X} {a.Y} {a.Z} - {b.X} {b.Y} {b.Z} {c.PlayerName} {c.Socket.RemoteEndPoint().AddressToString()} {_serverMapStorage.GetBlock(a.X, a.Y, a.Z)}");
                }

                break;
            case PacketType.PositionAndOrientation:
                {
                    Packet_ClientPositionAndOrientation p = packet.PositionAndOrientation;
                    _serverClientService.Clients[clientid].PositionMul32GlX = p.X;
                    _serverClientService.Clients[clientid].PositionMul32GlY = p.Y;
                    _serverClientService.Clients[clientid].PositionMul32GlZ = p.Z;
                    _serverClientService.Clients[clientid].PositionHeading = p.Heading;
                    _serverClientService.Clients[clientid].PositionPitch = p.Pitch;
                    _serverClientService.Clients[clientid].Stance = (byte)p.Stance;
                }

                break;
            case PacketType.Message:
                {
                    packet.Message.Message = packet.Message.Message.Trim();
                    // empty message
                    if (string.IsNullOrEmpty(packet.Message.Message))
                    {
                        //Ignore empty messages
                        break;
                    }
                    // server command
                    if (packet.Message.Message.StartsWith("/"))
                    {
                        string[] ss = packet.Message.Message.Split([' ']);
                        string command = ss[0].Replace("/", "");
                        string argument = packet.Message.Message.IndexOf(' ') < 0 ? "" : packet.Message.Message.Substring(packet.Message.Message.IndexOf(" ") + 1);
                        try
                        {
                            //Try to execute the given command
                            CommandInterpreter(clientid, command, argument);
                        }
                        catch (Exception ex)
                        {
                            //This will notify client of error instead of kicking him in case of an error
                            _serverPacketService.SendMessage(clientid, "Server error while executing command!", MessageType.Error);
                            _serverPacketService.SendMessage(clientid, "Details on server console!", MessageType.Error);
                            _gameLogger.Server.Error("Client {0} caused a command error.", clientid);
                            _gameLogger.Server.Error("Command: /{0}", command);
                            _gameLogger.Server.Error("Arguments: {0}", argument);
                            _gameLogger.Server.Error(ex.Message);
                            _gameLogger.Server.Error(ex.StackTrace);
                        }
                    }
                    // client command
                    else if (packet.Message.Message.StartsWith("."))
                    {
                        //Ignore clientside commands
                        break;
                    }
                    // chat message
                    else
                    {
                        string message = packet.Message.Message;
                        message = _modEvents.RaisePlayerChat(clientid, message, packet.Message.IsTeamchat != 0) ?? message;

                        if (_serverClientService.Clients[clientid].Privileges.Contains(Privilege.chat))
                        {
                            if (message == null)
                            {
                                break;
                            }

                            SendMessageToAll(string.Format("{0}: {1}", _serverClientService.Clients[clientid].ColoredPlayername(colorNormal), message));
                            _gameLogger.Server.Debug($"{_serverClientService.Clients[clientid].PlayerName}: {message}");

                        }
                        else
                        {
                            _serverPacketService.SendMessage(clientid, string.Format(_languageService.ServerNoChatPrivilege(), colorError));
                        }
                    }
                }

                break;
            case PacketType.Craft:
                DoCommandCraft(true, packet.Craft);
                break;
            case PacketType.InventoryAction:
                DoCommandInventory(clientid, packet.InventoryAction);
                break;
            case PacketType.Health:
                {
                    //todo server side
                    PacketServerPlayerStats stats = _playerStatusService.GetPlayerStats(_serverClientService.Clients[clientid].PlayerName);
                    stats.CurrentHealth = packet.Health.CurrentHealth;
                    if (stats.CurrentHealth < 1)
                    {
                        //death - reset health. More stuff done in Death packet handling
                        stats.CurrentHealth = stats.MaxHealth;
                    }

                    _serverClientService.Clients[clientid].IsPlayerStatsDirty = true;
                }

                break;
            case PacketType.Death:
                {
                    //DiagLog.Write("Death Packet Received. Client: {0}, Reason: {1}, Source: {2}", clientid, packet.Death.Reason, packet.Death.SourcePlayer);
                    _modEvents.RaisePlayerDeath(clientid, packet.Death.Reason, packet.Death.SourcePlayer);
                }

                break;
            case PacketType.Oxygen:
                {
                    //todo server side
                    PacketServerPlayerStats stats = _playerStatusService.GetPlayerStats(_serverClientService.Clients[clientid].PlayerName);
                    stats.CurrentOxygen = packet.Oxygen.CurrentOxygen;
                    _serverClientService.Clients[clientid].IsPlayerStatsDirty = true;
                }

                break;
            case PacketType.MonsterHit:
                HitMonsters(clientid, packet.Health.CurrentHealth);
                break;
            case PacketType.DialogClick:
                _modEvents.RaiseDialogClick(clientid, packet.DialogClick_.WidgetId);

                _modEvents.RaiseDialogClick2(new DialogClick2Args
                {
                    Player = clientid,
                    WidgetId = packet.DialogClick_.WidgetId,
                    TextBoxValue = packet.DialogClick_.TextBoxValue
                });

                break;
            case PacketType.Shot:
                int shootSoundIndex = pistolcycle++ % _blockRegistry.BlockTypes[packet.Shot.WeaponBlock].Sounds.ShootEnd.Length;	//Cycle all given ShootEnd sounds
                PlaySoundAtExceptPlayer((int)DeserializeFloat(packet.Shot.FromX), (int)DeserializeFloat(packet.Shot.FromZ), (int)DeserializeFloat(packet.Shot.FromY),
                    _blockRegistry.BlockTypes[packet.Shot.WeaponBlock].Sounds.ShootEnd[shootSoundIndex] + ".ogg", clientid);
                if (_blockRegistry.BlockTypes[packet.Shot.WeaponBlock].ProjectileSpeed == 0)
                {
                    SendBullet(clientid, DeserializeFloat(packet.Shot.FromX), DeserializeFloat(packet.Shot.FromY), DeserializeFloat(packet.Shot.FromZ),
                       DeserializeFloat(packet.Shot.ToX), DeserializeFloat(packet.Shot.ToY), DeserializeFloat(packet.Shot.ToZ), 150);
                }
                else
                {
                    Vector3 from = new(DeserializeFloat(packet.Shot.FromX), DeserializeFloat(packet.Shot.FromY), DeserializeFloat(packet.Shot.FromZ));
                    Vector3 to = new(DeserializeFloat(packet.Shot.ToX), DeserializeFloat(packet.Shot.ToY), DeserializeFloat(packet.Shot.ToZ));
                    Vector3 v = to - from;
                    v.Normalize();
                    v *= _blockRegistry.BlockTypes[packet.Shot.WeaponBlock].ProjectileSpeed;
                    SendProjectile(clientid, DeserializeFloat(packet.Shot.FromX), DeserializeFloat(packet.Shot.FromY), DeserializeFloat(packet.Shot.FromZ),
                        v.X, v.Y, v.Z, packet.Shot.WeaponBlock, DeserializeFloat(packet.Shot.ExplodesAfter));
                    //Handle OnWeaponShot so grenade ammo is correct
                    _modEvents.RaiseWeaponShot(clientid, packet.Shot.WeaponBlock);

                    return;
                }

                _modEvents.RaiseWeaponShot(clientid, packet.Shot.WeaponBlock);

                if (_serverClientService.Clients[clientid].LastPing < 0.3)
                {
                    if (packet.Shot.HitPlayer != -1)
                    {
                        //client-side shooting
                        _modEvents.RaiseWeaponHit(clientid, packet.Shot.HitPlayer, packet.Shot.WeaponBlock, packet.Shot.IsHitHead != 0);
                    }

                    return;
                }

                foreach (KeyValuePair<int, ServerPlayer> k in _serverClientService.Clients)
                {
                    if (k.Key == clientid)
                    {
                        continue;
                    }

                    Line3D pick = new()
                    {
                        Start = new Vector3(DeserializeFloat(packet.Shot.FromX), DeserializeFloat(packet.Shot.FromY), DeserializeFloat(packet.Shot.FromZ)),
                        End = new Vector3(DeserializeFloat(packet.Shot.ToX), DeserializeFloat(packet.Shot.ToY), DeserializeFloat(packet.Shot.ToZ))
                    };

                    Vector3 feetpos = new((float)k.Value.PositionMul32GlX / 32, (float)k.Value.PositionMul32GlY / 32, (float)k.Value.PositionMul32GlZ / 32);
                    //var p = PlayerPositionSpawn;
                    float headsize = (k.Value.ModelHeight - k.Value.EyeHeight) * 2; //0.4f;
                    float h = k.Value.ModelHeight - headsize;
                    float r = 0.35f;

                    Box3 bodybox = new(
                        new Vector3(feetpos.X - r, feetpos.Y, feetpos.Z - r),
                        new Vector3(feetpos.X + r, feetpos.Y + h, feetpos.Z + r)
                    );

                    Box3 headbox = new(
                        new Vector3(feetpos.X - r, feetpos.Y + h, feetpos.Z - r),
                        new Vector3(feetpos.X + r, feetpos.Y + h + headsize, feetpos.Z + r)
                    );

                    if (Intersection.CheckLineBoxExact(pick, headbox) != null)
                    {
                        _modEvents.RaiseWeaponHit(clientid, k.Key, packet.Shot.WeaponBlock, true);
                    }
                    else if (Intersection.CheckLineBoxExact(pick, bodybox) != null)
                    {
                        _modEvents.RaiseWeaponHit(clientid, k.Key, packet.Shot.WeaponBlock, false);
                    }
                }

                break;
            case PacketType.SpecialKey:
                _modEvents.RaiseSpecialKey(clientid, packet.SpecialKey_.Key_);

                break;
            case PacketType.ActiveMaterialSlot:
                _serverClientService.Clients[clientid].ActiveMaterialSlot = packet.ActiveMaterialSlot.ActiveMaterialSlot;
                _modEvents.RaiseChangedActiveMaterialSlot(clientid);

                break;
            case PacketType.Leave:
                //0: Leave - 1: Crash
                _gameLogger.Server.Information("Disconnect reason: {0}", packet.Leave.Reason);
                KillPlayer(clientid);
                break;
            case PacketType.Reload:
                break;
            case PacketType.ServerQuery:
                //Flood/DDoS-abuse protection
                if ((DateTime.UtcNow - lastQuery) < TimeSpan.FromMilliseconds(200))
                {
                    _gameLogger.Server.Information("ServerQuery rejected (too many requests)");
                    _serverPacketService.SendPacket(clientid, ServerPackets.DisconnectPlayer("Too many requests!"));
                    KillPlayer(clientid);
                    return;
                }

                _gameLogger.Server.Information("ServerQuery processed.");
                lastQuery = DateTime.UtcNow;
                //Client only wants server information. No real client.
                List<string> playernames = [];
                lock (_serverClientService.Clients)
                {
                    foreach (KeyValuePair<int, ServerPlayer> k in _serverClientService.Clients)
                    {
                        if (k.Value.QueryClient || k.Value.IsBot)
                        {
                            //Exclude bot players and query clients
                            continue;
                        }

                        playernames.Add(k.Value.PlayerName);
                    }
                }
                //Create query answer
                Packet_ServerQueryAnswer answer = new()
                {
                    Name = _config.Name,
                    MOTD = _config.Motd,
                    PlayerCount = playernames.Count,
                    MaxPlayers = _config.MaxClients,
                    PlayerList = string.Join(",", playernames.ToArray()),
                    Port = _config.Port,
                    GameMode = GameMode,
                    Password = _config.IsPasswordProtected(),
                    PublicHash = ReceivedKey,
                    ServerVersion = GameVersion.Version,
                    MapSizeX = _serverMapStorage.MapSizeX,
                    MapSizeY = _serverMapStorage.MapSizeY,
                    MapSizeZ = _serverMapStorage.MapSizeZ,
                    ServerThumbnail = GenerateServerThumbnail(),
                };
                //Send answer
                _serverPacketService.SendPacket(clientid, ServerPackets.AnswerQuery(answer));
                //Directly disconnect client after request.
                _serverPacketService.SendPacket(clientid, ServerPackets.DisconnectPlayer("Query success."));
                KillPlayer(clientid);
                break;
            case PacketType.GameResolution:
                //Update client information
                _serverClientService.Clients[clientid].WindowSize = new int[] { packet.GameResolution.Width, packet.GameResolution.Height };
                //DiagLog.Write("client:{0} --> {1}x{2}", clientid, clients[clientid].WindowSize[0], clients[clientid].WindowSize[1]);
                break;
            case PacketType.EntityInteraction:
                switch (packet.EntityInteraction.InteractionType)
                {
                    case PacketEntityInteractionType.Use:
                        ServerEntityId useId = c.SpawnedEntities[packet.EntityInteraction.EntityId - 64];
                        _modEvents.RaiseUseEntity(clientid, useId.ChunkX, useId.ChunkY, useId.ChunkZ, useId.Id);
                        break;

                    case PacketEntityInteractionType.Hit:
                        ServerEntityId hitId = c.SpawnedEntities[packet.EntityInteraction.EntityId - 64];
                        _modEvents.RaiseHitEntity(clientid, hitId.ChunkX, hitId.ChunkY, hitId.ChunkZ, hitId.Id);
                        break;
                    default:
                        _gameLogger.Server.Warning("Unknown EntityInteractionType: {0}, clientid: {1}", packet.EntityInteraction.InteractionType, clientid);
                        break;
                }

                break;
            default:
                _gameLogger.Server.Warning("Invalid packet: {0}, clientid:{1}", packet.Id, clientid);
                break;
        }
    }

    public bool CheckBuildPrivileges(int player, int x, int y, int z, PacketBlockSetMode mode)
    {
        if (!PlayerHasPrivilege(player, Privilege.build))
        {
            _serverPacketService.SendMessage(player, colorError + _languageService.ServerNoBuildPrivilege());
            return false;
        }

        if (_serverClientService.Clients[player].IsSpectator && !_config.AllowSpectatorBuild)
        {
            _serverPacketService.SendMessage(player, colorError + _languageService.ServerNoSpectatorBuild());
            return false;
        }

        if (_modEvents.RaisePermission(new PermissionArgs { Player = player, X = x, Y = y, Z = z }))
        {
            return true;
        }

        if (!_config.CanUserBuild(_serverClientService.Clients[player], x, y, z)
            && !ExtraPrivileges.ContainsKey(Privilege.build))
        {
            _serverPacketService.SendMessage(player, colorError + _languageService.ServerNoBuildPermissionHere());
            return false;
        }

        bool retval = true;
        if (mode == PacketBlockSetMode.Create)
        {
            retval = retval && _modEvents.RaiseCheckBlockBuild(player, x, y, z);
        }
        else if (mode == PacketBlockSetMode.Destroy)
        {
            retval = retval && _modEvents.RaiseCheckBlockDelete(player, x, y, z);
        }

        return retval;
    }

    private bool CheckUsePrivileges(int player, int x, int y, int z)
    {
        if (!PlayerHasPrivilege(player, Privilege.use))
        {
            _serverPacketService.SendMessage(player, colorError + _languageService.ServerNoUsePrivilege());
            return false;
        }

        if (_serverClientService.Clients[player].IsSpectator && !_config.AllowSpectatorUse)
        {
            _serverPacketService.SendMessage(player, colorError + _languageService.ServerNoSpectatorUse());
            return false;
        }

        return _modEvents.RaiseCheckBlockUse(player, x, y, z);
    }

    public void SendServerRedirect(int clientid, string ip_, int port_)
    {
        Packet_Server p = new()
        {
            Id = Packet_ServerIdEnum.ServerRedirect,
            Redirect = new Packet_ServerRedirect()
            {
                IP = ip_,
                Port = port_,
            }
        };
        _serverPacketService.SendPacket(clientid, p);
    }

    private static byte[] GenerateServerThumbnail()
    {
        string filename = Path.Combine(Path.Combine("data", "public"), "thumbnail.png");
        Bitmap bmp;
        if (File.Exists(filename))
        {
            try
            {
                bmp = new Bitmap(filename);
            }
            catch
            {
                //Create empty bitmap in case of failure
                bmp = new Bitmap(64, 64);
            }
        }
        else
        {
            bmp = new Bitmap(64, 64);
        }

        Bitmap bmp2 = bmp;
        if (bmp.Width != 64 || bmp.Height != 64)
        {
            //Resize the image if it does not have the proper size
            bmp2 = new Bitmap(bmp, 64, 64);
        }

        using MemoryStream ms = new();
        //Convert image to a byte[] for transfer
        bmp2.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        return ms.ToArray();
    }

    private static float DeserializeFloat(int p) => (float)p / 32;

    private void SendProjectile(int player, float fromx, float fromy, float fromz, float velocityx, float velocityy, float velocityz, int block, float explodesafter)
    {
        foreach (KeyValuePair<int, ServerPlayer> k in _serverClientService.Clients)
        {
            if (k.Key == player)
            {
                continue;
            }

            Packet_Server p = new()
            {
                Id = Packet_ServerIdEnum.Projectile,
                Projectile = new Packet_ServerProjectile()
                {
                    FromXFloat = SerializeFloat(fromx),
                    FromYFloat = SerializeFloat(fromy),
                    FromZFloat = SerializeFloat(fromz),
                    VelocityXFloat = SerializeFloat(velocityx),
                    VelocityYFloat = SerializeFloat(velocityy),
                    VelocityZFloat = SerializeFloat(velocityz),
                    BlockId = block,
                    ExplodesAfterFloat = SerializeFloat(explodesafter),
                    SourcePlayerID = player,
                }
            };
            _serverPacketService.SendPacket(k.Key, Serialize(p));
        }
    }

    private void SendBullet(int player, float fromx, float fromy, float fromz, float tox, float toy, float toz, float speed)
    {
        foreach (KeyValuePair<int, ServerPlayer> k in _serverClientService.Clients)
        {
            if (k.Key == player)
            {
                continue;
            }

            Packet_Server p = new()
            {
                Id = Packet_ServerIdEnum.Bullet,
                Bullet = new Packet_ServerBullet()
                {
                    FromXFloat = SerializeFloat(fromx),
                    FromYFloat = SerializeFloat(fromy),
                    FromZFloat = SerializeFloat(fromz),
                    ToXFloat = SerializeFloat(tox),
                    ToYFloat = SerializeFloat(toy),
                    ToZFloat = SerializeFloat(toz),
                    SpeedFloat = SerializeFloat(speed)
                }
            };
            _serverPacketService.SendPacket(k.Key, Serialize(p));
        }
    }

    public Vector3i GetPlayerSpawnPositionMul32(int clientid)
    {
        Vector3i position;
        Spawn playerSpawn = null;
        // Check if there is a spawn entry for his assign group
        if (_serverClientService.Clients[clientid].ClientGroup.Spawn != null)
        {
            playerSpawn = _serverClientService.Clients[clientid].ClientGroup.Spawn;
        }
        // Check if there is an entry in clients with spawn member (overrides group spawn).
        foreach (Client client in _serverClientService.ServerClient.Clients)
        {
            if (client.Name.Equals(_serverClientService.Clients[clientid].PlayerName, StringComparison.InvariantCultureIgnoreCase))
            {
                if (client.Spawn != null)
                {
                    playerSpawn = client.Spawn;
                }

                break;
            }
        }

        if (playerSpawn == null)
        {
            position = new Vector3i(DefaultPlayerSpawn.X * 32, DefaultPlayerSpawn.Z * 32, DefaultPlayerSpawn.Y * 32);
        }
        else
        {
            position = SpawnToVector3i(playerSpawn);
        }

        return position;
    }

    private void RunInClientSandbox(string script, int clientid)
    {
        ServerPlayer client = _serverClientService.GetClient(clientid);
        if (!_config.AllowScripting)
        {
            _serverPacketService.SendMessage(clientid, "Server scripts disabled.", MessageType.Error);
            return;
        }

        if (!client.Privileges.Contains(Privilege.run))
        {
            _serverPacketService.SendMessage(clientid, "Insufficient privileges to access this command.", MessageType.Error);
            return;
        }

        _gameLogger.Server.Information(string.Format("{0} runs script:\n{1}", client.PlayerName, script));
        if (client.Interpreter == null)
        {
            client.Interpreter = new JavaScriptInterpreter();
            client.Console = new ScriptConsole(this, _blockRegistry, _saveGameService, _serverClientService, _serverPacketService, clientid);
            client.Console.InjectConsoleCommands(client.Interpreter);
            client.Interpreter.SetVariables(new Dictionary<string, object>() { { "client", client }, { "server", this }, });
            client.Interpreter.Execute("function inspect(obj) { for( property in obj) { out(property)}}");
        }

        IScriptInterpreter interpreter = client.Interpreter;
        object result;
        _serverPacketService.SendMessage(clientid, colorNormal + script);
        if (interpreter.Execute(script, out result))
        {
            try
            {
                _serverPacketService.SendMessage(clientid, $"{colorSuccess} => {result}");
            }
            catch (FormatException e) // can happen
            {
                _serverPacketService.SendMessage(clientid, $"{colorError}Error. {e.Message}");
            }

            return;
        }

        _serverPacketService.SendMessage(clientid, $"{colorError}Error.");
    }

    private void NotifyBlock(int x, int y, int z, int blocktype)
    {
        foreach (KeyValuePair<int, ServerPlayer> k in _serverClientService.Clients)
        {
            SendSetBlock(k.Key, x, y, z, blocktype);
        }
    }

    private bool DoCommandCraft(bool execute, Packet_ClientCraft cmd)
    {
        if (_serverMapStorage.GetBlock(cmd.X, cmd.Y, cmd.Z) != _blockRegistry.BlockIdCraftingTable)
        {
            return false;
        }

        if (cmd.RecipeId < 0 || cmd.RecipeId >= CraftingRecipes.Count)
        {
            return false;
        }

        Vector3i[] table = _craftingTableTool.GetTable(cmd.X, cmd.Y, cmd.Z, out int tableCount);
        int[] ontable = _craftingTableTool.GetOnTable(table, tableCount, out int ontableCount);
        List<int> outputtoadd = [];
        int i = cmd.RecipeId;
        {
            //try apply recipe. if success then try until fail.
            for (; ; )
            {
                //check if ingredients available
                foreach (Ingredient ingredient in CraftingRecipes[i].Ingredients)
                {
                    if (ontable.AsSpan(0, tableCount).Count(ingredient.Type) < ingredient.Amount)
                    {
                        goto nextrecipe;
                    }
                }
                //remove ingredients
                foreach (Ingredient ingredient in CraftingRecipes[i].Ingredients)
                {
                    for (int ii = 0; ii < ingredient.Amount; ii++)
                    {
                        //replace on table
                        ReplaceOne(ontable, ontableCount, ingredient.Type, _blockRegistry.BlockIdEmpty);
                    }
                }
                //add output
                for (int z = 0; z < CraftingRecipes[i].Output.Amount; z++)
                {
                    outputtoadd.Add(CraftingRecipes[i].Output.Type);
                }
            }

        nextrecipe:
            ;
        }

        foreach (var v in outputtoadd)
        {
            ReplaceOne(ontable, ontableCount, _blockRegistry.BlockIdEmpty, v);
        }

        int zz = 0;
        if (execute)
        {
            for (int k = 0; k < tableCount; k++)
            {
                Vector3i v = table[k];
                SetBlockAndNotify(v.X, v.Y, v.Z + 1, ontable[zz]);
                zz++;
            }
        }

        return true;
    }

    private static void ReplaceOne<T>(T[] l, int lCount, T from, T to) where T : IEquatable<T>
    {
        Span<T> span = l.AsSpan(0, lCount);
        int index = span.IndexOf(from);
        if (index >= 0)
        {
            span[index] = to;
        }
    }

    private IGameDataItems _dataItems;
    public InventoryUtil GetInventoryUtil(Inventory inventory)
    {
        return new()
        {
            d_Inventory = inventory,
            d_Items = _dataItems
        };
    }

    private void DoCommandInventory(int player_id, Packet_ClientInventoryAction cmd)
    {
        Inventory inventory = GetPlayerInventory(_serverClientService.Clients[player_id].PlayerName);
        InventoryServer s = new()
        {
            d_Inventory = inventory,
            d_InventoryUtil = GetInventoryUtil(inventory),
            d_Items = _dataItems,
            d_DropItem = this
        };

        switch (cmd.Action)
        {
            case PacketInventoryActionType.Click:
                s.InventoryClick(cmd.A);
                break;
            case PacketInventoryActionType.MoveToInventory:
                s.MoveToInventory(cmd.A);
                break;
            case PacketInventoryActionType.WearItem:
                s.WearItem(cmd.A, cmd.B);
                break;
            default:
                break;
        }

        _serverClientService.Clients[player_id].IsInventoryDirty = true;
        NotifyInventory(player_id);
    }

    private bool IsFillAreaValid(ServerPlayer client, Vector3i a, Vector3i b)
    {
        if (!VectorUtils.IsValidPos(_serverMapStorage, a.X, a.Y, a.Z) || !VectorUtils.IsValidPos(_serverMapStorage, b.X, b.Y, b.Z))
        {
            return false;
        }

        int minX = Math.Min(a.X, b.X), maxX = Math.Max(a.X, b.X);
        int minY = Math.Min(a.Y, b.Y), maxY = Math.Max(a.Y, b.Y);
        int minZ = Math.Min(a.Z, b.Z), maxZ = Math.Max(a.Z, b.Z);

        return _config.Areas.Any(area =>
            area.CanUserBuild(client) &&
            area.ContainsBox(minX, minY, minZ, maxX, maxY, maxZ));
    }

    private bool DoFillArea(int player_id, Packet_ClientFillArea fill, int blockCount)
    {
        Vector3i a = new(fill.X1, fill.Y1, fill.Z1);
        Vector3i b = new(fill.X2, fill.Y2, fill.Z2);

        int startx = Math.Min(a.X, b.X);
        int endx = Math.Max(a.X, b.X);
        int starty = Math.Min(a.Y, b.Y);
        int endy = Math.Max(a.Y, b.Y);
        int startz = Math.Min(a.Z, b.Z);
        int endz = Math.Max(a.Z, b.Z);

        int blockType = fill.BlockType;
        blockType = _blockRegistry.WhenPlayerPlacesGetsConvertedTo[blockType];

        Inventory inventory = GetPlayerInventory(_serverClientService.Clients[player_id].PlayerName);
        InventoryItem? item = inventory.RightHand[fill.MaterialSlot];
        if (item == null)
        {
            return false;
        }
        //This prevents the player's inventory from getting sent to them while using fill (causes excessive bandwith usage)
        _serverClientService.Clients[player_id].UsingFill = true;
        for (int x = startx; x <= endx; ++x)
        {
            for (int y = starty; y <= endy; ++y)
            {
                for (int z = startz; z <= endz; ++z)
                {
                    Packet_ClientSetBlock cmd = new()
                    {
                        X = x,
                        Y = y,
                        Z = z,
                        MaterialSlot = fill.MaterialSlot
                    };
                    if (GetBlock(x, y, z) != 0)
                    {
                        cmd.Mode = PacketBlockSetMode.Destroy;
                        DoCommandBuild(player_id, true, cmd);
                    }

                    if (blockType != _blockRegistry.BlockIdFillArea)
                    {
                        cmd.Mode = PacketBlockSetMode.Create;
                        DoCommandBuild(player_id, true, cmd);
                    }
                }
            }
        }

        _serverClientService.Clients[player_id].UsingFill = false;
        return true;
    }

    /// <summary>
    /// Determines if a given client can see the specified chunk<br/>
    /// <b>Attention!</b> Chunk coordinates are NOT world coordinates!<br/>
    /// chunk position = (world position / chunk size)
    /// </summary>
    /// <param name="clientid">Client ID</param>
    /// <param name="vx">Chunk x coordinate</param>
    /// <param name="vy">Chunk y coordinate</param>
    /// <param name="vz">Chunk z coordinate</param>
    /// <returns>true if client can see the chunk, false otherwise</returns>
    public bool ClientSeenChunk(int clientid, int vx, int vy, int vz)
    {
        int pos = VectorIndexUtil.Index3d(vx, vy, vz, _serverMapStorage.MapSizeX / GameConstants.ServerChunkSize, _serverMapStorage.MapSizeY / GameConstants.ServerChunkSize);
        return _serverClientService.Clients[clientid].chunksseen[pos];
    }

    /// <summary>
    /// Sets a given chunk as seen by the client<br/>
    /// <b>Attention!</b> Chunk coordinates are NOT world coordinates!<br/>
    /// chunk position = (world position / chunk size)
    /// </summary>
    /// <param name="clientid">Client ID</param>
    /// <param name="vx">Chunk x coordinate</param>
    /// <param name="vy">Chunk y coordinate</param>
    /// <param name="vz">Chunk z coordinate</param>
    /// <param name="time"></param>
    public void ClientSeenChunkSet(int clientid, int vx, int vy, int vz, int time)
    {
        int pos = VectorIndexUtil.Index3d(vx, vy, vz, _serverMapStorage.MapSizeX / GameConstants.ServerChunkSize, _serverMapStorage.MapSizeY / GameConstants.ServerChunkSize);
        _serverClientService.Clients[clientid].chunksseen[pos] = true;
        _serverClientService.Clients[clientid].chunksseenTime[pos] = time;
        //DiagLog.Write("SeenChunk:   {0},{1},{2} Client: {3}", vx, vy, vz, clientid);
    }

    /// <summary>
    /// Sets a given chunk as unseen by the client<br/>
    /// <b>Attention!</b> Chunk coordinates are NOT world coordinates!<br/>
    /// chunk position = (world position / chunk size)
    /// </summary>
    /// <param name="clientid">Client ID</param>
    /// <param name="vx">Chunk x coordinate</param>
    /// <param name="vy">Chunk y coordinate</param>
    /// <param name="vz">Chunk z coordinate</param>
    public void ClientSeenChunkRemove(int clientid, int vx, int vy, int vz)
    {
        int pos = VectorIndexUtil.Index3d(vx, vy, vz, _serverMapStorage.MapSizeX / GameConstants.ServerChunkSize, _serverMapStorage.MapSizeY / GameConstants.ServerChunkSize);
        _serverClientService.Clients[clientid].chunksseen[pos] = false;
        _serverClientService.Clients[clientid].chunksseenTime[pos] = 0;
        //DiagLog.Write("UnseenChunk: {0},{1},{2} Client: {3}", vx, vy, vz, clientid);
    }

    private void SetFillAreaLimit(int clientid)
    {
        ServerPlayer client = _serverClientService.GetClient(clientid);
        if (client == null)
        {
            return;
        }

        int maxFill = 500;
        if (_serverClientService.ServerClient.DefaultFillLimit != null)
        {
            maxFill = _serverClientService.ServerClient.DefaultFillLimit.Value;
        }

        // Check if there is a fill-limit entry for his assigned group.
        if (client.ClientGroup.FillLimit != null)
        {
            maxFill = client.ClientGroup.FillLimit.Value;
        }

        // Check if there is an entry in clients with fill-limit member (overrides group fill-limit).
        foreach (Client clientConfig in _serverClientService.ServerClient.Clients)
        {
            if (clientConfig.Name.Equals(client.PlayerName, StringComparison.InvariantCultureIgnoreCase))
            {
                if (clientConfig.FillLimit != null)
                {
                    maxFill = clientConfig.FillLimit.Value;
                }

                break;
            }
        }

        client.FillLimit = maxFill;
        SendFillAreaLimit(clientid, maxFill);
    }

    private void SendFillAreaLimit(int clientid, int limit)
    {
        Packet_ServerFillAreaLimit p = new()
        {
            Limit = limit
        };
        _serverPacketService.SendPacket(clientid, Serialize(new Packet_Server() { Id = Packet_ServerIdEnum.FillAreaLimit, FillAreaLimit = p }));
    }

    private bool DoCommandBuild(int player_id, bool execute, Packet_ClientSetBlock cmd)
    {
        Vector3 v = new(cmd.X, cmd.Y, cmd.Z);
        Inventory inventory = GetPlayerInventory(_serverClientService.Clients[player_id].PlayerName);
        if (cmd.Mode == PacketBlockSetMode.Use)
        {
            _modEvents.RaiseBlockUse(player_id, cmd.X, cmd.Y, cmd.Z);

            return true;
        }

        if (cmd.Mode == PacketBlockSetMode.UseWithTool)
        {
            _modEvents.RaiseBlockUseWithTool(player_id, cmd.X, cmd.Y, cmd.Z, cmd.BlockType);

            return true;
        }

        if (cmd.Mode == PacketBlockSetMode.Create
            && _blockRegistry.Rail[cmd.BlockType] != 0)
        {
            return DoCommandBuildRail(player_id, cmd);
        }

        if (cmd.Mode == PacketBlockSetMode.Destroy
            && _blockRegistry.Rail[_serverMapStorage.GetBlock(cmd.X, cmd.Y, cmd.Z)] != 0)
        {
            return DoCommandRemoveRail(player_id, execute, cmd);
        }

        if (cmd.Mode == PacketBlockSetMode.Create)
        {
            int oldblock = _serverMapStorage.GetBlock(cmd.X, cmd.Y, cmd.Z);
            if (!(oldblock == 0 || _blockRegistry.BlockTypes[oldblock].IsFluid()))
            {
                return false;
            }

            InventoryItem? item = inventory.RightHand[cmd.MaterialSlot];
            if (item == null)
            {
                return false;
            }

            switch (item.InventoryItemType)
            {
                case InventoryItemType.Block:
                    item.BlockCount--;
                    if (item.BlockCount == 0)
                    {
                        inventory.RightHand[cmd.MaterialSlot] = null;
                    }

                    if (_blockRegistry.Rail[item.BlockId] != 0)
                    {
                    }

                    SetBlockAndNotify(cmd.X, cmd.Y, cmd.Z, item.BlockId);
                    _modEvents.RaiseBlockBuild(player_id, cmd.X, cmd.Y, cmd.Z);

                    break;
                default:
                    //TODO
                    return false;
            }
        }
        else
        {
            InventoryItem item = new()
            {
                InventoryItemType = InventoryItemType.Block
            };
            int blockid = _serverMapStorage.GetBlock(cmd.X, cmd.Y, cmd.Z);
            item.BlockId = _blockRegistry.WhenPlayerPlacesGetsConvertedTo[blockid];
            if (!_config.IsCreative)
            {
                GetInventoryUtil(inventory).GrabItem(item, cmd.MaterialSlot);
            }

            SetBlockAndNotify(cmd.X, cmd.Y, cmd.Z, SpecialBlockId.Empty);
            _modEvents.RaiseBlockDelete(player_id, cmd.X, cmd.Y, cmd.Z, blockid);
        }

        _serverClientService.Clients[player_id].IsInventoryDirty = true;
        NotifyInventory(player_id);
        return true;
    }

    private bool DoCommandBuildRail(int player_id, Packet_ClientSetBlock cmd)
    {
        Inventory inventory = GetPlayerInventory(_serverClientService.Clients[player_id].PlayerName);
        int oldblock = _serverMapStorage.GetBlock(cmd.X, cmd.Y, cmd.Z);
        if (!(oldblock == SpecialBlockId.Empty || _blockRegistry.IsRailTile(oldblock)))
        {
            return false;
        }

        //count how many rails will be created
        int oldrailcount = 0;
        if (_blockRegistry.IsRailTile(oldblock))
        {
            oldrailcount = DirectionUtils.RailDirectionFlagsCount(
                oldblock - _blockRegistry.BlockIdRailStart);
        }

        int newrailcount = DirectionUtils.RailDirectionFlagsCount(
            cmd.BlockType - _blockRegistry.BlockIdRailStart);
        int blockstoput = newrailcount - oldrailcount;

        InventoryItem item = inventory.RightHand[cmd.MaterialSlot];
        if (!(item.InventoryItemType == InventoryItemType.Block && _blockRegistry.Rail[item.BlockId] != 0))
        {
            return false;
        }

        item.BlockCount -= blockstoput;
        if (item.BlockCount == 0)
        {
            inventory.RightHand[cmd.MaterialSlot] = null;
        }

        SetBlockAndNotify(cmd.X, cmd.Y, cmd.Z, cmd.BlockType);
        _modEvents.RaiseBlockBuild(player_id, cmd.X, cmd.Y, cmd.Z);

        _serverClientService.Clients[player_id].IsInventoryDirty = true;
        NotifyInventory(player_id);
        return true;
    }

    private bool DoCommandRemoveRail(int player_id, bool execute, Packet_ClientSetBlock cmd)
    {
        Inventory inventory = GetPlayerInventory(_serverClientService.Clients[player_id].PlayerName);
        //add to inventory
        int blockid = _serverMapStorage.GetBlock(cmd.X, cmd.Y, cmd.Z);
        int blocktype = _blockRegistry.WhenPlayerPlacesGetsConvertedTo[blockid];
        if ((!IsValid(blocktype))
            || blocktype == SpecialBlockId.Empty)
        {
            return false;
        }

        int blockstopick = 1;
        if (_blockRegistry.IsRailTile(blocktype))
        {
            blockstopick = DirectionUtils.RailDirectionFlagsCount(
                blocktype - _blockRegistry.BlockIdRailStart);
        }

        InventoryItem item = new()
        {
            InventoryItemType = InventoryItemType.Block,
            BlockId = _blockRegistry.WhenPlayerPlacesGetsConvertedTo[blocktype],
            BlockCount = blockstopick
        };
        if (!_config.IsCreative)
        {
            GetInventoryUtil(inventory).GrabItem(item, cmd.MaterialSlot);
        }

        SetBlockAndNotify(cmd.X, cmd.Y, cmd.Z, SpecialBlockId.Empty);
        _modEvents.RaiseBlockDelete(player_id, cmd.X, cmd.Y, cmd.Z, blockid);

        _serverClientService.Clients[player_id].IsInventoryDirty = true;
        NotifyInventory(player_id);
        return true;
    }

    private bool IsValid(int blocktype) => _blockRegistry.BlockTypes[blocktype].Name != null;

    public void SetBlockAndNotify(int x, int y, int z, int blocktype)
    {
        _serverMapStorage.SetBlockNotMakingDirty(x, y, z, blocktype);
        NotifyBlock(x, y, z, blocktype);
    }

    public byte[] Serialize(Packet_Server p) => MemoryPackSerializer.Serialize(p);

    private string GenerateUsername(string name)
    {
        int appendNumber = 1;
        while (_serverClientService.Clients.Values.Any(c => c.PlayerName.Equals($"{name}{appendNumber}", StringComparison.OrdinalIgnoreCase)))
        {
            appendNumber++;
        }

        return $"{name}{appendNumber}";
    }

    public void ServerMessageToAll(string message, MessageType color)
    {
        SendMessageToAll(MessageTypeToString(color) + message);
        _gameLogger.Server.Information(string.Format("SERVER MESSAGE: {0}.", message));
    }

    public void SendMessageToAll(string message)
    {
        _gameLogger.Server.Information("Message to all: " + message);
        foreach (KeyValuePair<int, ServerPlayer> k in _serverClientService.Clients)
        {
            _serverPacketService.SendMessage(k.Key, message);
        }
    }

    private void SendSetBlock(int clientid, int x, int y, int z, int blocktype)
    {
        if (!ClientSeenChunk(clientid, x / GameConstants.ServerChunkSize, y / GameConstants.ServerChunkSize, z / GameConstants.ServerChunkSize))
        {
            // don't send block updates for chunks a player can not see
            return;
        }

        Packet_ServerSetBlock p = new() { X = x, Y = y, Z = z, BlockType = blocktype };
        _serverPacketService.SendPacket(clientid, Serialize(new Packet_Server() { Id = Packet_ServerIdEnum.SetBlock, SetBlock = p }));
    }

    public void SendSound(int clientid, string name, int x, int y, int z)
    {
        Packet_ServerSound p = new() { Name = name, X = x, Y = y, Z = z };
        _serverPacketService.SendPacket(clientid, Serialize(new Packet_Server() { Id = Packet_ServerIdEnum.Sound, Sound = p }));
    }

    private void SendPlayerSpawnPosition(int clientid, int x, int y, int z)
    {
        Packet_ServerPlayerSpawnPosition p = new()
        {
            X = x,
            Y = y,
            Z = z
        };
        _serverPacketService.SendPacket(clientid, Serialize(new Packet_Server()
        {
            Id = Packet_ServerIdEnum.PlayerSpawnPosition,
            PlayerSpawnPosition = p,
        }));
    }

    public long TotalReceivedBytes { get; set; }

    public int DrawDistance { get; set; } = 512;

    public double InvertedChunkSize { get; set; } = 1.0 / 32;

    public int InvertChunk(int num) => (int)(num * InvertedChunkSize);

    public int ChunkDrawDistance => DrawDistance / GameConstants.ServerChunkSize;

    public byte[] CompressChunkNetwork(ushort[] chunk) => _networkCompression.Compress(MemoryMarshal.AsBytes(chunk.AsSpan()));

    private string[] GetRequiredBlobMd5()
        => [.. _assetManager.Assets.Select(a => a.md5)];

    private string[] GetRequiredBlobName()
        => [.. _assetManager.Assets.Select(a => a.name)];

    private readonly int blobPartLength = 1024;

    private async void SendBlobs(int clientid, string[] requestedMd5)
    {
        _serverPacketService.SendPacket(clientid, ServerPackets.LevelInitialize());
        await  _assetManager.LoadAssetsAsync();

        List<Asset> tosend = [];
        for (int i = 0; i < _assetManager.Assets.Count; i++)
        {
            Asset f = _assetManager.Assets[i];
            for (int k = 0; k < requestedMd5.Length; k++)
            {
                if (f.md5 == requestedMd5[k])
                {
                    tosend.Add(f);
                }
            }
        }

        for (int i = 0; i < tosend.Count; i++)
        {
            Asset f = tosend[i];
            SendBlobInitialize(clientid, f.md5, f.name);
            byte[] blob = f.data;
            int totalsent = 0;
            foreach (byte[] part in Parts(blob, blobPartLength))
            {
                SendLevelProgress(clientid,
                    (int)((((float)i / tosend.Count)
                        + ((float)totalsent / blob.Length / tosend.Count)) * 100),
                    _languageService.ServerProgressDownloadingData());
                SendBlobPart(clientid, part);
                totalsent += part.Length;
            }

            SendBlobFinalize(clientid);
        }

        SendLevelProgress(clientid, 0, _languageService.ServerProgressGenerating());
    }

    public IEnumerable<byte[]> Parts(byte[] blob, int partsize)
    {
        for (int i = 0; i < blob.Length; i += partsize)
        {
            yield return blob[i..Math.Min(i + partsize, blob.Length)];
        }
    }

    private void SendBlobInitialize(int clientid, string hash, string name)
    {
        Packet_ServerBlobInitialize p = new() { Name = name, Md5 = hash };
        _serverPacketService.SendPacket(clientid, Serialize(new Packet_Server() { Id = Packet_ServerIdEnum.BlobInitialize, BlobInitialize = p }));
    }

    private void SendBlobPart(int clientid, byte[] data)
    {
        Packet_ServerBlobPart p = new() { Data = data };
        _serverPacketService.SendPacket(clientid, Serialize(new Packet_Server() { Id = Packet_ServerIdEnum.BlobPart, BlobPart = p }));
    }

    private void SendBlobFinalize(int clientid)
    {
        Packet_ServerBlobFinalize p = new() { };
        _serverPacketService.SendPacket(clientid, Serialize(new Packet_Server() { Id = Packet_ServerIdEnum.BlobFinalize, BlobFinalize = p }));
    }

    public void SendBlockTypes(int clientid)
    {
        foreach ((int id, BlockType? blockType) in _blockRegistry.BlockTypes)
        {
            Packet_ServerBlockType p1 = new() { Id = id, Blocktype = blockType };
            _serverPacketService.SendPacket(clientid, Serialize(new Packet_Server() { Id = Packet_ServerIdEnum.BlockType, BlockType = p1 }));
        }

        Packet_ServerBlockTypes p = new() { };
        _serverPacketService.SendPacket(clientid, Serialize(new Packet_Server() { Id = Packet_ServerIdEnum.BlockTypes, BlockTypes = p }));
    }

    private void SendTranslations(int clientid)
    {
        //Read all lines from server translation and send them to the client
        foreach (((string? lang, string? id), string? translated) in _languageService.AllStrings())
        {
            Packet_ServerTranslatedString p = new()
            {
                Lang = lang,
                Id = id,
                Translation = translated
            };
            _serverPacketService.SendPacket(clientid, Serialize(new Packet_Server { Id = Packet_ServerIdEnum.Translation, Translation = p }));
        }
    }

    public int SerializeFloat(float p) => (int)(p * 32);

    private void SendSunLevels(int clientid)
    {
        Packet_ServerSunLevels p = new();
        p.Sunlevels = sunlevels;
        _serverPacketService.SendPacket(clientid, Serialize(new Packet_Server() { Id = Packet_ServerIdEnum.SunLevels, SunLevels = p }));
    }

    private void SendLightLevels(int clientid)
    {
        Packet_ServerLightLevels p = new();
        int[] l = new int[lightlevels.Length];
        for (int i = 0; i < lightlevels.Length; i++)
        {
            l[i] = SerializeFloat(lightlevels[i]);
        }

        p.Lightlevels = l;
        _serverPacketService.SendPacket(clientid, Serialize(new Packet_Server() { Id = Packet_ServerIdEnum.LightLevels, LightLevels = p }));
    }

    private void SendCraftingRecipes(int clientid)
    {
        Packet_ServerCraftingRecipes p = new()
        {
            CraftingRecipes = [.. CraftingRecipes]
        };
        _serverPacketService.SendPacket(clientid, Serialize(new Packet_Server() { Id = Packet_ServerIdEnum.CraftingRecipes, CraftingRecipes = p }));
    }

    private void SendLevelProgress(int clientid, int percentcomplete, string status)
    {
        Packet_ServerLevelProgress p = new() { PercentComplete = percentcomplete, Status = status };
        _serverPacketService.SendPacket(clientid, Serialize(new Packet_Server() { Id = Packet_ServerIdEnum.LevelDataChunk, LevelDataChunk = p }));
    }

    public RenderHint RenderHint { get; set; } = RenderHint.Fast;

    private void SendServerIdentification(int clientid)
    {
        Packet_ServerIdentification p = new()
        {
            MdProtocolVersion = GameVersion.Version,
            AssignedClientId = clientid,
            ServerName = _config.Name,
            ServerMotd = _config.Motd,
            MapSizeX = _serverMapStorage.MapSizeX,
            MapSizeY = _serverMapStorage.MapSizeY,
            MapSizeZ = _serverMapStorage.MapSizeZ,
            DisableShadows = EnableShadows ? 0 : 1,
            PlayerAreaSize = playerareasize,
            RenderHint_ = (int)RenderHint,
            RequiredBlobMd5 = GetRequiredBlobMd5(),
            RequiredBlobName = GetRequiredBlobName(),
        };
        _serverPacketService.SendPacket(clientid, Serialize(new Packet_Server() { Id = Packet_ServerIdEnum.ServerIdentification, Identification = p }));
    }

    public void SendFreemoveState(int clientid, bool isEnabled)
    {
        Packet_ServerFreemove p = new()
        {
            IsEnabled = isEnabled ? 1 : 0
        };
        _serverPacketService.SendPacket(clientid, Serialize(new Packet_Server() { Id = Packet_ServerIdEnum.Freemove, Freemove = p }));
    }

    private static string ComputeMd5(string input)
    {
        byte[] hash = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLower();
    }

    public Dictionary<string, bool> Disabledprivileges { get; set; } = [];
    public Dictionary<string, bool> ExtraPrivileges { get; set; } = [];

    public MeinKraft.Group? DefaultGroupGuest { get; set; }
    public MeinKraft.Group DefaultGroupRegistered { get; set; }
    public Vector3i DefaultPlayerSpawn { get; set; }

    private Vector3i SpawnToVector3i(Spawn spawn)
    {
        int x = spawn.x;
        int y = spawn.y;
        int z;
        if (!VectorUtils.IsValidPos(_serverMapStorage, x, y))
        {
            throw new Exception(_languageService.ServerInvalidSpawnCoordinates());
        }

        if (spawn.z == null)
        {
            z = VectorUtils.BlockHeight(_serverMapStorage, 0, x, y);
        }
        else
        {
            z = spawn.z.Value;
            if (!VectorUtils.IsValidPos(_serverMapStorage, x, y, z))
            {
                throw new Exception(_languageService.ServerInvalidSpawnCoordinates());
            }
        }

        return new Vector3i(x * 32, z * 32, y * 32);
    }

    private const int dumpmax = 30;
    public void DropItem(ref InventoryItem item, Vector3i pos)
    {
        switch (item.InventoryItemType)
        {
            case InventoryItemType.Block:
                for (int i = 0; i < dumpmax; i++)
                {
                    if (item.BlockCount == 0)
                    {
                        break;
                    }
                    //find empty position that is nearest to dump place AND has a block under.
                    Vector3i? nearpos = FindDumpPlace(pos);
                    if (nearpos == null)
                    {
                        break;
                    }

                    SetBlockAndNotify(nearpos.Value.X, nearpos.Value.Y, nearpos.Value.Z, item.BlockId);
                    item.BlockCount--;
                }

                if (item.BlockCount == 0)
                {
                    item = null;
                }

                break;
            default:
                //todo
                break;
        }
    }

    private Vector3i? FindDumpPlace(Vector3i pos)
    {
        List<Vector3i> l = [];
        for (int x = 0; x < 10; x++)
        {
            for (int y = 0; y < 10; y++)
            {
                for (int z = 0; z < 10; z++)
                {
                    int xx = pos.X + x - (10 / 2);
                    int yy = pos.Y + y - (10 / 2);
                    int zz = pos.Z + z - (10 / 2);
                    if (!VectorUtils.IsValidPos(_serverMapStorage, xx, yy, zz))
                    {
                        continue;
                    }

                    if (_serverMapStorage.GetBlock(xx, yy, zz) == SpecialBlockId.Empty
                        && _serverMapStorage.GetBlock(xx, yy, zz - 1) != SpecialBlockId.Empty)
                    {
                        bool playernear = false;
                        foreach (KeyValuePair<int, ServerPlayer> player in _serverClientService.Clients)
                        {
                            if (VectorUtils.DistanceSquared(PlayerBlockPosition(player.Value), new Vector3i(xx, yy, zz)) < 9)
                            {
                                playernear = true;
                            }
                        }

                        if (!playernear)
                        {
                            l.Add(new Vector3i(xx, yy, zz));
                        }
                    }
                }
            }
        }

        l.Sort((a, b) => VectorUtils.DistanceSquared(a, pos).CompareTo(VectorUtils.DistanceSquared(b, pos)));
        if (l.Count > 0)
        {
            return l[0];
        }

        return null;
    }

    public void SetBlockType(int id, string name, BlockType block)
    {
        _blockRegistry.BlockTypes[id] = block;
        block.Name = name;
        _blockRegistry.RegisterBlockType(id, block);
    }

    public void SetBlockType(string name, BlockType block)
    {
        int id = _blockRegistry.BlockTypes.Count == 0 ? 0 : _blockRegistry.BlockTypes.Keys.Max() + 1;
        SetBlockType(id, name, block);
    }

    private int[] sunlevels = [];
    public void SetSunLevels(int[] sunLevels) => sunlevels = sunLevels;

    private float[] lightlevels = [];
    public void SetLightLevels(float[] lightLevels) => lightlevels = lightLevels;

    public List<CraftingRecipe> CraftingRecipes { get; set; } = [];

    public void SendDialog(int player, string id, Dialog dialog)
    {
        Packet_ServerDialog p = new()
        {
            DialogId = id,
            Dialog = dialog,
        };
        _serverPacketService.SendPacket(player, Serialize(new Packet_Server() { Id = Packet_ServerIdEnum.Dialog, Dialog = p }));
    }

    public bool PlayerHasPrivilege(int player, string privilege)
    {
        if (ExtraPrivileges.ContainsKey(privilege))
        {
            return true;
        }

        if (Disabledprivileges.ContainsKey(privilege))
        {
            return false;
        }

        return _serverClientService.GetClient(player).Privileges.Contains(privilege);
    }

    public void PlaySoundAt(int posx, int posy, int posz, string sound) => PlaySoundAtExceptPlayer(posx, posy, posz, sound, null);

    private void PlaySoundAtExceptPlayer(int posx, int posy, int posz, string sound, int? player)
    {
        Vector3i pos = new(posx, posy, posz);
        foreach (KeyValuePair<int, ServerPlayer> k in _serverClientService.Clients)
        {
            if (player != null && player == k.Key)
            {
                continue;
            }

            int distance = VectorUtils.DistanceSquared(new Vector3i(k.Value.PositionMul32GlX / 32, k.Value.PositionMul32GlZ / 32, k.Value.PositionMul32GlY / 32), pos);
            if (distance < 64 * 64)
            {
                SendSound(k.Key, sound, pos.X, posy, posz);
            }
        }
    }

    public void PlaySoundAt(int posx, int posy, int posz, string sound, int range) => PlaySoundAtExceptPlayer(posx, posy, posz, sound, null, range);

    private void PlaySoundAtExceptPlayer(int posx, int posy, int posz, string sound, int? player, int range)
    {
        Vector3i pos = new(posx, posy, posz);
        foreach (KeyValuePair<int, ServerPlayer> k in _serverClientService.Clients)
        {
            if (player != null && player == k.Key)
            {
                continue;
            }

            int distance = VectorUtils.DistanceSquared(new Vector3i(k.Value.PositionMul32GlX / 32, k.Value.PositionMul32GlZ / 32, k.Value.PositionMul32GlY / 32), pos);
            if (distance < range)
            {
                SendSound(k.Key, sound, pos.X, posy, posz);
            }
        }
    }

    public void SendPacketFollow(int player, int target, bool tpp)
    {
        Packet_ServerFollow p = new()
        {
            Client = target == -1 ? null : _serverClientService.Clients[target].PlayerName,
            Tpp = tpp ? 1 : 0,
        };
        _serverPacketService.SendPacket(player, Serialize(new Packet_Server() { Id = Packet_ServerIdEnum.Follow, Follow = p }));
    }

    public void SendAmmo(int playerid, Dictionary<int, int> totalAmmo)
    {
        Packet_ServerAmmo p = new();
        Packet_IntInt[] t = new Packet_IntInt[totalAmmo.Count];
        int i = 0;
        foreach (KeyValuePair<int, int> k in totalAmmo)
        {
            t[i++] = new Packet_IntInt() { Key_ = k.Key, Value_ = k.Value };
        }

        p.TotalAmmo = t;
        _serverPacketService.SendPacket(playerid, Serialize(new Packet_Server() { Id = Packet_ServerIdEnum.Ammo, Ammo = p }));
    }

    public void SendExplosion(int player, float x, float y, float z, bool relativeposition, float range, float time)
    {
        Packet_ServerExplosion p = new()
        {
            XFloat = SerializeFloat(x),
            YFloat = SerializeFloat(y),
            ZFloat = SerializeFloat(z),
            IsRelativeToPlayerPosition = relativeposition ? 1 : 0,
            RangeFloat = SerializeFloat(range),
            TimeFloat = SerializeFloat(time)
        };
        _serverPacketService.SendPacket(player, Serialize(new Packet_Server() { Id = Packet_ServerIdEnum.Explosion, Explosion = p }));
    }

    public string GetGroupColor(int playerid) => _serverClientService.GetClient(playerid).ClientGroup.GroupColorString();

    public string GetGroupName(int playerid) => _serverClientService.GetClient(playerid).ClientGroup.Name;

    public void InstallHttpModule(string name, Func<string> description, IHttpModule module)
    {
        ActiveHttpModule m = new()
        {
            name = name,
            description = description,
            module = module
        };
        HttpModules.Add(m);
    }

    public List<ActiveHttpModule> HttpModules { get; set; } = [];

    public GameTimer GetTimer() => _gameTimer;

    public void PlayerEntitySetDirty(int player)
    {
        foreach (ServerPlayer k in _serverClientService.Clients.Values)
        {
            k.PlayersDirty[player] = true;
        }
    }

    public ServerEntity GetEntity(int chunkx, int chunky, int chunkz, int id)
    {
        ServerChunk c = _serverMapStorage.GetChunk(chunkx * GameConstants.ServerChunkSize, chunky * GameConstants.ServerChunkSize, chunkz * GameConstants.ServerChunkSize);
        return c.Entities[id];
    }

    public void SetEntityDirty(ServerEntityId id)
    {
        foreach (KeyValuePair<int, ServerPlayer> k in _serverClientService.Clients)
        {
            for (int i = 0; i < k.Value.SpawnedEntities.Length; i++)
            {
                ServerEntityId s = k.Value.SpawnedEntities[i];
                if (s != null &&
                    s.ChunkX == id.ChunkX &&
                    s.ChunkY == id.ChunkY &&
                    s.ChunkZ == id.ChunkZ &&
                    s.Id == id.Id)
                {
                    k.Value.UpdateEntity[i] = true;
                }
            }
        }

        ServerChunk chunk = _serverMapStorage.GetChunk(id.ChunkX * GameConstants.ServerChunkSize, id.ChunkY * GameConstants.ServerChunkSize, id.ChunkZ * GameConstants.ServerChunkSize);
        chunk.DirtyForSaving = true;
    }

    public void DespawnEntity(ServerEntityId id)
    {
        ServerChunk chunk = _serverMapStorage.GetChunk(id.ChunkX * GameConstants.ServerChunkSize, id.ChunkY * GameConstants.ServerChunkSize, id.ChunkZ * GameConstants.ServerChunkSize);
        chunk.Entities.Remove(id.Id);
        chunk.DirtyForSaving = true;
    }

    public void AddEntity(int x, int y, int z, ServerEntity e)
    {
        ServerChunk c = _serverMapStorage.GetChunk(x, y, z);
        int id = c.Entities.Count == 0 ? 0 : c.Entities.Keys.Max() + 1;
        c.Entities[id] = e;
        c.DirtyForSaving = true;
    }
}

/// <summary>A timer paired with its callback, held in <see cref="ServerGameService.Timers"/>.</summary>
public sealed record ServerTimerRegistration(ServerTimer Timer, Action Callback);