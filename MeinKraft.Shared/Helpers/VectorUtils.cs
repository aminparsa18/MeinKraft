using OpenTK.Mathematics;
using System.Drawing;

public class VectorUtils
{
    public static void ToVectorInFixedSystem(float dx, float dy, float dz, float orientationx, float orientationy, ref Vector3 output)
    {
        if (dx == 0 && dy == 0 && dz == 0)
        {
            output = Vector3.Zero;
            return;
        }

        float xRot = orientationx;
        float yRot = orientationy;

        output.X = (dx * MathF.Cos(yRot)) + (dy * MathF.Sin(xRot) * MathF.Sin(yRot)) - (dz * MathF.Cos(xRot) * MathF.Sin(yRot));
        output.Y = (dy * MathF.Cos(xRot)) + (dz * MathF.Sin(xRot));
        output.Z = (dx * MathF.Sin(yRot)) - (dy * MathF.Sin(xRot) * MathF.Cos(yRot)) + (dz * MathF.Cos(xRot) * MathF.Cos(yRot));
    }

    public static bool UnProject(int winX, int winY, int winZ, Matrix4 model, Matrix4 proj, int[] view, out Vector3 objPos)
    {
        objPos = Vector3.Zero;

        Matrix4.Mult(in model, in proj, out Matrix4 finalMatrix);
        finalMatrix.Invert();

        Vector4 inp;
        inp.X = winX;
        inp.Y = winY;
        inp.Z = winZ;
        inp.W = 1;

        // Map x and y from window coordinates
        inp.X = (inp.X - view[0]) / view[2];
        inp.Y = (inp.Y - view[1]) / view[3];

        // Map to range -1 to 1
        inp.X = (inp.X * 2) - 1;
        inp.Y = (inp.Y * 2) - 1;
        inp.Z = (inp.Z * 2) - 1;

        Vector4.TransformRow(in inp, in finalMatrix, out Vector4 out_);

        if (out_.W == 0)
        {
            return false;
        }

        objPos.X = out_.X / out_.W;
        objPos.Y = out_.Y / out_.W;
        objPos.Z = out_.Z / out_.W;

        return true;
    }

    /// <summary>
    /// Returns <see langword="true"/> when the point (<paramref name="x"/>, <paramref name="y"/>)
    /// lies within the rectangle defined by origin (<paramref name="rx"/>, <paramref name="ry"/>)
    /// and size (<paramref name="rw"/>, <paramref name="rh"/>).
    /// </summary>
    public static bool PointInRect(float x, float y, float rx, float ry, float rw, float rh)
       => new Box2(rx, ry, rx + rw, ry + rh).ContainsExclusive(new Vector2(x, y));

    /// <summary>
    /// Returns the UV rectangle for a texture within a packed atlas grid.
    /// </summary>
    /// <param name="textureId">Flat index of the texture in the atlas.</param>
    /// <param name="texturesPacked">Number of textures along one axis of the atlas (assumed square).</param>
    /// <returns>A <see cref="RectangleF"/> in normalised [0,1] UV space.</returns>
    public static RectangleF GetAtlasRect(int textureId, int texturesPacked)
    {
        RectangleF r = new()
        {
            Y = 1f / texturesPacked * (textureId / texturesPacked),
            X = 1f / texturesPacked * (textureId % texturesPacked),
            Width = 1f / texturesPacked,
            Height = 1f / texturesPacked
        };
        return r;
    }

    public static int DistanceSquared(Vector3i a, Vector3i b)
    {
        int dx = a.X - b.X;
        int dy = a.Y - b.Y;
        int dz = a.Z - b.Z;
        return (dx * dx) + (dy * dy) + (dz * dz);
    }

    public static bool IsValidPos(IMapStorage map, int x, int y, int z) => x >= 0 && y >= 0 && z >= 0 && x < map.MapSizeX && y < map.MapSizeY && z < map.MapSizeZ;

    public static bool IsValidPos(IMapStorage map, int x, int y) => x >= 0 && y >= 0 && x < map.MapSizeX && y < map.MapSizeY;

    public static bool IsValidChunkPos(IMapStorage map, int cx, int cy, int cz, int chunksize)
    {
        return cx >= 0 && cy >= 0 && cz >= 0
            && cx < map.MapSizeX / chunksize
            && cy < map.MapSizeY / chunksize
            && cz < map.MapSizeZ / chunksize;
    }

    public static Point PlayerArea(int playerAreaSize, int centerAreaSize, Vector3i blockPosition)
    {
        Point p = PlayerCenterArea(centerAreaSize, blockPosition);
        int x = p.X + (centerAreaSize / 2);
        int y = p.Y + (centerAreaSize / 2);
        x -= playerAreaSize / 2;
        y -= playerAreaSize / 2;
        return new Point(x, y);
    }

    private static Point PlayerCenterArea(int centerAreaSize, Vector3i blockPosition)
    {
        int px = blockPosition.X;
        int py = blockPosition.Y;
        int gridposx = px / centerAreaSize * centerAreaSize;
        int gridposy = py / centerAreaSize * centerAreaSize;
        return new Point(gridposx, gridposy);
    }

    public static int BlockHeight(IMapStorage map, int tileidempty, int x, int y)
    {
        for (int z = map.MapSizeZ - 1; z >= 0; z--)
        {
            if (map.GetBlock(x, y, z) != tileidempty)
            {
                return z + 1;
            }
        }

        return map.MapSizeZ / 2;
    }

    /// <summary>
    /// Replaces the rotation component of the current model-view matrix with an identity rotation,
    /// making the object always face the camera (cylindrical billboard).
    /// See: http://stackoverflow.com/a/5487981
    /// </summary>
    public static void Billboard(IMeshDrawer meshDrawer)
    {
        Matrix4 m = meshDrawer.mvMatrix.Peek();

        float d = MathF.Sqrt((m.Row0.X * m.Row0.X) + (m.Row0.Y * m.Row0.Y) + (m.Row0.Z * m.Row0.Z));

        m.Row0 = new Vector4(d, 0, 0, 0);
        m.Row1 = new Vector4(0, d, 0, 0);
        m.Row2 = new Vector4(0, 0, d, 0);
        m.Row3 = new Vector4(m.Row3.X, m.Row3.Y, m.Row3.Z, 1);

        Matrix4.CreateRotationX(MathF.PI, out Matrix4 rotX);
        m = rotX * m;

        _ = meshDrawer.mvMatrix.Pop();
        meshDrawer.mvMatrix.Push(m);
        meshDrawer.GLLoadMatrix(m);
    }
}
