using MeinKraft;

public class ServerSystemSign : ServerSystem
{
    private ServerGameService server;
    private readonly IServerModManager _modManager;
    private readonly IServerMapStorage _serverMapStorage;
    private readonly IClientRegistry _serverClientService;

    public ServerSystemSign(IModEvents modEvents, IServerModManager modManager, IServerMapStorage serverMapStorage, IClientRegistry serverClientService) : base(modEvents)
    {
        _modManager = modManager;
        _serverMapStorage = serverMapStorage;
        _serverClientService = serverClientService;
    }

    protected override void Initialize()
    {
        ModEvents.BlockUseWithTool += OnUseWithTool;
        ModEvents.UpdateEntity += UpdateEntity;
        ModEvents.UseEntity += OnUseEntity;
        ModEvents.DialogClick2 += OnDialogClick;
    }

    private void OnUseWithTool(BlockUseWithToolArgs args)
    {
        if (_modManager.GetBlockName(args.Tool) != "Sign")
            return;

        if (_serverMapStorage.GetChunk(args.X, args.Y, args.Z) == null)
            return;

        if (!server.CheckBuildPrivileges(args.Player, args.X, args.Y, args.Z, PacketBlockSetMode.Create))
            return;

        ServerEntity e = new()
        {
            Position = new ServerEntityPositionAndOrientation
            {
                X = args.X + (1f / 2),
                Y = args.Z,
                Z = args.Y + (1f / 2)
            },
            Sign = new ServerEntitySign { Text = "Hello world!" }
        };

        e.Position.Heading = EntityHeading.GetHeading(
            _modManager.GetPlayerPositionX(args.Player),
            _modManager.GetPlayerPositionY(args.Player),
            e.Position.X,
            e.Position.Z
        );

        server.AddEntity(args.X, args.Y, args.Z, e);
    }

    private void UpdateEntity(UpdateEntityArgs args)
    {
        ServerEntity e = server.GetEntity(args.ChunkX, args.ChunkY, args.ChunkZ, args.Id);
        if (e.Sign == null)
            return;

        e.DrawModel ??= new ServerEntityAnimatedModel();
        e.DrawModel.Model = "signmodel.txt";
        e.DrawModel.Texture = "signmodel.png";
        e.DrawModel.ModelHeight = 1f * 13 / 10;

        e.DrawText ??= new ServerEntityDrawText();
        e.DrawText.Text = e.Sign.Text;
        e.DrawText.Dx = 1f * 3 / 32;
        e.DrawText.Dy = 1f * 36 / 32;
        e.DrawText.Dz = 1f * 3 / 32;

        e.Usable = true;
        e.DrawName ??= new ServerEntityDrawName
        {
            Name = "Sign",
            OnlyWhenSelected = true
        };
    }

    private void OnUseEntity(UseEntityArgs args)
    {
        ServerEntity e = server.GetEntity(args.ChunkX, args.ChunkY, args.ChunkZ, args.Id);
        if (e.Sign == null)
            return;

        if (!server.CheckBuildPrivileges(args.Player, (int)e.Position.X, (int)e.Position.Z, (int)e.Position.Y, PacketBlockSetMode.Use))
            return;

        DialogFont font = new("Verdana", 11f, DialogFontStyle.Bold);

        Widget okButton = Widget.MakeSolid(100, 100, 100, 50, ColorUtils.ColorFromArgb(255, 100, 100, 100));
        okButton.ClickKey = (char)13;
        okButton.Id = "UseSign_OK";

        Dialog d = new()
        {
            Width = 400,
            Height = 200,
            IsModal = true,
            Widgets =
            [
                Widget.MakeSolid(0, 0, 300, 200, ColorUtils.ColorFromArgb(255, 50, 50, 50)),
            Widget.MakeTextBox(e.Sign.Text, font, 50, 50, 200, 50, ColorUtils.ColorFromArgb(255, 0, 0, 0)),
            okButton,
            Widget.MakeText("OK", font, 100, 100, ColorUtils.ColorFromArgb(255, 0, 0, 0))
            ]
        };

        _serverClientService.Clients[args.Player].EditingSign = new ServerEntityId
        {
            ChunkX = args.ChunkX,
            ChunkY = args.ChunkY,
            ChunkZ = args.ChunkZ,
            Id = args.Id
        };

        server.SendDialog(args.Player, "UseSign", d);
    }

    private void OnDialogClick(DialogClick2Args args)
    {
        if (args.WidgetId != "UseSign_OK")
        {
            return;
        }

        ServerPlayer client = _serverClientService.Clients[args.Player];
        string newText = args.TextBoxValue[1];
        ServerEntityId id = client.EditingSign;

        client.EditingSign = null;

        if (newText != "")
        {
            ServerEntity e = server.GetEntity(id.ChunkX, id.ChunkY, id.ChunkZ, id.Id);
            e.Sign.Text = newText;
            server.SetEntityDirty(id);
        }
        else
        {
            server.DespawnEntity(id);
        }

        server.SendDialog(args.Player, "UseSign", null);
    }
}