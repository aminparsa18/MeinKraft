using MeinKraft;

/// <summary>
/// Renders an animated sky sphere whose colors are driven by sun position and sky/glow textures.
/// </summary>
public class ModSkySphereAnimated : ModBase
{
    private const int TextureSize = 512;
    private const int FancySegments = 64;
    private const int NormalSegments = 20;

    private readonly ModBase stars;
    private readonly IOpenGlService platform;
    private readonly IMeshDrawer meshDrawer;
    private readonly ILightManager _lightManager;
    private GeometryModel skyModel;
    private byte[] skyPixels;
    private byte[] glowPixels;
    private bool started;

    public ModSkySphereAnimated(IOpenGlService platform, IMeshDrawer meshDrawer, ILightManager lightManager, IGame game) : base(game)
    {
        this.platform = platform;
        this.meshDrawer = meshDrawer;
        this._lightManager = lightManager;
        stars = new ModSkySphereStatic(platform, meshDrawer, lightManager, game);
    }

    public override void OnRender3d(float deltaTime)
    {
        _lightManager.SkySphereNight = false;
        stars.OnRender3d(deltaTime);
        platform.GlDisableFog();
        DrawSkySphere();
        Game.SetFog();
    }

    internal void DrawSkySphere()
    {
        if (!started)
        {
            started = true;
            LoadPixels("sky.png", ref skyPixels);
            LoadPixels("glow.png", ref glowPixels);
        }

        platform.GlDisableDepthTest();
        Draw(Game.CurrentFov());
        platform.GlEnableDepthTest();
    }

    /// <summary>
    /// Loads a PNG asset into a flat RGBA pixel array.
    /// </summary>
    /// <param name="filename">Asset filename including extension (e.g. <c>"terrain.png"</c>).</param>
    /// <param name="pixels">Receives the loaded RGBA pixel data.</param>
    private void LoadPixels(string filename, ref byte[] pixels)
    {
        byte[] data = Game.GetAssetFile(filename);
        var (rgba, _, _) = PixelBuffer.RgbaFromPng(data, data.Length);
        pixels = rgba;
    }

    public void Draw(float fov)
    {
        int size = 1000;
        int segments = _lightManager.fancySkysphere ? FancySegments : NormalSegments;

        skyModel = GetSphereModelData2(skyModel, size, size, segments, segments,
            skyPixels, glowPixels, _lightManager.sunPosition.X, _lightManager.sunPosition.Y, _lightManager.sunPosition.Z);

        platform.UpdateModel(skyModel);
        Game.Set3dProjection(size * 2, fov);
        meshDrawer.GLMatrixModeModelView();
        meshDrawer.GLPushMatrix();
        meshDrawer.GLTranslate(Game.Player.Position.X, Game.Player.Position.Y, Game.Player.Position.Z);
        platform.BindTexture2d(0);
        meshDrawer.DrawModelData(skyModel);
        meshDrawer.GLPopMatrix();
        Game.Set3dProjection(Game.Zfar(), fov);
    }

    public static GeometryModel GetSphereModelData2(GeometryModel data,
        float radius, float height, int segments, int rings,
        byte[] skyPixels, byte[] glowPixels,
        float sunX, float sunY, float sunZ)
    {
        if (data == null)
        {
            data = new GeometryModel
            {
                Xyz = new float[rings * segments * 3],
                Uv = new float[rings * segments * 2],
                Rgba = new byte[rings * segments * 4]
            };
            data.VerticesCount = segments * rings;
            data.IndicesCount = segments * rings * 6;
            data.Indices = CalculateElements(segments, rings);
        }

        // Normalize sun direction once outside the vertex loop
        float sunLength = MathF.Sqrt((sunX * sunX) + (sunY * sunY) + (sunZ * sunZ));
        if (sunLength == 0)
        {
            sunLength = 1;
        }

        float sunXN = sunX / sunLength;
        float sunYN = sunY / sunLength;
        float sunZN = sunZ / sunLength;

        int i = 0;
        for (int y = 0; y < rings; y++)
        {
            float phi = y / (float)(rings - 1) * MathF.PI;
            float sinPhi = MathF.Sin(phi);
            float cosPhi = MathF.Cos(phi);

            for (int x = 0; x < segments; x++)
            {
                float theta = x / (float)(segments - 1) * 2 * MathF.PI;
                float vx = radius * sinPhi * MathF.Cos(theta);
                float vy = height * cosPhi;
                float vz = radius * sinPhi * MathF.Sin(theta);

                data.Xyz[i * 3] = vx;
                data.Xyz[(i * 3) + 1] = vy;
                data.Xyz[(i * 3) + 2] = vz;
                data.Uv[i * 2] = x / (float)(segments - 1);
                data.Uv[(i * 2) + 1] = y / (float)(rings - 1);
                float vertLen = MathF.Sqrt((vx * vx) + (vy * vy) + (vz * vz));
                float vxN = vx / vertLen, vyN = vy / vertLen, vzN = vz / vertLen;

                float dx = vxN - sunXN, dy = vyN - sunYN, dz = vzN - sunZN;
                float proximityToSun = 1f - (MathF.Sqrt((dx * dx) + (dy * dy) + (dz * dz)) / 2f);

                int skyColor = Texture2d(skyPixels, (sunYN + 2f) / 4f, 1f - ((vyN + 1f) / 2f));
                int glowColor = Texture2d(glowPixels, (sunYN + 1f) / 2f, 1f - proximityToSun);

                float skyA = ColorUtils.ColorA(skyColor) / 255f;
                float skyR = ColorUtils.ColorR(skyColor) / 255f;
                float skyG = ColorUtils.ColorG(skyColor) / 255f;
                float skyB = ColorUtils.ColorB(skyColor) / 255f;
                float glowA = ColorUtils.ColorA(glowColor) / 255f;
                float glowR = ColorUtils.ColorR(glowColor) / 255f;
                float glowG = ColorUtils.ColorG(glowColor) / 255f;
                float glowB = ColorUtils.ColorB(glowColor) / 255f;

                // Blend sky and glow
                data.Rgba[i * 4] = (byte)(Math.Min(1f, skyR + (glowR * glowA)) * 255);
                data.Rgba[(i * 4) + 1] = (byte)(Math.Min(1f, skyG + (glowG * glowA)) * 255);
                data.Rgba[(i * 4) + 2] = (byte)(Math.Min(1f, skyB + (glowB * glowA)) * 255);
                data.Rgba[(i * 4) + 3] = (byte)(Math.Min(1f, skyA) * 255);
                i++;
            }
        }

        return data;
    }

    /// <summary>
    /// Generates the triangle index buffer for a UV-sphere with the given tessellation.
    /// Each quad cell in the ring/segment grid is split into two triangles.
    /// </summary>
    /// <param name="segments">Number of subdivisions around the equator.</param>
    /// <param name="rings">Number of subdivisions from pole to pole.</param>
    private static int[] CalculateElements(int segments, int rings)
    {
        int[] indices = new int[segments * rings * 6];
        int i = 0;
        for (int y = 0; y < rings - 1; y++)
        {
            for (int x = 0; x < segments - 1; x++)
            {
                int bl = (y * segments) + x;
                int tl = ((y + 1) * segments) + x;
                int tr = ((y + 1) * segments) + x + 1;
                int br = (y * segments) + x + 1;

                indices[i++] = bl;
                indices[i++] = tl;
                indices[i++] = tr;
                indices[i++] = tr;
                indices[i++] = br;
                indices[i++] = bl;
            }
        }

        return indices;
    }

    private static int Texture2d(byte[] pixelsArgb, float x, float y)
    {
        int px = PositiveMod((int)(x * (TextureSize - 1)), TextureSize - 1);
        int py = PositiveMod((int)(y * (TextureSize - 1)), TextureSize - 1);
        return pixelsArgb[VectorIndexUtil.Index2d(px, py, TextureSize)];
    }

    private static int PositiveMod(int i, int n) => ((i % n) + n) % n;
}