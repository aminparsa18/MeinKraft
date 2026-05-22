using OpenTK.Mathematics;

/// <summary>
/// Holds the CPU-side geometry buffers and GPU handle references for a single renderable mesh.
/// Geometry is described by parallel arrays of positions (<see cref="Xyz"/>), texture coordinates
/// (<see cref="Uv"/>), and vertex colours (<see cref="Rgba"/>), indexed by <see cref="Indices"/>.
/// GPU handles (<see cref="VaoId"/>, <see cref="VertexVboId"/> etc.) are populated by
/// <see cref="IGameWindowService.CreateModel"/> and must not be modified directly.
/// </summary>
public partial class GeometryModel
{
    // GPU handles (set by CreateModel)
    public int VaoId { get; set; }
    public int VertexVboId { get; set; }
    public int ColorVboId { get; set; }
    public int UvVboId { get; set; }
    public int IndexVboId { get; set; }

    // geometry
    public float[] Xyz { get; set; }
    public float[] Uv { get; set; }
    public byte[] Rgba { get; set; }
    public int[] Indices { get; set; }
    public int Mode { get; set; }

    // counts
    public int VerticesCount { get; set; }
    public int IndicesCount { get; set; }

    // computed counts
    public int XyzCount => VerticesCount * 3;
    public int UvCount => VerticesCount * 2;
    public int RgbaCount => VerticesCount * 4;

    /// <summary>Full white, fully opaque vertex colour.</summary>
    private static readonly int White = ColorUtils.ColorFromArgb(255, 255, 255, 255);

    public static void AddVertex(GeometryModel model, float x, float y, float z, float u, float v, int color)
    {
        if (model.VerticesCount >= model.Xyz.Length / 3)
        {
            float[] xyz = model.Xyz;
            float[] uv = model.Uv;
            byte[] rgba = model.Rgba;

            Array.Resize(ref xyz, xyz.Length * 2);
            Array.Resize(ref uv, uv.Length * 2);
            Array.Resize(ref rgba, rgba.Length * 2);

            model.Xyz = xyz;
            model.Uv = uv;
            model.Rgba = rgba;
        }

        int vi = model.VerticesCount * 3;
        int ui = model.VerticesCount * 2;
        int ri = model.VerticesCount * 4;

        model.Xyz[vi] = x;
        model.Xyz[vi + 1] = y;
        model.Xyz[vi + 2] = z;

        model.Uv[ui] = u;
        model.Uv[ui + 1] = v;

        model.Rgba[ri] = (byte)ColorUtils.ColorR(color);
        model.Rgba[ri + 1] = (byte)ColorUtils.ColorG(color);
        model.Rgba[ri + 2] = (byte)ColorUtils.ColorB(color);
        model.Rgba[ri + 3] = (byte)ColorUtils.ColorA(color);

        model.VerticesCount++;
    }

    /// <summary>
    /// Appends a single vertex with the given position and full white colour
    /// to the model's XYZ, UV, and RGBA buffers.
    /// </summary>
    /// <param name="model">The model data being built.</param>
    /// <param name="x">Vertex X position.</param>
    /// <param name="y">Vertex Y position.</param>
    /// <param name="z">Vertex Z position.</param>
    public static void AddVertex(GeometryModel model, float x, float y, float z)
    {
        int xyzOffset = model.XyzCount;
        int uvOffset = model.UvCount;
        int rgbaOffset = model.RgbaCount;

        model.Xyz[xyzOffset] = x;
        model.Xyz[xyzOffset + 1] = y;
        model.Xyz[xyzOffset + 2] = z;
        // UV is always (0,0) for wireframe — no texture sampling needed.
        model.Uv[uvOffset] = 0f;
        model.Uv[uvOffset + 1] = 0f;

        model.Rgba[rgbaOffset] = (byte)ColorUtils.ColorR(White);
        model.Rgba[rgbaOffset + 1] = (byte)ColorUtils.ColorG(White);
        model.Rgba[rgbaOffset + 2] = (byte)ColorUtils.ColorB(White);
        model.Rgba[rgbaOffset + 3] = (byte)ColorUtils.ColorA(White);

        model.VerticesCount++;
    }

    /// <summary>
    /// Convenience overload of <see cref="AddVertex(GeometryModel,float,float,float,float,float,int)"/>
    /// that accepts a <see cref="Vector3"/> position.
    /// </summary>
    public static void AddVertex(GeometryModel model, Vector3 pos, float u, float v, int color)
        => AddVertex(model, pos.X, pos.Y, pos.Z, u, v, color);

    public static void AddIndex(GeometryModel model, int index)
    {
        if (model.IndicesCount >= model.Indices.Length)
        {
            int[] indices = model.Indices;
            Array.Resize(ref indices, indices.Length * 2);
            model.Indices = indices;
        }

        model.Indices[model.IndicesCount++] = index;
    }
}