using MeinKraft;

/// <summary>
/// Renders a static textured sky sphere, switching between day and night textures based on game state.
/// </summary>
public class ModSkySphereStatic : ModBase
{
    private const int SphereSize = 1000;
    private const int SphereSegments = 20;

    internal int SkyTexture = -1;
    private int skySphereTexture = -1;
    private int skySphereNightTexture = -1;
    private GeometryModel skyModel;
    private readonly IOpenGlService _openGlService;
    private readonly IMeshDrawer _meshDrawer;
    private readonly ILightManager _lightManager;

    public ModSkySphereStatic(IOpenGlService platform, IMeshDrawer meshDrawer, ILightManager lightManager, IGame game) : base(game)
    {
        this._openGlService = platform;
        this._meshDrawer = meshDrawer;
        _lightManager = lightManager;
    }

    public override void OnRender3d(float deltaTime)
    {
        _openGlService.GlDisableFog();
        DrawSkySphere();
        Game.SetFog();
    }

    internal void DrawSkySphere()
    {
        if (skySphereTexture == -1)
        {
            skySphereTexture = LoadTexture("skysphere.png");
            skySphereNightTexture = LoadTexture("skyspherenight.png");
        }

        // Simple shadows always use the day texture
        SkyTexture = (!_lightManager.SkySphereNight || _lightManager.ShadowsSimple)
            ? skySphereTexture
            : skySphereNightTexture;

        Draw(Game.CurrentFov());
    }

    public void Draw(float fov)
    {
        if (SkyTexture == -1)
        {
            throw new InvalidOperationException($"error in {nameof(ModSkySphereStatic)} - {nameof(DrawSkySphere)}");
        }

        skyModel ??= _openGlService.CreateModel(Sphere.Create(SphereSize, SphereSize, SphereSegments, SphereSegments));

        Game.Set3dProjection(SphereSize * 2, fov);
        _meshDrawer.GLMatrixModeModelView();
        _meshDrawer.GLPushMatrix();
        _meshDrawer.GLTranslate(Game.Player.Position.X, Game.Player.Position.Y, Game.Player.Position.Z);
        _openGlService.BindTexture2d(SkyTexture);
        _meshDrawer.DrawModel(skyModel);
        _meshDrawer.GLPopMatrix();
        Game.Set3dProjection(Game.Zfar(), fov);
    }

    private int LoadTexture(string filename)
    {
        byte[] data = Game.GetAssetFile(filename);
        var (rgba, w, h) = PixelBuffer.RgbaFromPng(data, data.Length);
        return _openGlService.LoadTextureRgba(rgba, w, h);
    }
}