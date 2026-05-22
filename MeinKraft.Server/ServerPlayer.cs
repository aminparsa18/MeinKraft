using MeinKraft;
using OpenTK.Mathematics;

public class ServerPlayer
{
    public ServerPlayer()
    {
        Entity = new ServerEntity
        {
            DrawName = new ServerEntityDrawName { ClientAutoComplete = true },
            Position = new ServerEntityPositionAndOrientation { Pitch = 2 * 255 / 4 },
            DrawModel = new ServerEntityAnimatedModel { DownloadSkin = true }
        };

        Id = -1;
        State = ClientStateOnServer.Connecting;
        QueryClient = true;
        Ping = new Ping();
        PlayerName = GameConstants.InvalidPlayerName;
        Model = "player.txt";
        chunksseenTime = [];
        heightmapchunksseen = [];
        IsInventoryDirty = true;
        IsPlayerStatsDirty = true;
        FillLimit = 500;
        Privileges = [];
        DisplayColor = "&f";
        EyeHeight = 1f * 15 / 10;
        ModelHeight = 1f * 17 / 10;
        WindowSize = [800, 600];
        PlayersDirty = new bool[128];
        Array.Fill(PlayersDirty, true);
        SpawnedEntities = new ServerEntityId[64];
        UpdateEntity = new bool[SpawnedEntities.Length];
    }

    /// <summary>Unique client identifier. -1 when unassigned.</summary>
    public int Id { get; set; }

    /// <summary>Current connection state. See <see cref="ClientStateOnServer"/>.</summary>
    public int State { get; set; }

    /// <summary>Whether the server should query this client for info.</summary>
    public bool QueryClient { get; set; }

    /// <summary>The main server socket this client is connected to.</summary>
    public NetServer MainSocket { get; set; }

    /// <summary>The client's individual network connection.</summary>
    public NetConnection Socket { get; set; }

    /// <summary>Handles ping tracking for this client.</summary>
    public Ping Ping { get; set; }

    /// <summary>Last recorded ping value in seconds.</summary>
    public float LastPing { get; set; }

    /// <summary>The player's display name.</summary>
    public string PlayerName
    {
        get => Entity.DrawName.Name;
        set => Entity.DrawName.Name = value;
    }

    /// <summary>X position multiplied by 32 for network precision.</summary>
    public int PositionMul32GlX
    {
        get => (int)(Entity.Position.X * 32);
        set => Entity.Position.X = value / 32f;
    }

    /// <summary>Y position multiplied by 32 for network precision.</summary>
    public int PositionMul32GlY
    {
        get => (int)(Entity.Position.Y * 32);
        set => Entity.Position.Y = value / 32f;
    }

    /// <summary>Z position multiplied by 32 for network precision.</summary>
    public int PositionMul32GlZ
    {
        get => (int)(Entity.Position.Z * 32);
        set => Entity.Position.Z = value / 32f;
    }

    /// <summary>Horizontal facing direction encoded as 0-255.</summary>
    public int PositionHeading
    {
        get => Entity.Position.Heading;
        set => Entity.Position.Heading = (byte)value;
    }

    /// <summary>Vertical look angle encoded as 0-255.</summary>
    public int PositionPitch
    {
        get => Entity.Position.Pitch;
        set => Entity.Position.Pitch = (byte)value;
    }
    internal ServerTimer notifyMonstersTimer;

    /// <summary>Player stance (standing, crouching, etc).</summary>
    public byte Stance
    {
        get => Entity.Position.Stance;
        set => Entity.Position.Stance = value;
    }

    /// <summary>Model filename used to render this player.</summary>
    public string Model
    {
        get => Entity.DrawModel.Model;
        set => Entity.DrawModel.Model = value;
    }

    /// <summary>Texture filename applied to this player's model.</summary>
    public string Texture
    {
        get => Entity.DrawModel.Texture;
        set => Entity.DrawModel.Texture = value;
    }

    /// <summary>Tracks when each chunk was last seen by this client, keyed by chunk index.</summary>
    public Dictionary<int, int> chunksseenTime { get; set; }

    /// <summary>Flags indicating which chunks have been sent to this client.</summary>
    public bool[] chunksseen { get; set; }

    /// <summary>Tracks when each heightmap chunk was last seen, keyed by 2D chunk position.</summary>
    public Dictionary<Vector2i, int> heightmapchunksseen { get; set; }

    /// <summary>Timer controlling map chunk notification intervals.</summary>
    public ServerTimer NotifyMapTimer { get; set; }

    /// <summary>Whether the client's inventory needs to be sent.</summary>
    public bool IsInventoryDirty { get; set; }

    /// <summary>Whether the client's player stats need to be sent.</summary>
    public bool IsPlayerStatsDirty { get; set; }

    /// <summary>Maximum number of blocks this client can fill at once.</summary>
    public int FillLimit { get; set; }

    /// <summary>The permission group this client belongs to.</summary>
    public Group ClientGroup { get; set; }

    /// <summary>Whether this client is a bot.</summary>
    public bool IsBot { get; set; }

    /// <summary>Assigns a group to this client, updating privileges and display color.</summary>
    public void AssignGroup(Group newGroup)
    {
        ClientGroup = newGroup;
        Privileges.Clear();
        Privileges.AddRange(newGroup.GroupPrivileges);
        color = newGroup.GroupColorString();
    }

    /// <summary>List of privilege identifiers granted to this client.</summary>
    public List<string> Privileges { get; set; }

    private string color;

    /// <summary>Color code prefix applied to this player's display name.</summary>
    public string DisplayColor
    {
        get => Entity.DrawName.Color;
        set => Entity.DrawName.Color = value;
    }

    /// <summary>Returns the player's name wrapped in their group color, followed by a reset color.</summary>
    public string ColoredPlayername(string subsequentColor)
        => $"{color}{PlayerName}{subsequentColor}";

    /// <summary>Timer controlling monster notification intervals.</summary>
    public ServerTimer NotifyMonstersTimer { get; set; }

    /// <summary>Script interpreter instance for mod/script execution.</summary>
    public IScriptInterpreter Interpreter { get; set; }

    /// <summary>Script console for this client's scripting context.</summary>
    public ScriptConsole Console { get; set; }

    /// <summary>Returns a formatted string: PlayerName:Group:Privileges IP</summary>
    public override string ToString()
    {
        string ip = Socket?.RemoteEndPoint().AddressToString() ?? "";
        return $"{PlayerName}:{ClientGroup.Name}:{ServerClientMisc.PrivilegesString(Privileges)} {ip}";
    }

    /// <summary>Eye height in world units used for camera positioning.</summary>
    internal float EyeHeight
    {
        get => Entity.DrawModel.EyeHeight;
        set => Entity.DrawModel.EyeHeight = value;
    }

    /// <summary>Full model height in world units used for collision.</summary>
    internal float ModelHeight
    {
        get => Entity.DrawModel.ModelHeight;
        set => Entity.DrawModel.ModelHeight = value;
    }

    /// <summary>Currently selected material/block slot in the hotbar.</summary>
    internal int ActiveMaterialSlot { get; set; }

    /// <summary>Whether this client is in spectator mode.</summary>
    internal bool IsSpectator { get; set; }

    /// <summary>Whether this client is currently performing a fill operation.</summary>
    internal bool UsingFill { get; set; }

    /// <summary>Client window dimensions in pixels [width, height].</summary>
    internal int[] WindowSize { get; set; }

    /// <summary>Accumulator for player position notification throttling.</summary>
    internal float NotifyPlayerPositionsAccum { get; set; }

    /// <summary>Dirty flags for each player slot, indicating which need to be re-sent.</summary>
    internal bool[] PlayersDirty { get; set; }

    /// <summary>The server-side entity representation of this client.</summary>
    internal ServerEntity Entity { get; set; }

    /// <summary>Overrides the entity's position if set, used for teleportation or correction.</summary>
    internal ServerEntityPositionAndOrientation PositionOverride { get; set; }

    /// <summary>Accumulator for entity notification throttling.</summary>
    internal float NotifyEntitiesAccum { get; set; }

    /// <summary>Array of entity IDs that have been spawned for this client.</summary>
    internal ServerEntityId[] SpawnedEntities { get; set; }

    /// <summary>The sign entity this client is currently editing, if any.</summary>
    internal ServerEntityId EditingSign { get; set; }

    /// <summary>Flags indicating which spawned entities need to be updated for this client.</summary>
    internal bool[] UpdateEntity { get; set; }
}