/// <summary>
/// Provides a factory method for generating a wireframe unit cube <see cref="GeometryModel"/>,
/// rendered as line loops around each of the 6 faces.
/// </summary>
public class WireframeCube
{
    /// <summary>
    /// Builds a wireframe unit cube <see cref="GeometryModel"/> centred at the origin,
    /// with extents from -1 to +1 on each axis.
    /// Rendered using <see cref="DrawModeEnum.Lines"/>.
    /// </summary>
    /// <returns>A <see cref="GeometryModel"/> representing the wireframe cube.</returns>
    public static GeometryModel Create()
    {
        // 8 unique corners of the unit cube
        float[] xyz =
        [
        -1, -1, -1,  //  0
         1, -1, -1,  //  1
         1,  1, -1,  //  2
        -1,  1, -1,  //  3
        -1, -1,  1,  //  4
         1, -1,  1,  //  5
         1,  1,  1,  //  6
        -1,  1,  1,  //  7
        ];

        // 12 unique edges, each as a pair of vertex indices
        int[] indices =
        [
        0, 1,  1, 2,  2, 3,  3, 0,  // back face
        4, 5,  5, 6,  6, 7,  7, 4,  // front face
        0, 4,  1, 5,  2, 6,  3, 7,  // connecting edges
        ];

        // Full white, fully opaque — tint at draw time if needed
        byte[] rgba = new byte[8 * 4];
        Array.Fill(rgba, (byte)255);

        return new GeometryModel
        {
            Mode = (int)DrawMode.Lines,
            VerticesCount = 8,
            IndicesCount = 24,
            Xyz = xyz,
            Uv = new float[8 * 2],
            Rgba = rgba, // defaults to 0,0,0,0 — set colour at draw time
            Indices = indices,
        };
    }
}