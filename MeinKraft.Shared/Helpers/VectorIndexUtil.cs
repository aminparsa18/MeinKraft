using OpenTK.Mathematics;

/// <summary>
/// Utility methods for converting between flat array indices and 2D/3D grid coordinates.
/// All index layouts use row-major (X-major) order:
/// <list type="bullet">
///   <item><description>2D: <c>index = x + y * sizeX</c></description></item>
///   <item><description>3D: <c>index = (h * sizeY + y) * sizeX + x</c></description></item>
/// </list>
/// </summary>
public class VectorIndexUtil
{
    /// <summary>
    /// Converts 3D grid coordinates to a flat array index.
    /// Layout: <c>index = (h * sizeY + y) * sizeX + x</c>.
    /// </summary>
    /// <param name="x">X coordinate (fastest-varying axis).</param>
    /// <param name="y">Y coordinate.</param>
    /// <param name="h">Z/height coordinate (slowest-varying axis).</param>
    /// <param name="sizex">Grid width (number of elements along X).</param>
    /// <param name="sizey">Grid depth (number of elements along Y).</param>
    /// <returns>The flat index corresponding to (<paramref name="x"/>, <paramref name="y"/>, <paramref name="h"/>).</returns>
    public static int Index3d(int x, int y, int h, int sizex, int sizey) => (((h * sizey) + y) * sizex) + x;

    /// <summary>
    /// Converts 2D grid coordinates to a flat array index.
    /// Layout: <c>index = x + y * sizeX</c>.
    /// </summary>
    /// <param name="x">X coordinate (fastest-varying axis).</param>
    /// <param name="y">Y coordinate.</param>
    /// <param name="sizex">Grid width (number of elements along X).</param>
    /// <returns>The flat index corresponding to (<paramref name="x"/>, <paramref name="y"/>).</returns>
    public static int Index2d(int x, int y, int sizex) => x + (y * sizex);

    /// <summary>
    /// Decomposes a flat array index into 3D grid coordinates, writing them into
    /// <paramref name="ret"/> to avoid allocating a new struct on every call.
    /// Inverse of <see cref="Index3d"/>.
    /// </summary>
    /// <param name="index">The flat index to decompose.</param>
    /// <param name="sizex">Grid width (number of elements along X).</param>
    /// <param name="sizey">Grid depth (number of elements along Y).</param>
    /// <param name="ret">Output vector; X/Y/Z are overwritten with the grid coordinates.</param>
    public static void PosInt(int index, int sizex, int sizey, ref Vector3i ret)
    {
        ret.X = index % sizex;
        ret.Y = index / sizex % sizey;
        ret.Z = index / (sizex * sizey);
    }

    /// <summary>
    /// Returns the X component of the 3D grid coordinate corresponding to <paramref name="index"/>.
    /// </summary>
    /// <param name="index">The flat index.</param>
    /// <param name="sizex">Grid width (number of elements along X).</param>
    /// <param name="sizey">Grid depth (unused — present for call-site uniformity with <see cref="PosY"/> and <see cref="PosZ"/>).</param>
    public static int PosX(int index, int sizex, int sizey) => index % sizex;

    /// <summary>
    /// Returns the Y component of the 3D grid coordinate corresponding to <paramref name="index"/>.
    /// </summary>
    /// <param name="index">The flat index.</param>
    /// <param name="sizex">Grid width (number of elements along X).</param>
    /// <param name="sizey">Grid depth (number of elements along Y).</param>
    public static int PosY(int index, int sizex, int sizey) => index / sizex % sizey;

    /// <summary>
    /// Returns the Z (height) component of the 3D grid coordinate corresponding to <paramref name="index"/>.
    /// </summary>
    /// <param name="index">The flat index.</param>
    /// <param name="sizex">Grid width (number of elements along X).</param>
    /// <param name="sizey">Grid depth (number of elements along Y).</param>
    public static int PosZ(int index, int sizex, int sizey) => index / (sizex * sizey);
}