/// <summary>
/// Renders a compass HUD element when the player has a compass in their active material slots.
/// </summary>
public class ModCompass : ModBase
{
    private const float CompassSize = 175f;
    private const float CompassPosX = 100f;
    private const float CompassPosY = 100f;
    private const float NeedleDamping = 0.9f;
    private const float NeedleStiffness = 50f;

    private int compassId = -1;
    private int needleId = -1;
    private float compassAngle;
    private float compassVelocity;
    private readonly IGameWindowService platform;
    private readonly IMeshDrawer meshDrawer;
    private readonly IBlockRegistry blockTypeRegistry;

    public ModCompass(IGameWindowService platform, IMeshDrawer meshDrawer, IBlockRegistry blockTypeRegistry, IGame game) : base(game)
    {
        this.platform = platform;
        this.meshDrawer = meshDrawer;
        this.blockTypeRegistry = blockTypeRegistry;
    }

    public override void OnRender2d(float dt)
    {
        if (Game.GuiState == GameState.MapLoading)
        {
            return;
        }

        DrawCompass(Game);
    }

    private bool CompassInActiveMaterials(IGame game)
    {
        for (int i = 0; i < 10; i++)
        {
            if (game.MaterialSlots(i) == blockTypeRegistry.BlockIdCompass)
            {
                return true;
            }
        }

        return false;
    }

    public void DrawCompass(IGame game)
    {
        if (!CompassInActiveMaterials(game))
        {
            return;
        }

        if (compassId == -1)
        {
            compassId = game.GetTexture("compass.png");
            needleId = game.GetTexture("compassneedle.png");
        }

        float posX = platform.CanvasWidth - CompassPosX;
        float posY = CompassPosY;
        float playerOrientation = -(game.Player.Position.RotY / (2 * MathF.PI)) * 360f;

        // Spring-damper smoothing toward player orientation
        compassVelocity += (playerOrientation - compassAngle) / NeedleStiffness;
        compassVelocity *= NeedleDamping;
        compassAngle += compassVelocity;

        int white = ColorUtils.ColorFromArgb(255, 255, 255, 255);

        // Compass rose
        game.Draw2dTexture(compassId, posX - (CompassSize / 2), posY - (CompassSize / 2), CompassSize, CompassSize, null, 0, white, false);

        // Compass needle (rotated to match orientation)
        meshDrawer.GLPushMatrix();
        meshDrawer.GLTranslate(posX, posY, 0);
        meshDrawer.GLRotate(compassAngle, 0, 0, 90);
        meshDrawer.GLTranslate(-CompassSize / 2, -CompassSize / 2, 0);
        game.Draw2dTexture(needleId, 0, 0, CompassSize, CompassSize, null, 0, white, false);
        meshDrawer.GLPopMatrix();
    }
}