/// <summary>
/// Concrete implementation of <see cref="IServerConfig"/>. Serialised to and
/// from XML (typically <c>ServerConfig.xml</c>) so settings persist across
/// server restarts. Construct with defaults via <see cref="ServerConfig()"/>,
/// then call <see cref="CopyFrom"/> to apply a freshly deserialised config
/// without replacing the registered singleton reference.
/// </summary>
public class ServerConfig
{
    public string Name { get; set; } = "MeinKraft server";
    public string Motd { get; set; } = "MOTD";
    public string WelcomeMessage { get; set; } = "Welcome to my MeinKraft server!";
    public int Port { get; set; } = 25565;
    public int MaxClients { get; set; } = 16;
    public int AutoRestartCycle { get; set; } = 6;
    public bool ServerMonitor { get; set; } = true;
    public int ClientConnectionTimeout { get; set; } = 600;
    public int ClientPlayingTimeout { get; set; } = 60;
    public bool BuildLogging { get; set; }
    public bool ServerEventLogging { get; set; }
    public bool ChatLogging { get; set; }
    public bool AllowScripting { get; set; }
    public string Key { get; set; } = Guid.NewGuid().ToString();
    public bool IsCreative { get; set; } = true;
    public bool Public { get; set; } = true;
    public string? Password { get; set; }
    public bool AllowGuests { get; set; } = true;
    public bool Monsters { get; set; }
    public int MapSizeX { get; set; } = 9984;
    public int MapSizeY { get; set; } = 9984;
    public int MapSizeZ { get; set; } = 128;
    public List<AreaConfig> Areas { get; set; } = [];
    public int Seed { get; set; }
    public bool RandomSeed { get; set; } = true;
    public bool EnableHTTPServer { get; set; }
    public bool AllowSpectatorUse { get; set; }
    public bool AllowSpectatorBuild { get; set; }
    public string ServerLanguage { get; set; } = "en";
    public int PlayerDrawDistance { get; set; } = 128;
    public bool EnablePlayerPushing { get; set; } = true;

    public bool IsPasswordProtected() => !string.IsNullOrEmpty(Password);

    public bool CanUserBuild(ServerPlayer client, int x, int y, int z)
    {
        foreach (AreaConfig area in Areas)
        {
            if (area.IsInCoords(x, y, z) && area.CanUserBuild(client))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// Defines a rectangular bounding box within the world in which build permissions
/// are restricted to specific groups or players. Areas are evaluated in order;
/// the first matching area that grants permission wins.
/// </summary>
public class AreaConfig
{
    /// <summary>Unique numeric identifier for this area. <c>-1</c> means unassigned.</summary>
    public int Id { get; set; }

    private int x1, x2, y1, y2, z1, z2;
    private string coords;

    /// <summary>
    /// Names of groups whose members are unconditionally allowed to build in this
    /// area, regardless of their group level.
    /// </summary>
    public List<string> PermittedGroups { get; set; }

    /// <summary>
    /// Usernames of individual players who are unconditionally allowed to build
    /// in this area, regardless of their group.
    /// </summary>
    public List<string> PermittedUsers { get; set; }

    /// <summary>
    /// Minimum group level required to build in this area. Players whose group
    /// level is greater than or equal to this value are permitted even if their
    /// group or username is not explicitly listed.
    /// <see langword="null"/> means no level-based bypass is applied.
    /// </summary>
    public int? Level { get; set; }

    /// <summary>
    /// Initialises a new <see cref="AreaConfig"/> with default coordinates
    /// (origin to origin) and empty permission lists.
    /// </summary>
    public AreaConfig()
    {
        Id = -1;
        Coords = "0,0,0,0,0,0";
        PermittedGroups = [];
        PermittedUsers = [];
    }

    /// <summary>
    /// Gets or sets the bounding box as a comma-separated string in the format
    /// <c>x1,y1,z1,x2,y2,z2</c>. Setting this property immediately parses and
    /// caches the six individual coordinate values.
    /// </summary>
    public string Coords
    {
        get => coords;
        set
        {
            coords = value;
            string[] parts = value.Split(',');
            x1 = Convert.ToInt32(parts[0]);
            x2 = Convert.ToInt32(parts[3]);
            y1 = Convert.ToInt32(parts[1]);
            y2 = Convert.ToInt32(parts[4]);
            z1 = Convert.ToInt32(parts[2]);
            z2 = Convert.ToInt32(parts[5]);
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> if the point
    /// (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>) lies
    /// within or on the boundary of this area.
    /// </summary>
    public bool IsInCoords(int x, int y, int z)
        => x >= x1 && x <= x2 && y >= y1 && y <= y2 && z >= z1 && z <= z2;

    /// <summary>
    /// Returns <see langword="true"/> if the axis-aligned bounding box defined by
    /// the given min/max corners is entirely contained within this area.
    /// </summary>
    public bool ContainsBox(int minX, int minY, int minZ, int maxX, int maxY, int maxZ)
        => minX >= x1 && maxX <= x2 && minY >= y1 && maxY <= y2 && minZ >= z1 && maxZ <= z2;

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="client"/> is permitted to
    /// build in this area based on group level, group name, or explicit username.
    /// </summary>
    /// <param name="client">The player requesting build permission.</param>
    public bool CanUserBuild(ServerPlayer client)
    {
        if (Level != null && client.ClientGroup.Level >= Level)
            return true;

        if (PermittedGroups.Contains(client.ClientGroup.Name))
            return true;

        if (PermittedUsers.Any(u => u.Equals(client.PlayerName, StringComparison.InvariantCultureIgnoreCase)))
            return true;

        return false;
    }

    /// <summary>
    /// Returns a compact string representation of this area in the format
    /// <c>id:coords:groups:users:level</c>, suitable for logging.
    /// </summary>
    public override string ToString()
    {
        string groups = string.Join(",", PermittedGroups);
        string users = string.Join(",", PermittedUsers);
        string level = Level?.ToString() ?? "";
        return $"{Id}:{Coords}:{groups}:{users}:{level}";
    }

    /// <summary>
    /// Returns the default two-area setup used when no <c>ServerConfig.xml</c>
    /// exists: a public guest-accessible zone covering the lower half of the map,
    /// and a protected builder/moderator/admin zone covering the full map height.
    /// </summary>
    public static List<AreaConfig> GetDefaultAreas()
    {
        AreaConfig publicArea = new() { Id = 1, Coords = "0,0,1,9984,5000,128" };
        publicArea.PermittedGroups.Add("Guest");

        AreaConfig protectedArea = new() { Id = 2, Coords = "0,0,1,9984,9984,128" };
        protectedArea.PermittedGroups.Add("Builder");
        protectedArea.PermittedGroups.Add("Moderator");
        protectedArea.PermittedGroups.Add("Admin");

        return [publicArea, protectedArea];
    }
}