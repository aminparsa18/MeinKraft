using MeinKraft;
using OpenTK.Mathematics;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Server system responsible for loading and saving <c>ServerClient.txt</c>, which
/// stores player groups, registered client entries, and the default world spawn point.
/// <para>
/// On first run, if no file exists, defaults are generated and written to disk.
/// Like <c>ServerConfig.txt</c>, the file supports a legacy XML format that is
/// automatically upgraded to the current serializer format on load.
/// </para>
/// </summary>
public class ServerSystemLoadServerClient : ServerSystem
{
    private const string ClientFilename = "ServerClient.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IServerMapStorage _serverMapStorage;
    private readonly ILanguageService _languageService;
    private readonly IClientRegistry _serverClientService;
    private readonly ServerGameService server;

    public ServerSystemLoadServerClient(ServerGameService server, IModEvents modEvents, IServerMapStorage serverMapStorage, ILanguageService languageService, IClientRegistry serverClientService) : base(modEvents)
    {
        this.server = server;
        _serverMapStorage = serverMapStorage;
        _languageService = languageService;
        _serverClientService = serverClientService;
    }

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    protected override void Initialize() => LoadServerClient();

    /// <inheritdoc/>
    protected override void OnUpdate(ServerGameService server, float dt)
    {
        if (_serverClientService.ServerClientNeedsSaving)
        {
            _serverClientService.ServerClientNeedsSaving = false;
            SaveServerClient();
        }
    }

    // -------------------------------------------------------------------------
    // Load
    // -------------------------------------------------------------------------

    /// <summary>
    /// Loads <c>ServerClient.txt</c> from the game config directory, then resolves
    /// the default player spawn point and the default guest/registered groups.
    /// <list type="bullet">
    ///   <item>If the file does not exist, defaults are written and the method continues.</item>
    ///   <item>If the file uses the current XML format it is deserialized directly.</item>
    ///   <item>If the file uses the legacy format, known fields are read via XPath and
    ///         the file is immediately re-saved in the current format.</item>
    /// </list>
    /// </summary>
    /// <exception cref="Exception">
    /// Thrown if the guest or registered default group name in the config does not
    /// match any group defined in <c>ServerClient.txt</c>.
    /// </exception>
    public void LoadServerClient()
    {
        string path = Path.Combine(GameStorePath.gamepathconfig, ClientFilename);

        if (!File.Exists(path))
        {
            Console.WriteLine(_languageService.ServerClientConfigNotFound());
            SaveServerClient();
        }
        else
        {
            TryLoadCurrentFormat(path);
        }

        ResolveSpawn();
        ResolveDefaultGroups();
        Console.WriteLine(_languageService.ServerClientConfigLoaded());
    }

    /// <summary>
    /// Attempts to deserialize <c>ServerClient.txt</c> using the current
    /// <see cref="XmlSerializer"/> format. Groups are sorted after a successful load.
    /// </summary>
    /// <returns><c>true</c> on success; <c>false</c> if deserialization fails.</returns>
    private bool TryLoadCurrentFormat(string path)
    {
        try
        {
            string json = File.ReadAllText(path);
            _serverClientService.ServerClient = JsonSerializer.Deserialize<ServerRoster>(json, JsonOptions)
                                  ?? new ServerRoster();
            _serverClientService.ServerClient.Groups.Sort();
            SaveServerClient();
            return true;
        }
        catch
        {
            return false;
        }
    }

    // -------------------------------------------------------------------------
    // Spawn resolution
    // -------------------------------------------------------------------------

    /// <summary>
    /// Resolves <see cref="ServerGameService.DefaultPlayerSpawn"/> from the loaded config.
    /// <list type="bullet">
    ///   <item>If no spawn is configured, the spawn is placed at the surface above
    ///         the centre of the map.</item>
    ///   <item>If a spawn is configured but has no Z component, the surface height
    ///         at the (X, Y) position is used.</item>
    ///   <item>If all three coordinates are present, they are used verbatim.</item>
    /// </list>
    /// When no configured spawn exists, <see cref="ServerGameService.DontSpawnPlayerInWater"/>
    /// is applied to push the position above any water surface.
    /// </summary>
    private void ResolveSpawn()
    {
        if (_serverClientService.ServerClient.DefaultSpawn == null)
        {
            int x = _serverMapStorage.MapSizeX / 2;
            int y = _serverMapStorage.MapSizeY / 2;
            int z = VectorUtils.BlockHeight(_serverMapStorage, 0, x, y);
            server.DefaultPlayerSpawn = server.DontSpawnPlayerInWater(new Vector3i(x, y, z));
            return;
        }

        Spawn spawn = _serverClientService.ServerClient.DefaultSpawn;
        int spawnZ = spawn.z ?? VectorUtils.BlockHeight(_serverMapStorage, 0, spawn.x, spawn.y);
        server.DefaultPlayerSpawn = new Vector3i(spawn.x, spawn.y, spawnZ);
    }

    // -------------------------------------------------------------------------
    // Group resolution
    // -------------------------------------------------------------------------

    /// <summary>
    /// Resolves <see cref="ServerGameService.DefaultGroupGuest"/> and
    /// <see cref="ServerGameService.DefaultGroupRegistered"/> by matching the group names
    /// stored in the config against the loaded group list.
    /// </summary>
    /// <exception cref="Exception">
    /// Thrown if either group name cannot be found, indicating a misconfigured
    /// <c>ServerClient.txt</c>.
    /// </exception>
    private void ResolveDefaultGroups()
    {
        server.DefaultGroupGuest = _serverClientService.ServerClient.Groups
            .Find(g => g.Name.Equals(_serverClientService.ServerClient.DefaultGroupGuests))
            ?? throw new Exception(_languageService.ServerClientConfigGuestGroupNotFound());

        server.DefaultGroupRegistered = _serverClientService.ServerClient.Groups
            .Find(g => g.Name.Equals(_serverClientService.ServerClient.DefaultGroupRegistered))
            ?? throw new Exception(_languageService.ServerClientConfigRegisteredGroupNotFound());
    }

    // -------------------------------------------------------------------------
    // Save
    // -------------------------------------------------------------------------

    /// <summary>
    /// Serializes <see cref="ServerGameService.ServerClient"/> to <c>ServerClient.txt</c>.
    /// If the object has not yet been initialized, or if its groups or client
    /// lists are empty, defaults are populated before saving.
    /// </summary>
    public void SaveServerClient()
    {
        Directory.CreateDirectory(GameStorePath.gamepathconfig);

        _serverClientService.ServerClient ??= new ServerRoster();

        if (_serverClientService.ServerClient.Groups.Count == 0)
        {
            _serverClientService.ServerClient.Groups = ServerClientMisc.GetDefaultGroups();
        }

        if (_serverClientService.ServerClient.Clients.Count == 0)
        {
            _serverClientService.ServerClient.Clients = ServerClientMisc.GetDefaultClients();
        }

        _serverClientService.ServerClient.Clients.Sort();

        File.WriteAllText(
            Path.Combine(GameStorePath.gamepathconfig, ClientFilename),
            JsonSerializer.Serialize(_serverClientService.ServerClient, JsonOptions));
    }
}