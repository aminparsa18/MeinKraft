/// <summary>
/// Provides factory methods for building <see cref="GeometryModel"/> instances
/// representing screen-aligned quads (two triangles forming a rectangle).
/// </summary>
public class Quad
{
    /// <summary>Number of vertices in a quad.</summary>
    private const int VertexCount = 4;

    /// <summary>Number of position components per vertex (X, Y, Z).</summary>
    private const int PositionComponents = 3;

    /// <summary>Number of UV components per vertex (U, V).</summary>
    private const int UvComponents = 2;

    /// <summary>Number of indices forming the two triangles of a quad.</summary>
    private const int IndexCount = 6;

    /// <summary>
    /// Flat XYZ positions for a unit quad centred at the origin on the Z=0 plane.
    /// Laid out as 4 vertices × 3 components.
    /// </summary>
    private static readonly float[] QuadVertices =
    [
        // X      Y     Z
        -1f,  -1f,   0f,  // Bottom-left
         1f,  -1f,   0f,  // Bottom-right
         1f,   1f,   0f,  // Top-right
        -1f,   1f,   0f,  // Top-left
    ];

    /// <summary>
    /// Flat UV texture coordinates for a quad covering the full [0,1] range.
    /// Laid out as 4 vertices × 2 components.
    /// </summary>
    private static readonly float[] QuadTextureCoords =
    [
        // U   V
        0f,  0f,  // Bottom-left
        1f,  0f,  // Bottom-right
        1f,  1f,  // Top-right
        0f,  1f,  // Top-left
    ];

    /// <summary>
    /// Triangle indices for a quad, forming two clockwise triangles.
    /// </summary>
    private static readonly int[] QuadVertexIndices =
    [
        0, 1, 2,
        0, 2, 3,
    ];

    /// <summary>
    /// Cached all-white rgba array (255,255,255,255 × 4 vertices).
    /// The most common colour passed to <see cref="CreateColored"/>; returning
    /// this instance avoids allocating a <c>byte[16]</c> on every call.
    /// MUST NOT be mutated by callers.
    /// </summary>
    private static readonly byte[] s_whiteRgba = [
        255, 255, 255, 255,
        255, 255, 255, 255,
        255, 255, 255, 255,
        255, 255, 255, 255,
    ];

    /// <summary>
    /// Builds a unit quad <see cref="GeometryModel"/> centred at the origin
    /// with full [0,1] UV coverage and no vertex colours.
    /// </summary>
    /// <returns>A <see cref="GeometryModel"/> representing the unit quad.</returns>
    public static GeometryModel Create()
    {
        GeometryModel m = new();

        float[] xyz = new float[PositionComponents * VertexCount];
        Array.Copy(QuadVertices, xyz, xyz.Length);
        m.Xyz = xyz;

        float[] uv = new float[UvComponents * VertexCount];
        Array.Copy(QuadTextureCoords, uv, uv.Length);
        m.Uv = uv;
        // white, fully opaque for all vertices
        m.Rgba = s_whiteRgba;   // shared; caller must not mutate

        m.VerticesCount = VertexCount;
        m.Indices = QuadVertexIndices;
        m.IndicesCount = IndexCount;

        return m;
    }
}