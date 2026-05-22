using MeinKraft;

/// <summary>
/// Renders the sun and moon as billboarded sprites and updates their positions based on time of day.
/// </summary>
public class ModSunMoon(IMeshDrawer meshDrawer, ILightManager lightManager, IGame game) : ModBase(game)
{
    private const float TwoPi = 2 * MathF.PI;
    private const float OrbitRadius = 20f;
    private const float SpriteScale = 0.02f;

    internal int ImageSize = 96;
    internal float day_length_in_seconds = 30f;

    private int hour = 6;
    private float t;
    private int sunTexture = -1;
    private int moonTexture = -1;

    private readonly IMeshDrawer meshDrawer = meshDrawer;
    private readonly ILightManager _lightManager = lightManager;
    public int GetHour() => hour;

    public void SetHour(int value)
    {
        hour = value;
        t = (hour - 6) / 24f * TwoPi;
    }

    public override void OnRender3d(float dt)
    {
        if (sunTexture == -1)
        {
            sunTexture = Game.GetTexture("sun.png");
            moonTexture = Game.GetTexture("moon.png");
        }

        UpdateSunMoonPosition(dt);

        float bodyX = (_lightManager.isNight ? _lightManager.moonPosition.X : _lightManager.sunPosition.X) + Game.Player.Position.X;
        float bodyY = (_lightManager.isNight ? _lightManager.moonPosition.Y : _lightManager.sunPosition.Y) + Game.Player.Position.Y;
        float bodyZ = (_lightManager.isNight ? _lightManager.moonPosition.Z : _lightManager.sunPosition.Z) + Game.Player.Position.Z;

        meshDrawer.GLMatrixModeModelView();
        meshDrawer.GLPushMatrix();
        meshDrawer.GLTranslate(bodyX, bodyY, bodyZ);
        VectorUtils.Billboard(meshDrawer);
        meshDrawer.GLScale(SpriteScale, SpriteScale, SpriteScale);
        Game.Draw2dTexture(_lightManager.isNight ? moonTexture : sunTexture, 0, 0, ImageSize, ImageSize, null, 0, ColorUtils.ColorFromArgb(255, 255, 255, 255), false);
        meshDrawer.GLPopMatrix();
    }

    private void UpdateSunMoonPosition(float dt)
    {
        t += dt * TwoPi / day_length_in_seconds;

        _lightManager.isNight = (t + TwoPi) % TwoPi > MathF.PI;

        _lightManager.sunPosition = new OpenTK.Mathematics.Vector3(MathF.Cos(t) * OrbitRadius, MathF.Sin(t) * OrbitRadius, MathF.Sin(t) * OrbitRadius);
        _lightManager.moonPosition = new OpenTK.Mathematics.Vector3(MathF.Cos(-t) * OrbitRadius, MathF.Sin(-t) * OrbitRadius, MathF.Sin(t) * OrbitRadius);
    }
}