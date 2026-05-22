using System.Text;

public class ModDrawTestModel : ModBase
{
    private readonly IOpenGlService _openGlService;
    private readonly IVoxelMap voxelMap;
    private readonly IMeshDrawer meshDrawer;

    public ModDrawTestModel(IOpenGlService platformOpenGl, IVoxelMap voxelMap, IMeshDrawer meshDrawer, IGame game) : base(game)
    {
        this._openGlService = platformOpenGl;
        this.voxelMap = voxelMap;
        this.meshDrawer = meshDrawer;
    }

    public override void OnRender3d(float deltaTime)
    {
        if (Game.GuiState == GameState.MapLoading)
        {
            return;
        }

        DrawTestModel(deltaTime);
    }

    private void DrawTestModel(float deltaTime)
    {
        if (!Game.EnableDrawTestCharacter)
        {
            return;
        }

        if (testmodel == null)
        {
            testmodel = new AnimatedModelRenderer(meshDrawer, _openGlService);
            byte[] data = Game.GetAssetFile("player.txt");
            int dataLength = Game.GetAssetFileLength("player.txt");
            string dataString = Encoding.UTF8.GetString(data, 0, dataLength);
            AnimatedModel model = AnimatedModelSerializer.Deserialize(dataString);
            testmodel.Start(Game, model);
        }

        meshDrawer.GLPushMatrix();
        meshDrawer.GLTranslate(voxelMap.MapSizeX / 2, Game.Blockheight(voxelMap.MapSizeX / 2, (voxelMap.MapSizeY / 2) - 2, 128), (voxelMap.MapSizeY / 2) - 2);
        _openGlService.BindTexture2d(Game.GetTexture("mineplayer.png"));
        testmodel.Render(deltaTime, 0, true, true, 1);
        meshDrawer.GLPopMatrix();
    }

    private AnimatedModelRenderer testmodel;

    public override bool OnClientCommand(ClientCommandArgs args)
    {
        if (args.Command == "testmodel")
        {
            Game.EnableDrawTestCharacter = Game.BoolCommandArgument(args.Arguments);
            return true;
        }

        return false;
    }
}
