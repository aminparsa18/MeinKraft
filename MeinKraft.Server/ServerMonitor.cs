using MeinKraft;
using System.Text.Json;
using System.Text.Json.Serialization;
using static MeinKraft.ServerPacketService;

public class ServerMonitor
{
    private ServerMonitorConfig config;
    private readonly ILanguageService _languageService;
    private readonly IClientRegistry _serverClientService;
    private readonly IServerPacketService _serverPacketService;
    private readonly ServerGameService server;
    private readonly Dictionary<int, MonitorClient> monitorClients;

    public ServerMonitor(ServerGameService server, ILanguageService languageService, IClientRegistry serverClientService, IServerPacketService serverPacketService)
    {
        this.server = server;
        _serverClientService = serverClientService;
        _serverPacketService = serverPacketService;
        _languageService = languageService;
        LoadConfig();
        monitorClients = [];
    }

    public bool RemoveMonitorClient(int clientid) => monitorClients.Remove(clientid);

    public void Start()
    {
        Thread serverMonitorThread = new(new ThreadStart(Process));
        serverMonitorThread.Start();
    }

    private void Process()
    {
        // TODO: implement proper monitoring after fully migrated to Server separate project
        //while (!Exit.Exit)
        {
            Thread.Sleep(TimeSpan.FromSeconds(config.TimeIntervall));
            foreach (KeyValuePair<int, MonitorClient> k in monitorClients)
            {
                k.Value.BlocksSet = 0;
                k.Value.MessagesSent = 0;
                k.Value.PacketsReceived = 0;
            }
        }
    }

    public bool CheckPacket(int clientId, Packet_Client packet)
    {
        if (!monitorClients.TryGetValue(clientId, out MonitorClient? value))
        {
            value = new MonitorClient() { Id = clientId };
            monitorClients.Add(clientId, value);
        }

        value.PacketsReceived++;
        if (value.PacketsReceived > config.MaxPackets)
        {
            server.Kick(GameConstants.ServerConsoleId, clientId, "Packet Overflow");
            return false;
        }

        switch (packet.Id)
        {
            case PacketType.SetBlock:
            case PacketType.FillArea:
                if (monitorClients[clientId].SetBlockPunished())
                {
                    // TODO: revert block at client
                    return false;
                }

                if (monitorClients[clientId].BlocksSet < config.MaxBlocks)
                {
                    monitorClients[clientId].BlocksSet++;
                    return true;
                }
                // punish client
                return ActionSetBlock(clientId);
            case PacketType.Message:
                if (monitorClients[clientId].MessagePunished())
                {
                    _serverPacketService.SendMessage(clientId, _languageService.ServerMonitorChatNotSent(), MessageType.Error);
                    return false;
                }

                if (monitorClients[clientId].MessagesSent < config.MaxMessages)
                {
                    monitorClients[clientId].MessagesSent++;
                    return true;
                }
                // punish client
                return ActionMessage(clientId);
            default:
                return true;
        }
    }

    // Actions which will be taken when client exceeds a limit.
    private bool ActionSetBlock(int clientId)
    {
        monitorClients[clientId].SetBlockPunishment = new Punishment();//infinte duration
        server.ServerMessageToAll(string.Format(_languageService.ServerMonitorBuildingDisabled(), _serverClientService.GetClient(clientId).PlayerName), MessageType.Important);
        return false;
    }

    private bool ActionMessage(int clientId)
    {
        monitorClients[clientId].MessagePunishment = new Punishment(new TimeSpan(0, 0, config.MessageBanTime));
        server.ServerMessageToAll(string.Format(_languageService.ServerMonitorChatMuted(), _serverClientService.GetClient(clientId).PlayerName, config.MessageBanTime), MessageType.Important);
        return false;
    }

    private class MonitorClient
    {
        public int Id = -1;
        public int PacketsReceived = 0;
        public int BlocksSet = 0;
        public int MessagesSent = 0;

        public Punishment? SetBlockPunishment;
        public bool SetBlockPunished()
        {
            if (SetBlockPunishment == null)
            {
                return false;
            }

            return SetBlockPunishment.Active();
        }

        public Punishment? MessagePunishment;
        public bool MessagePunished()
        {
            if (MessagePunishment == null)
            {
                return false;
            }

            return MessagePunishment.Active();
        }
    }

    private class Punishment
    {
        private readonly DateTime punishmentStartDate;
        private readonly bool permanent;
        private readonly TimeSpan duration;

        public Punishment(TimeSpan duration)
        {
            punishmentStartDate = DateTime.UtcNow;
            this.duration = duration;
            permanent = false;
        }

        public Punishment()
        {
            punishmentStartDate = DateTime.UtcNow;
            duration = TimeSpan.MinValue;
            permanent = true;
        }

        public bool Active()
        {
            if (permanent)
            {
                return true;
            }

            if (DateTime.UtcNow.Subtract(punishmentStartDate).CompareTo(duration) == -1)
            {
                return true;
            }

            return false;
        }
    }

    public class ServerMonitorConfig
    {
        public int MaxPackets; // max number of packets - packet flood protection
        public int MaxBlocks; // max number of blocks which can be set within the time intervall
        public int MaxMessages; // max number of chat messages per time intervall
        public int MessageBanTime;// how long gets a player muted (in seconds)
        public int TimeIntervall; // in seconds, resets count values

        public ServerMonitorConfig()
        {
            //Set Defaults
            MaxPackets = 500;
            MaxBlocks = 50;
            MaxMessages = 3;
            MessageBanTime = 60;
            TimeIntervall = 3;
        }
    }

    private readonly string filename = "ServerMonitor.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private void LoadConfig()
    {
        string path = Path.Combine(GameStorePath.gamepathconfig, filename);
        if (!File.Exists(path))
        {
            Console.WriteLine(_languageService.ServerMonitorConfigNotFound());
            config = new ServerMonitorConfig();
            SaveConfig();
        }
        else
        {
            try
            {
                string json = File.ReadAllText(path);
                config = JsonSerializer.Deserialize<ServerMonitorConfig>(json, JsonOptions)
                              ?? new ServerMonitorConfig();
            }
            catch
            {
                config = new ServerMonitorConfig();
            }
        }

        Console.WriteLine(_languageService.ServerMonitorConfigLoaded());
    }

    public void SaveConfig()
    {
        if (!Directory.Exists(GameStorePath.gamepathconfig))
        {
            Directory.CreateDirectory(GameStorePath.gamepathconfig);
        }

        config ??= new ServerMonitorConfig();
        File.WriteAllText(
            Path.Combine(GameStorePath.gamepathconfig, filename),
            JsonSerializer.Serialize(config, JsonOptions));
    }
}
