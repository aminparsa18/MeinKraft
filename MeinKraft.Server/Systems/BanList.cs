using MeinKraft;
using System.Text.Json;
using System.Text.Json.Serialization;

public class ServerSystemBanList : ServerSystem
{
    private const string BanlistFilename = "ServerBanlist.json";

    private readonly ILanguageService _languageService;
    private readonly IClientRegistry _serverClientService;
    private readonly IServerPacketService _serverPacketService;
    private readonly IGameLogger _gameLogger;
    private readonly ServerGameService server;

    public ServerSystemBanList(ServerGameService server, IModEvents modEvents, ILanguageService languageService, IClientRegistry serverClientService,
        IServerPacketService serverPacketService, IGameLogger gameLogger) : base(modEvents)
    {   
        this.server = server;
        _languageService = languageService;
        _serverClientService = serverClientService;
        _serverPacketService = serverPacketService;
        _gameLogger = gameLogger;
    }

    protected override void Initialize() => LoadBanlist();

    protected override void OnUpdate(ServerGameService server, float dt)
    {
        if (_serverClientService.BanList.ClearTimeBans() > 0)
        {
            SaveBanlist();
        }

        foreach (KeyValuePair<int, ServerPlayer> k in _serverClientService.Clients)
        {
            CheckAndKickBannedClient(k.Key, k.Value);
        }
    }

    private void CheckAndKickBannedClient(int clientId, ServerPlayer client)
    {
        string ip = client.Socket.RemoteEndPoint().AddressToString();

        if (_serverClientService.BanList.IsIPBanned(ip))
        {
            string reason = _serverClientService.BanList.GetIPEntry(ip).Reason ?? "";
            _serverPacketService.SendPacket(clientId, ServerPackets.DisconnectPlayer(
                string.Format(_languageService.ServerIPBanned(), reason)));
            LogAndKick(clientId, $"Banned IP {ip} tries to connect.");
            return;
        }

        string username = client.PlayerName;
        if (_serverClientService.BanList.IsUserBanned(username))
        {
            string reason = _serverClientService.BanList.GetUserEntry(username).Reason ?? "";
            _serverPacketService.SendPacket(clientId, ServerPackets.DisconnectPlayer(
                string.Format(_languageService.ServerUsernameBanned(), reason)));
            LogAndKick(clientId, $"{ip} fails to join (banned username: {username}).");
        }
    }

    private void LogAndKick(int clientId, string message)
    {
        Console.WriteLine(message);
       _gameLogger.Server.Debug(message);
        server.KillPlayer(clientId);
    }

    // -------------------------------------------------------------------------
    // Command dispatch
    // -------------------------------------------------------------------------

    public override bool OnCommand(ServerGameService server, int sourceClientId, string command, string argument)
    {
        string[] args = argument.Split(' ');
        string colorError = GameConstants.colorError;

        void SendInvalidArgs()
            => _serverPacketService.SendMessage(sourceClientId, colorError + _languageService.Get("Server_CommandInvalidArgs"));

        bool TryParseId(out int id) => int.TryParse(args[0], out id);
        bool TryParseDuration(int index, out int duration) => int.TryParse(args[index], out duration);

        string TrailingReason(int fromIndex)
            => args.Length > fromIndex ? string.Join(" ", args, fromIndex, args.Length - fromIndex) : "";

        switch (command)
        {
            case "banip_id":
                if (!TryParseId(out int banipId))
                {
                    SendInvalidArgs();
                    return true;
                }

                BanIP(sourceClientId, banipId, TrailingReason(1));
                return true;

            case "banip":
                BanIP(sourceClientId, args[0], TrailingReason(1));
                return true;

            case "ban_id":
                if (!TryParseId(out int banId))
                {
                    SendInvalidArgs();
                    return true;
                }

                Ban(sourceClientId, banId, TrailingReason(1));
                return true;

            case "ban":
                Ban(sourceClientId, args[0], TrailingReason(1));
                return true;

            case "timebanip_id": // /timebanip_id <id> <duration> [reason]
                if (args.Length < 2 || !TryParseId(out int tbanipById) || !TryParseDuration(1, out int tbanipByIdDur))
                {
                    SendInvalidArgs();
                    return true;
                }

                if (tbanipByIdDur <= 0)
                {
                    _serverPacketService.SendMessage(sourceClientId, colorError + _languageService.Get("Server_CommandTimeBanInvalidValue"));
                    return true;
                }

                TimeBanIP(sourceClientId, tbanipById, TrailingReason(2), tbanipByIdDur);
                return true;

            case "timebanip": // /timebanip <name> <duration> [reason]
                if (args.Length < 2 || !TryParseDuration(1, out int tbanipDur))
                {
                    SendInvalidArgs();
                    return true;
                }

                if (tbanipDur <= 0)
                {
                    _serverPacketService.SendMessage(sourceClientId, colorError + _languageService.Get("Server_CommandTimeBanInvalidValue"));
                    return true;
                }

                TimeBanIP(sourceClientId, args[0], TrailingReason(2), tbanipDur);
                return true;

            case "timeban_id": // /timeban_id <id> <duration> [reason]
                if (args.Length < 2 || !TryParseId(out int tbanById) || !TryParseDuration(1, out int tbanByIdDur))
                {
                    SendInvalidArgs();
                    return true;
                }

                if (tbanByIdDur <= 0)
                {
                    _serverPacketService.SendMessage(sourceClientId, colorError + _languageService.Get("Server_CommandTimeBanInvalidValue"));
                    return true;
                }

                TimeBan(sourceClientId, tbanById, TrailingReason(2), tbanByIdDur);
                return true;

            case "timeban": // /timeban <name> <duration> [reason]
                if (args.Length < 2 || !TryParseDuration(1, out int tbanDur))
                {
                    SendInvalidArgs();
                    return true;
                }

                if (tbanDur <= 0)
                {
                    _serverPacketService.SendMessage(sourceClientId, colorError + _languageService.Get("Server_CommandTimeBanInvalidValue"));
                    return true;
                }

                TimeBan(sourceClientId, args[0], TrailingReason(2), tbanDur);
                return true;

            case "ban_offline":
                BanOffline(sourceClientId, args[0], TrailingReason(1));
                return true;

            case "unban":
                if (args.Length == 2)
                {
                    Unban(sourceClientId, args[0], args[1]);
                    return true;
                }

                SendInvalidArgs();
                return true;

            default:
                return false;
        }
    }

    // -------------------------------------------------------------------------
    // Ban helpers — name-based overloads resolve to ID-based
    // -------------------------------------------------------------------------

    public bool Ban(int sourceClientId, string target, string reason = "")
    {
        ServerPlayer targetClient = _serverClientService.GetClient(target);
        if (targetClient != null)
        {
            return Ban(sourceClientId, targetClient.Id, reason);
        }

        _serverPacketService.SendMessage(sourceClientId, string.Format(
            _languageService.Get("Server_CommandPlayerNotFound"), GameConstants.colorError, target));
        return false;
    }

    public bool Ban(int sourceClientId, int targetClientId, string reason = "")
    {
        if (!CheckPrivilege(sourceClientId, Privilege.ban))
        {
            return false;
        }

        ServerPlayer target = _serverClientService.GetClient(targetClientId);
        if (target == null)
        {
            return SendNonexistentId(sourceClientId, targetClientId);
        }

        if (!CheckTargetRank(sourceClientId, target))
        {
            return false;
        }

        reason = FormatReason(reason);
        string targetName = target.PlayerName;
        string sourceName = _serverClientService.GetClient(sourceClientId).PlayerName;

        _serverClientService.BanList.BanPlayer(targetName, sourceName, reason);
        SaveBanlist();
        BroadcastAndKick(sourceClientId, targetClientId,
            "Server_CommandBanMessage", "Server_CommandBanNotification",
            targetName, sourceName, target, reason);
        _gameLogger.Server.Debug($"{sourceName} bans {targetName}.{reason}");
        return true;
    }

    public bool BanIP(int sourceClientId, string target, string reason = "")
    {
        ServerPlayer targetClient = _serverClientService.GetClient(target);
        if (targetClient != null)
        {
            return BanIP(sourceClientId, targetClient.Id, reason);
        }

        _serverPacketService.SendMessage(sourceClientId, string.Format(
            _languageService.Get("Server_CommandPlayerNotFound"), GameConstants.colorError, target));
        return false;
    }

    public bool BanIP(int sourceClientId, int targetClientId, string reason = "")
    {
        if (!CheckPrivilege(sourceClientId, Privilege.banip))
        {
            return false;
        }

        ServerPlayer target = _serverClientService.GetClient(targetClientId);
        if (target == null)
        {
            return SendNonexistentId(sourceClientId, targetClientId);
        }

        if (!CheckTargetRank(sourceClientId, target))
        {
            return false;
        }

        reason = FormatReason( reason);
        string targetName = target.PlayerName;
        string sourceName = _serverClientService.GetClient(sourceClientId).PlayerName;

        _serverClientService.BanList.BanIP(target.Socket.RemoteEndPoint().AddressToString(), sourceName, reason);
        SaveBanlist();
        BroadcastAndKick(sourceClientId, targetClientId,
            "Server_CommandIPBanMessage", "Server_CommandIPBanNotification",
            targetName, sourceName, target, reason);
        _gameLogger.Server.Debug($"{sourceName} IP bans {targetName}.{reason}");
        return true;
    }

    public bool TimeBan(int sourceClientId, string target, string reason, int duration)
    {
        ServerPlayer targetClient = _serverClientService.GetClient(target);
        if (targetClient != null)
        {
            return TimeBan(sourceClientId, targetClient.Id, reason, duration);
        }

        _serverPacketService.SendMessage(sourceClientId, string.Format(
            _languageService.Get("Server_CommandPlayerNotFound"), GameConstants.colorError, target));
        return false;
    }

    public bool TimeBan(int sourceClientId, int targetClientId, string reason, int duration)
    {
        if (!CheckPrivilege(sourceClientId, Privilege.ban))
        {
            return false;
        }

        ServerPlayer target = _serverClientService.GetClient(targetClientId);
        if (target == null)
        {
            return SendNonexistentId(sourceClientId, targetClientId);
        }

        if (!CheckTargetRank(sourceClientId, target))
        {
            return false;
        }

        reason = FormatReason(reason);
        string targetName = target.PlayerName;
        string sourceName = _serverClientService.GetClient(sourceClientId).PlayerName;

        _serverClientService.BanList.TimeBanPlayer(targetName, sourceName, reason, duration);
        SaveBanlist();
        server.SendMessageToAll(string.Format(_languageService.Get("Server_CommandTimeBanMessage"),
            GameConstants.colorImportant, target.ColoredPlayername(GameConstants.colorImportant),
            _serverClientService.GetClient(sourceClientId).ColoredPlayername(GameConstants.colorImportant), duration, reason));
        _serverPacketService.SendPacket(targetClientId, ServerPackets.DisconnectPlayer(
            string.Format(_languageService.Get("Server_CommandTimeBanNotification"), duration, reason)));
        _gameLogger.Server.Debug($"{sourceName} bans {targetName} for {duration} minutes.{reason}");
        server.KillPlayer(targetClientId);
        return true;
    }

    public bool TimeBanIP(int sourceClientId, string target, string reason, int duration)
    {
        ServerPlayer targetClient = _serverClientService.GetClient(target);
        if (targetClient != null)
        {
            return TimeBanIP(sourceClientId, targetClient.Id, reason, duration);
        }

        _serverPacketService.SendMessage(sourceClientId, string.Format(
            _languageService.Get("Server_CommandPlayerNotFound"), GameConstants.colorError, target));
        return false;
    }

    public bool TimeBanIP(int sourceClientId, int targetClientId, string reason, int duration)
    {
        if (!CheckPrivilege(sourceClientId, Privilege.banip))
        {
            return false;
        }

        ServerPlayer target = _serverClientService.GetClient(targetClientId);
        if (target == null)
        {
            return SendNonexistentId(sourceClientId, targetClientId);
        }

        if (!CheckTargetRank(sourceClientId, target))
        {
            return false;
        }

        reason = FormatReason(reason);
        string targetName = target.PlayerName;
        string sourceName = _serverClientService.GetClient(sourceClientId).PlayerName;

        _serverClientService.BanList.TimeBanIP(target.Socket.RemoteEndPoint().AddressToString(), sourceName, reason, duration);
        SaveBanlist();
        server.SendMessageToAll(string.Format(_languageService.Get("Server_CommandTimeIPBanMessage"),
            GameConstants.colorImportant, target.ColoredPlayername(GameConstants.colorImportant),
            _serverClientService.GetClient(sourceClientId).ColoredPlayername(GameConstants.colorImportant), duration, reason));
        _serverPacketService.SendPacket(targetClientId, ServerPackets.DisconnectPlayer(
            string.Format(_languageService.Get("Server_CommandTimeIPBanNotification"), duration, reason)));
        _gameLogger.Server.Debug($"{sourceName} IP bans {targetName} for {duration} minutes.{reason}");
        server.KillPlayer(targetClientId);
        return true;
    }

    public bool BanOffline(int sourceClientId, string target, string reason = "")
    {
        if (!CheckPrivilege(sourceClientId, Privilege.ban_offline))
        {
            return false;
        }

        if (_serverClientService.GetClient(target) != null)
        {
            _serverPacketService.SendMessage(sourceClientId, string.Format(
                _languageService.Get("Server_CommandBanOfflineTargetOnline"), GameConstants.colorError, target));
            return false;
        }

        reason = FormatReason(reason);

        // If the player has a config entry, verify rank and remove it
        Client targetConfigEntry = _serverClientService.ServerClient.Clients.Find(
            c => c.Name.Equals(target, StringComparison.InvariantCultureIgnoreCase));

        if (targetConfigEntry != null)
        {
            Group targetGroup = _serverClientService.ServerClient.Groups.Find(g => g.Name.Equals(targetConfigEntry.Group));
            if (targetGroup == null)
            {
                _serverPacketService.SendMessage(sourceClientId, string.Format(
                    _languageService.Get("Server_CommandInvalidGroup"), GameConstants.colorError));
                return false;
            }

            ServerPlayer sourceClient = _serverClientService.GetClient(sourceClientId);
            if (targetGroup.IsSuperior(sourceClient.ClientGroup) || targetGroup.EqualLevel(sourceClient.ClientGroup))
            {
                _serverPacketService.SendMessage(sourceClientId, string.Format(
                    _languageService.Get("Server_CommandTargetUserSuperior"), GameConstants.colorError));
                return false;
            }

            _serverClientService.ServerClient.Clients.Remove(targetConfigEntry);
            _serverClientService.ServerClientNeedsSaving = true;
        }

        ServerPlayer source = _serverClientService.GetClient(sourceClientId);
        _serverClientService.BanList.BanPlayer(target, source.PlayerName, reason);
        SaveBanlist();
        server.SendMessageToAll(string.Format(_languageService.Get("Server_CommandBanOfflineMessage"),
            GameConstants.colorImportant, target, source.ColoredPlayername(GameConstants.colorImportant), reason));
        _gameLogger.Server.Debug($"{source.PlayerName} bans {target}.{reason}");
        return true;
    }

    public bool Unban(int sourceClientId, string type, string target)
    {
        if (!CheckPrivilege(sourceClientId, Privilege.unban))
        {
            return false;
        }

        if (type == "-p")
        {
            bool exists = _serverClientService.BanList.UnbanPlayer(target);
            SaveBanlist();
            if (!exists)
            {
                _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandPlayerNotFound"), GameConstants.colorError, target));
            }
            else
            {
                _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandUnbanSuccess"), GameConstants.colorSuccess, target));
                _gameLogger.Server.Debug($"{_serverClientService.GetClient(sourceClientId).PlayerName} unbans player {target}.");
            }

            return true;
        }

        if (type == "-ip")
        {
            bool exists = _serverClientService.BanList.UnbanIP(target);
            SaveBanlist();
            if (!exists)
            {
                _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandUnbanIPNotFound"), GameConstants.colorError, target));
            }
            else
            {
                _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandUnbanIPSuccess"), GameConstants.colorSuccess, target));
                _gameLogger.Server.Debug($"{_serverClientService.GetClient(sourceClientId).PlayerName} unbans IP {target}.");
            }

            return true;
        }

        _serverPacketService.SendMessage(sourceClientId, $"{GameConstants.colorError}Invalid type: {type}");
        return false;
    }

    // -------------------------------------------------------------------------
    // Banlist persistence
    // -------------------------------------------------------------------------
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public void LoadBanlist()
    {
        string path = Path.Combine(GameStorePath.gamepathconfig, BanlistFilename);

        if (!File.Exists(path))
        {
            Console.WriteLine(_languageService.ServerBanlistNotFound());
            SaveBanlist();
            return;
        }

        try
        {
            string json = File.ReadAllText(path);
            _serverClientService.BanList = JsonSerializer.Deserialize<ServerBanlist>(json, JsonOptions)
                             ?? new ServerBanlist();
        }
        catch
        {
            try
            {
                File.Copy(path, path + ".old");
                Console.WriteLine(_languageService.ServerBanlistCorrupt());
            }
            catch
            {
                Console.WriteLine(_languageService.ServerBanlistCorruptNoBackup());
            }

            _serverClientService.BanList = null;
            SaveBanlist();
            return;
        }

        SaveBanlist();
        Console.WriteLine(_languageService.ServerBanlistLoaded());
    }

    public void SaveBanlist()
    {
        Directory.CreateDirectory(GameStorePath.gamepathconfig);
        _serverClientService.BanList ??= new ServerBanlist();

        File.WriteAllText(
            Path.Combine(GameStorePath.gamepathconfig, BanlistFilename),
            JsonSerializer.Serialize(_serverClientService.BanList, JsonOptions));
    }

    // -------------------------------------------------------------------------
    // Shared guard helpers
    // -------------------------------------------------------------------------

    private bool CheckPrivilege(int sourceClientId, string privilege)
    {
        if (server.PlayerHasPrivilege(sourceClientId, privilege))
        {
            return true;
        }

        _serverPacketService.SendMessage(sourceClientId, string.Format(
            _languageService.Get("Server_CommandInsufficientPrivileges"), GameConstants.colorError));
        return false;
    }

    private bool CheckTargetRank(int sourceClientId, ServerPlayer target)
    {
        ServerPlayer source = _serverClientService.GetClient(sourceClientId);
        if (target.ClientGroup.IsSuperior(source.ClientGroup) || target.ClientGroup.EqualLevel(source.ClientGroup))
        {
            _serverPacketService.SendMessage(sourceClientId, string.Format(
                _languageService.Get("Server_CommandTargetUserSuperior"), GameConstants.colorError));
            return false;
        }

        return true;
    }

    private bool SendNonexistentId(int sourceClientId, int targetClientId)
    {
        _serverPacketService.SendMessage(sourceClientId, string.Format(
            _languageService.Get("Server_CommandNonexistantID"), GameConstants.colorError, targetClientId));
        return false;
    }

    private string FormatReason(string reason)
        => string.IsNullOrEmpty(reason) ? "" : _languageService.Get("Server_CommandKickBanReason") + reason + ".";

    private void BroadcastAndKick(int sourceClientId, int targetClientId,
        string broadcastKey, string notificationKey,
        string targetName, string sourceName, ServerPlayer target, string reason)
    {
        server.SendMessageToAll(string.Format(_languageService.Get(broadcastKey),
            GameConstants.colorImportant,
            target.ColoredPlayername(GameConstants.colorImportant),
            _serverClientService.GetClient(sourceClientId).ColoredPlayername(GameConstants.colorImportant),
            reason));
        _serverPacketService.SendPacket(targetClientId, ServerPackets.DisconnectPlayer(
            string.Format(_languageService.Get(notificationKey), reason)));
        server.KillPlayer(targetClientId);
    }
}