using MeinKraft;

/// <summary>
/// Server system that manages Permission Sign entities. A Permission Sign defines
/// a 3D area in the world that restricts block interactions to a specific player
/// or group. Signs are placed with the PermissionSign tool and configured via an
/// in-game dialog. Permission checks are evaluated against all signs in the 3×3×3
/// chunk neighbourhood around the block being acted on.
/// </summary>
public class ServerSystemPermissionSign : ServerSystem
{
    private ServerGameService server;

    private const int AreaSize = 32;
    private const int GroupButtonOffsetY = 150;
    private const int GroupButtonStepY = 50;
    private const string GroupIdPrefix = "PermissionSignGroup";
    private const string OkWidgetId = "UsePermissionSign_OK";
    private const string DialogKey = "UseSign";

    private readonly IServerModManager _serverModManager;
    private readonly IServerMapStorage _serverMapStorage;
    private readonly ILanguageService _languageService;
    private readonly IClientRegistry _serverClientService;
    private readonly IServerPacketService _serverPacketService;

    public ServerSystemPermissionSign(IModEvents modEvents, IServerModManager serverModManager, IServerMapStorage serverMapStorage, IClientRegistry serverClientService, 
        ILanguageService languageService, IServerPacketService serverPacketService) : base(modEvents)
    {
        _serverModManager = serverModManager;
        _serverMapStorage = serverMapStorage;
        _serverClientService = serverClientService;
        _languageService = languageService;
        _serverPacketService = serverPacketService;
    }

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    protected override void Initialize()
    {
        ModEvents.BlockUseWithTool += OnUseWithTool;
        ModEvents.UpdateEntity += UpdateEntity;
        ModEvents.UseEntity += OnUseEntity;
        ModEvents.DialogClick2 += OnDialogClick;
        ModEvents.Permission += OnPermission;
    }

    // -------------------------------------------------------------------------
    // Placement
    // -------------------------------------------------------------------------

    /// <summary>
    /// Handles using the PermissionSign tool on a block. Places a new
    /// <see cref="ServerEntityPermissionSign"/> entity at the target position,
    /// facing the placing player, defaulting to the "Admin" group.
    /// </summary>
    private void OnUseWithTool(BlockUseWithToolArgs args)
    {
        if (_serverModManager.GetBlockName(args.Tool) != "PermissionSign")
            return;

        if (_serverMapStorage.GetChunk(args.X, args.Y, args.Z) == null)
            return;

        if (!server.CheckBuildPrivileges(args.Player, args.X, args.Y, args.Z, PacketBlockSetMode.Create))
            return;

        if (!CheckAreaPrivilege(args.Player))
            return;

        ServerEntity e = new()
        {
            Position = new ServerEntityPositionAndOrientation
            {
                X = args.X + (1f / 2),
                Y = args.Z,
                Z = args.Y + (1f / 2)
            },
            PermissionSign = new ServerEntityPermissionSign
            {
                Name = "Admin",
                Type = PermissionSignType.Group
            }
        };

        e.Position.Heading = EntityHeading.GetHeading(
            _serverModManager.GetPlayerPositionX(args.Player),
            _serverModManager.GetPlayerPositionY(args.Player),
            e.Position.X, e.Position.Z);

        server.AddEntity(args.X, args.Y, args.Z, e);
    }

    // -------------------------------------------------------------------------
    // Entity update (visual + area)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Runs each tick for every entity. Updates the visual model, display text,
    /// and restricted draw area for permission sign entities. The area extends
    /// in the direction the sign faces, determined by the entity's heading.
    /// </summary>
    private void UpdateEntity(UpdateEntityArgs args)
    {
        ServerEntity e = server.GetEntity(args.ChunkX, args.ChunkY, args.ChunkZ, args.Id);
        if (e.PermissionSign == null)
            return;

        e.DrawModel ??= new ServerEntityAnimatedModel();
        e.DrawModel.Model = "signmodel.txt";
        e.DrawModel.Texture = "permissionsignmodel.png";
        e.DrawModel.ModelHeight = 1f * 13 / 10;

        e.DrawText ??= new ServerEntityDrawText();
        e.DrawText.Text = e.PermissionSign.Type == PermissionSignType.Group
            ? $"&4{e.PermissionSign.Name}"
            : e.PermissionSign.Name;
        e.DrawText.Dx = 1f * 3 / 32;
        e.DrawText.Dy = 1f * 36 / 32;
        e.DrawText.Dz = 1f * 3 / 32;

        e.Usable = true;
        e.DrawName ??= new ServerEntityDrawName { Name = "Permission Sign", OnlyWhenSelected = true };
        e.DrawArea ??= new ServerEntityDrawArea();
        UpdateDrawArea(e);
    }

    /// <summary>
    /// Positions the draw area based on the sign's heading. The area always
    /// extends one full <see cref="AreaSize"/> in the direction the sign faces.
    /// </summary>
    private static void UpdateDrawArea(ServerEntity e)
    {
        int px = (int)e.Position.X;
        int py = (int)e.Position.Y;
        int pz = (int)e.Position.Z;
        int half = AreaSize / 2;

        // Quantise heading into 4 cardinal directions (north/east/south/west)
        int rotDir = (byte)(e.Position.Heading + (255 / 8)) / 64;
        e.DrawArea.X = rotDir switch
        {
            1 => px,
            3 => px - AreaSize,
            _ => px - half
        };
        e.DrawArea.Y = py - half;
        e.DrawArea.Z = rotDir switch
        {
            0 => pz - AreaSize,
            2 => pz,
            _ => pz - half
        };
        e.DrawArea.SizeX = AreaSize;
        e.DrawArea.SizeY = AreaSize;
        e.DrawArea.SizeZ = AreaSize;
    }

    // -------------------------------------------------------------------------
    // Dialog — open
    // -------------------------------------------------------------------------

    /// <summary>
    /// Opens the permission sign configuration dialog for the interacting player.
    /// The dialog contains a text box for entering a player name, a "Set player"
    /// button, and one button per server group.
    /// </summary>
    private void OnUseEntity(UseEntityArgs args)
    {
        ServerEntity e = server.GetEntity(args.ChunkX, args.ChunkY, args.ChunkZ, args.Id);
        if (e.PermissionSign == null)
            return;

        if (!CheckAreaPrivilege(args.Player))
            return;

        DialogFont font = new("Verdana", 11f, DialogFontStyle.Bold);
        List<Group> groups = _serverClientService.ServerClient.Groups;
        int widgetCount = 0;

        Dialog d = new()
        {
            Width = 400,
            Height = 400,
            IsModal = true,
            Widgets = new Widget[4 + (groups.Count * 2)]
        };

        d.Widgets[widgetCount++] = Widget.MakeSolid(0, 0, 400, 400, ColorUtils.ColorFromArgb(255, 50, 50, 50));
        d.Widgets[widgetCount++] = Widget.MakeTextBox(e.PermissionSign.Name, font, 50, 50, 200, 50, ColorUtils.ColorFromArgb(255, 0, 0, 0));

        for (int i = 0; i < groups.Count; i++)
        {
            int buttonY = GroupButtonOffsetY + (i * GroupButtonStepY);
            Widget groupButton = Widget.MakeSolid(50, buttonY, 100, 40, ColorUtils.ColorFromArgb(255, 100, 100, 100));
            groupButton.ClickKey = (char)13;
            groupButton.Id = GroupIdPrefix + groups[i].Name;
            d.Widgets[widgetCount++] = groupButton;
            d.Widgets[widgetCount++] = Widget.MakeText(groups[i].Name, font, 50, buttonY, ColorUtils.ColorFromArgb(255, 0, 0, 0));
        }

        Widget okButton = Widget.MakeSolid(200, 50, 100, 50, ColorUtils.ColorFromArgb(255, 100, 100, 100));
        okButton.ClickKey = (char)13;
        okButton.Id = OkWidgetId;
        d.Widgets[widgetCount++] = okButton;
        d.Widgets[widgetCount++] = Widget.MakeText("Set player", font, 200, 50, ColorUtils.ColorFromArgb(255, 0, 0, 0));

        _serverClientService.Clients[args.Player].EditingSign = new ServerEntityId
        {
            ChunkX = args.ChunkX,
            ChunkY = args.ChunkY,
            ChunkZ = args.ChunkZ,
            Id = args.Id
        };
        server.SendDialog(args.Player, DialogKey, d);
    }

    // -------------------------------------------------------------------------
    // Dialog — click
    // -------------------------------------------------------------------------

    /// <summary>
    /// Handles a button click in the permission sign dialog.
    /// <list type="bullet">
    ///   <item><c>UsePermissionSign_OK</c> — sets the sign to the typed player name,
    ///         promoting to group type if the name matches an existing group.</item>
    ///   <item><c>PermissionSignGroup*</c> — sets the sign directly to the clicked group.</item>
    /// </list>
    /// An empty name despawns the sign; a non-empty name updates and marks it dirty.
    /// </summary>
    private void OnDialogClick(DialogClick2Args args)
    {
        if (!TryResolveDialogTarget(args, out string name, out PermissionSignType type))
        {
            return;
        }

        ServerPlayer client = _serverClientService.Clients[args.Player];
        ServerEntityId id = client.EditingSign;
        client.EditingSign = null;

        if (!string.IsNullOrEmpty(name))
        {
            ServerEntity e = server.GetEntity(id.ChunkX, id.ChunkY, id.ChunkZ, id.Id);
            e.PermissionSign.Name = name;
            e.PermissionSign.Type = type;
            server.SetEntityDirty(id);
        }
        else
        {
            server.DespawnEntity(id);
        }

        server.SendDialog(args.Player, DialogKey, null);
    }

    /// <summary>
    /// Parses the clicked widget ID to determine the target name and type.
    /// Returns <c>false</c> if the widget belongs to an unrelated dialog.
    /// </summary>
    private bool TryResolveDialogTarget(DialogClick2Args args, out string name, out PermissionSignType type)
    {
        name = null;
        type = PermissionSignType.Player;

        if (args.WidgetId == OkWidgetId)
        {
            string candidate = args.TextBoxValue[1];
            Group matchedGroup = _serverClientService.ServerClient.Groups
                .FirstOrDefault(g => g.Name == candidate);
            name = candidate;
            if (matchedGroup != null)
            {
                type = PermissionSignType.Group;
            }

            return true;
        }

        if (args.WidgetId.StartsWith(GroupIdPrefix))
        {
            Group matchedGroup = _serverClientService.ServerClient.Groups
                .FirstOrDefault(g => GroupIdPrefix + g.Name == args.WidgetId);
            if (matchedGroup == null)
            {
                return false;
            }

            name = matchedGroup.Name;
            type = PermissionSignType.Group;
            return true;
        }

        return false;
    }

    // -------------------------------------------------------------------------
    // Permission check
    // -------------------------------------------------------------------------

    /// <summary>
    /// Evaluates all permission signs in the 3×3×3 chunk neighbourhood around
    /// the block position in <paramref name="args"/>. If any sign's draw area
    /// contains the block and its assigned player or group matches the acting
    /// player, permission is granted and the check short-circuits.
    /// </summary>
    private void OnPermission(PermissionArgs args)
    {
        int blockX = args.X;
        int blockY = args.Y;
        int blockZ = args.Z;

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    int cx = (blockX / GameConstants.ServerChunkSize) + dx;
                    int cy = (blockY / GameConstants.ServerChunkSize) + dy;
                    int cz = (blockZ / GameConstants.ServerChunkSize) + dz;

                    if (!VectorUtils.IsValidChunkPos(_serverMapStorage, cx, cy, cz, GameConstants.ServerChunkSize))
                    {
                        continue;
                    }

                    ServerChunk chunk = _serverMapStorage.GetChunkAt(cx, cy, cz);
                    if (chunk == null)
                    {
                        return;
                    }

                    foreach ((int _, ServerEntity? entity) in chunk.Entities)
                    {
                        if (entity?.PermissionSign == null || entity.DrawArea == null)
                        {
                            continue;
                        }

                        if (!InArea(blockX, blockY, blockZ,
                                entity.DrawArea.X, entity.DrawArea.Z, entity.DrawArea.Y,
                                entity.DrawArea.SizeX, entity.DrawArea.SizeZ, entity.DrawArea.SizeY))
                        {
                            continue;
                        }

                        ServerPlayer client = _serverClientService.Clients[args.Player];

                        bool allowed = entity.PermissionSign.Type switch
                        {
                            PermissionSignType.Group => entity.PermissionSign.Name == client.ClientGroup.Name,
                            PermissionSignType.Player => entity.PermissionSign.Name == client.PlayerName,
                            _ => false
                        };

                        if (allowed)
                        {
                            args.Allowed = true;
                            return;
                        }
                    }
                }
            }
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Checks whether <paramref name="player"/> has the <c>area_add</c> privilege,
    /// sending an error message and returning <c>false</c> if not.
    /// </summary>
    private bool CheckAreaPrivilege(int player)
    {
        if (server.PlayerHasPrivilege(player, Privilege.area_add))
        {
            return true;
        }

        _serverPacketService.SendMessage(player,
            GameConstants.colorError + _languageService.Get("Server_CommandInsufficientPrivileges"));
        return false;
    }

    /// <summary>
    /// Returns <c>true</c> if the point (<paramref name="x"/>, <paramref name="y"/>,
    /// <paramref name="z"/>) lies within the axis-aligned box defined by the given
    /// origin and size.
    /// </summary>
    private static bool InArea(int x, int y, int z,
        int areaX, int areaY, int areaZ,
        int areaSizeX, int areaSizeY, int areaSizeZ)
        => x >= areaX && x < areaX + areaSizeX &&
        y >= areaY && y < areaY + areaSizeY &&
        z >= areaZ && z < areaZ + areaSizeZ;
}