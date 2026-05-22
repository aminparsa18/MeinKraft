/// <summary>
/// Renders minecart entities in the 3D world with interpolated rotation.
/// </summary>
public class ModDrawMinecarts : ModBase
{
    private const float VerticalOffset = -0.7f;
    private const float HalfSize = -0.5f;
    private const float HeightOffset = -0.3f;

    private int minecartTexture = -1;
    private readonly IMeshDrawer meshDrawer;
    private readonly IOpenGlService openGlService;

    public ModDrawMinecarts(IMeshDrawer meshDrawer, IOpenGlService openGlService, IGame game) : base(game)
    {
        this.meshDrawer = meshDrawer;
        this.openGlService = openGlService;
    }

    public override void OnRender3d(float deltaTime)
    {
        for (int i = 0; i < Game.Entities.Count; i++)
        {
            Minecart m = Game.Entities[i]?.Minecart;
            if (m == null || !m.Enabled)
            {
                continue;
            }

            Draw(m);
        }
    }

    private void Draw(Minecart m)
    {
        minecartTexture = minecartTexture == -1 ? Game.GetTexture("minecart.png") : minecartTexture;

        float rot = AngleInterpolation.InterpolateAngle360(
            VehicleRotation(m.LastDirection),
            VehicleRotation(m.Direction),
            m.Progress);

        RectangleF[] cc = CuboidRenderer.CuboidNet(8, 8, 8, 0, 0);
        CuboidRenderer.CuboidNetNormalize(cc, 32, 16);

        meshDrawer.GLPushMatrix();
        meshDrawer.GLTranslate(m.PositionX, m.PositionY + VerticalOffset, m.PositionZ);
        meshDrawer.GLRotate(-rot - 90, 0, 1, 0);
        openGlService.BindTexture2d(minecartTexture);
        CuboidRenderer.DrawCuboidWorld(openGlService, meshDrawer, HalfSize, HeightOffset, HalfSize, 1, 1, 1, cc, 1);
        meshDrawer.GLPopMatrix();
    }

    private static float VehicleRotation(VehicleDirection12 dir) => dir switch
    {
        VehicleDirection12.VerticalUp => 0,
        VehicleDirection12.DownRightRight or VehicleDirection12.UpLeftUp => 45,
        VehicleDirection12.HorizontalRight => 90,
        VehicleDirection12.UpRightRight or VehicleDirection12.DownLeftDown => 135,
        VehicleDirection12.VerticalDown => 180,
        VehicleDirection12.UpLeftLeft or VehicleDirection12.DownRightDown => 225,
        VehicleDirection12.HorizontalLeft => 270,
        VehicleDirection12.UpRightUp or VehicleDirection12.DownLeftLeft => 315,
        _ => 0,
    };
}


/// <summary>Represents a minecart entity moving along rails.</summary>
public class Minecart
{
    public bool Enabled { get; set; }
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public float PositionZ { get; set; }
    public VehicleDirection12 Direction { get; set; }
    public VehicleDirection12 LastDirection { get; set; }
    public float Progress { get; set; }
}
