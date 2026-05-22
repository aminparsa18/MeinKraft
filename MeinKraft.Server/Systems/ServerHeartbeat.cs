using MeinKraft;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

/// <summary>
/// Sends a heartbeat to the public server list every 60 seconds when the server
/// is configured as public. The first heartbeat fires immediately on startup by
/// initialising the elapsed timer to the interval.
/// The received server hash is printed to the console once on first successful contact.
/// </summary>
public class ServerSystemHeartbeat : ServerSystem
{
    private const float HeartbeatInterval = 60f;
    private const string HashPrefix = "server=";

    private float elapsed;
    private bool hashPrinted;
    private readonly ServerHeartbeat heartbeat = new();
    private readonly ILanguageService _languageService;
    private readonly IClientRegistry _serverClientService;
    private readonly ServerConfig _config;

    public ServerSystemHeartbeat(IModEvents modEvents, ILanguageService languageService, IClientRegistry serverClientService,
        IOptions<ServerConfig> options) : base(modEvents)
    {
        _languageService = languageService;
        _serverClientService = serverClientService;
        _config = options.Value;
        // Pre-fill the timer so the first heartbeat fires on the first tick
        elapsed = HeartbeatInterval;
    }

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    protected override void OnUpdate(ServerGameService server, float dt)
    {
        elapsed += dt;
        while (elapsed >= HeartbeatInterval)
        {
            elapsed -= HeartbeatInterval;
            if (_config.Public)
            {
                heartbeat.GameMode = server.GameMode;
                ThreadPool.QueueUserWorkItem(async (_) => await SendHeartbeat(server));
            }
        }
    }

    // -------------------------------------------------------------------------
    // Heartbeat
    // -------------------------------------------------------------------------

    /// <summary>
    /// Populates the heartbeat payload from the current server state and dispatches
    /// it asynchronously. Bot players are excluded from the player list.
    /// Errors are logged briefly in release builds and in full in debug builds.
    /// </summary>
    public async Task SendHeartbeat(ServerGameService server)
    {
        if (_config?.Key == null)
        {
            return;
        }

        heartbeat.Name = _config.Name;
        heartbeat.MaxClients = _config.MaxClients;
        heartbeat.PasswordProtected = _config.IsPasswordProtected();
        heartbeat.AllowGuests = _config.AllowGuests;
        heartbeat.Port = _config.Port;
        heartbeat.Version = GameVersion.Version;
        heartbeat.Key = _config.Key;
        heartbeat.Motd = _config.Motd;

        List<string> playerNames = new();
        lock (_serverClientService.Clients)
        {
            foreach ((int _, ServerPlayer? client) in _serverClientService.Clients)
            {
                if (!client.IsBot)
                {
                    playerNames.Add(client.PlayerName);
                }
            }
        }

        heartbeat.Players = playerNames;
        heartbeat.UsersCount = playerNames.Count;

        try
        {
            //TODO: its not hosted yet
            // await heartbeat.SendHeartbeatAsync();
            //server.ReceivedKey = heartbeat.ReceivedKey;

            if (!hashPrinted)
            {
                Console.WriteLine($"hash: {StripHashPrefix(heartbeat.ReceivedKey)}");
                hashPrinted = true;
            }

            Console.WriteLine(_languageService.ServerHeartbeatSent());
        }
        catch (Exception e)
        {
#if DEBUG
            Console.WriteLine(e.ToString());
#endif
            Console.WriteLine("{0} ({1})", _languageService.ServerHeartbeatError(), e.Message);
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Strips the <c>"server="</c> prefix from the received key string, returning
    /// only the hash value. Returns an empty string if parsing fails.
    /// </summary>
    private static string StripHashPrefix(string hash)
    {
        try
        {
            int idx = hash?.IndexOf(HashPrefix) ?? -1;
            return idx >= 0 ? hash[(idx + HashPrefix.Length)..] : hash ?? "";
        }
        catch
        {
            return "";
        }
    }
}

// =============================================================================

/// <summary>
/// Encapsulates the heartbeat payload and the HTTP logic for posting it to the
/// public server list. The list URL is fetched once and cached for the lifetime
/// of the instance.
/// </summary>
public class ServerHeartbeat
{
    public string Name { get; set; } = "";
    public string Key { get; set; } = Guid.NewGuid().ToString();
    public int MaxClients { get; set; } = 16;
    public bool Public { get; set; } = true;
    public bool PasswordProtected { get; set; }
    public bool AllowGuests { get; set; } = true;
    public int Port { get; set; } = 25565;
    public string Version { get; set; } = "Unknown";
    public List<string> Players { get; set; } = [];
    public int UsersCount { get; set; }
    public string Motd { get; set; } = "";
    public string GameMode { get; set; }
    public string ReceivedKey { get; set; }

    private string listUrl;

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15),
        DefaultRequestHeaders =
        {
            CacheControl = new CacheControlHeaderValue { NoCache = true, NoStore = true }
        }
    };

    /// <summary>
    /// Posts the current heartbeat payload to the server list. The list endpoint
    /// URL is resolved once on first call and cached. <see cref="ReceivedKey"/>
    /// is populated with the server's response after a successful post.
    /// </summary>
    public async Task SendHeartbeatAsync()
    {
        listUrl ??= await HttpClient.GetStringAsync("http://manicdigger.sourceforge.net/heartbeat.txt");

        Dictionary<string, string> formData = new()
        {
            ["name"] = Name,
            ["max"] = MaxClients.ToString(),
            ["public"] = Public.ToString(),
            ["passwordProtected"] = PasswordProtected.ToString(),
            ["allowGuests"] = AllowGuests.ToString(),
            ["port"] = Port.ToString(),
            ["version"] = Version,
            ["fingerprint"] = Key.Replace("-", ""),
            ["users"] = UsersCount.ToString(),
            ["motd"] = Motd,
            ["gamemode"] = GameMode,
            ["players"] = string.Join(",", Players),
        };

        using FormUrlEncodedContent content = new(formData);
        using HttpResponseMessage response = await HttpClient.PostAsync(listUrl, content);
        response.EnsureSuccessStatusCode();
        ReceivedKey = await response.Content.ReadAsStringAsync();
    }
}