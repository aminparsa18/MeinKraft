using System.Drawing;

/// <summary>
/// Renders cuboid (box) geometry with UV-mapped faces using a standard
/// Minecraft-style cuboid net layout in the texture atlas.
/// </summary>
public static class CuboidRenderer
{
    /// <summary>
    /// The number of vertices per cuboid face (always a quad = 4 vertices).
    /// </summary>
    private const int VerticesPerFace = 4;

    /// <summary>
    /// The number of faces on a cuboid.
    /// </summary>
    private const int FaceCount = 6;

    /// <summary>
    /// The number of indices per face (2 triangles × 3 indices = 6).
    /// </summary>
    private const int IndicesPerFace = 6;

    /// <summary>
    /// Maps the 6 faces of a cuboid to UV rectangles (in pixels) within
    /// the texture atlas, based on the standard cuboid net layout:
    /// <code>
    ///         [top][bottom]
    /// [right][front][left][back]
    /// </code>
    /// </summary>
    /// <param name="tsizex">Width of the cuboid in pixels on the texture net (X dimension).</param>
    /// <param name="tsizey">Height of the cuboid in pixels on the texture net (Y dimension).</param>
    /// <param name="tsizez">Depth of the cuboid in pixels on the texture net (Z dimension).</param>
    /// <param name="tstartx">Horizontal start position of the net in the texture atlas, in pixels.</param>
    /// <param name="tstarty">Vertical start position of the net in the texture atlas, in pixels.</param>
    /// <returns>
    /// An array of 6 <see cref="RectangleF"/> values in pixel coordinates,
    /// ordered as: front, back, right, left, top, bottom.
    /// Pass to <see cref="CuboidNetNormalize"/> before rendering.
    /// </returns>
    public static RectangleF[] CuboidNet(float tsizex, float tsizey, float tsizez, float tstartx, float tstarty)
    {
        return
        [
            new RectangleF(tsizez + tstartx,                tsizez + tstarty, tsizex, tsizey), // front
            new RectangleF((2 * tsizez) + tsizex + tstartx,   tsizez + tstarty, tsizex, tsizey), // back
            new RectangleF(tstartx,                          tsizez + tstarty, tsizez, tsizey), // right
            new RectangleF(tsizez + tsizex + tstartx,       tsizez + tstarty, tsizez, tsizey), // left
            new RectangleF(tsizez + tstartx,                 tstarty,          tsizex, tsizez), // top
            new RectangleF(tsizez + tsizex + tstartx,        tstarty,          tsizex, tsizez), // bottom
        ];
    }

    /// <summary>
    /// Normalizes the pixel-space UV rectangles from <see cref="CuboidNet"/> to
    /// relative coordinates in the range 0-1 by dividing by the texture atlas dimensions.
    /// A small inset is applied to each edge to avoid texture bleeding on ATI/AMD GPUs
    /// caused by floating point imprecision at texel boundaries.
    /// </summary>
    /// <param name="coords">
    /// The 6 face rectangles returned by <see cref="CuboidNet"/>, modified in place.
    /// </param>
    /// <param name="textureWidth">Width of the texture atlas, in pixels.</param>
    /// <param name="textureHeight">Height of the texture atlas, in pixels.</param>
    public static void CuboidNetNormalize(RectangleF[] coords, float textureWidth, float textureHeight)
    {
        // Inset each UV edge slightly to prevent texture bleeding on ATI/AMD GPUs.
        const float AtiArtifactFix = 0.15f;

        for (int i = 0; i < 6; i++)
        {
            float x = (coords[i].X + AtiArtifactFix) / textureWidth;
            float y = (coords[i].Y + AtiArtifactFix) / textureHeight;
            float w = ((coords[i].X + coords[i].Width - AtiArtifactFix) / textureWidth) - x;
            float h = ((coords[i].Y + coords[i].Height - AtiArtifactFix) / textureHeight) - y;
            coords[i] = new RectangleF(x, y, w, h);
        }
    }

    /// <summary>
    /// Uploads and submits a <see cref="GeometryModel"/> buffer for a cuboid,
    /// disabling face culling during the draw call so all faces are visible.
    /// Since cuboid geometry is rebuilt every draw call, <see cref="IGameWindowService.UpdateModel"/>
    /// is used to sync the CPU buffers to the GPU before drawing.
    /// </summary>
    /// <param name="game">The game instance used for GL draw calls.</param>
    /// <param name="meshDrawer">The mesh drawer used to draw the model.</param>
    /// <param name="data">The model data with all vertices already written.</param>
    private static void SubmitCuboid(IOpenGlService openGlService, IMeshDrawer meshDrawer, GeometryModel data)
    {
        data.Indices = new int[FaceCount * IndicesPerFace];
        for (int i = 0; i < FaceCount; i++)
        {
            data.Indices[(i * IndicesPerFace) + 0] = (i * VerticesPerFace) + 3;
            data.Indices[(i * IndicesPerFace) + 1] = (i * VerticesPerFace) + 2;
            data.Indices[(i * IndicesPerFace) + 2] = (i * VerticesPerFace) + 0;
            data.Indices[(i * IndicesPerFace) + 3] = (i * VerticesPerFace) + 2;
            data.Indices[(i * IndicesPerFace) + 4] = (i * VerticesPerFace) + 1;
            data.Indices[(i * IndicesPerFace) + 5] = (i * VerticesPerFace) + 0;
        }

        data.IndicesCount = FaceCount * IndicesPerFace;
        data.Mode = (int)DrawMode.Triangles;

        // Sync all CPU buffers (xyz, rgba, uv, indices) to GPU.
        // CreateModel is called on first use; BufferSubData on subsequent frames.
        openGlService.UpdateModel(data);

        openGlService.GlDisableCullFace();
        meshDrawer.DrawModelData(data);
        openGlService.GlEnableCullFace();
    }

    /// <summary>
    /// Creates an empty <see cref="GeometryModel"/> buffer sized for one cuboid (6 faces × 4 vertices).
    /// </summary>
    /// <param name="light">Light intensity in the range 0-1.</param>
    /// <param name="color">The packed ARGB color encoding the light intensity.</param>
    private static GeometryModel CreateCuboidBuffer(float light, out int color)
    {
        int light255 = (int)(light * 255);
        color = ColorUtils.ColorFromArgb(255, light255, light255, light255);
        return new GeometryModel
        {
            Xyz = new float[VerticesPerFace * FaceCount * 3],
            Uv = new float[VerticesPerFace * FaceCount * 2],
            Rgba = new byte[VerticesPerFace * FaceCount * 4]
        };
    }

    /// <summary>
    /// Draws a cuboid using world-space winding order, where the front face
    /// is at minimum X. Used for static world geometry.
    /// </summary>
    public static void DrawCuboidWorld(IOpenGlService openGlService, IMeshDrawer meshDrawer, float posX, float posY, float posZ,
        float sizeX, float sizeY, float sizeZ,
        RectangleF[] textureCoords, float light)
    {
        GeometryModel data = CreateCuboidBuffer(light, out int color);
        RectangleF rect;

        // Front (min X)
        rect = textureCoords[0];
        GeometryModel.AddVertex(data, posX, posY, posZ, rect.X, rect.Bottom, color);
        GeometryModel.AddVertex(data, posX, posY, posZ + sizeZ, rect.X + rect.Width, rect.Bottom, color);
        GeometryModel.AddVertex(data, posX, posY + sizeY, posZ + sizeZ, rect.X + rect.Width, rect.Y, color);
        GeometryModel.AddVertex(data, posX, posY + sizeY, posZ, rect.X, rect.Y, color);

        // Back (max X)
        rect = textureCoords[1];
        GeometryModel.AddVertex(data, posX + sizeX, posY, posZ, rect.X, rect.Bottom, color);
        GeometryModel.AddVertex(data, posX + sizeX, posY, posZ + sizeZ, rect.X + rect.Width, rect.Bottom, color);
        GeometryModel.AddVertex(data, posX + sizeX, posY + sizeY, posZ + sizeZ, rect.X + rect.Width, rect.Y, color);
        GeometryModel.AddVertex(data, posX + sizeX, posY + sizeY, posZ, rect.X, rect.Y, color);

        // Left (min Z)
        rect = textureCoords[2];
        GeometryModel.AddVertex(data, posX + sizeX, posY, posZ, rect.X, rect.Bottom, color);
        GeometryModel.AddVertex(data, posX, posY, posZ, rect.X + rect.Width, rect.Bottom, color);
        GeometryModel.AddVertex(data, posX, posY + sizeY, posZ, rect.X + rect.Width, rect.Y, color);
        GeometryModel.AddVertex(data, posX + sizeX, posY + sizeY, posZ, rect.X, rect.Y, color);

        // Right (max Z)
        rect = textureCoords[3];
        GeometryModel.AddVertex(data, posX + sizeX, posY, posZ + sizeZ, rect.X + rect.Width, rect.Bottom, color);
        GeometryModel.AddVertex(data, posX, posY, posZ + sizeZ, rect.X, rect.Bottom, color);
        GeometryModel.AddVertex(data, posX, posY + sizeY, posZ + sizeZ, rect.X, rect.Y, color);
        GeometryModel.AddVertex(data, posX + sizeX, posY + sizeY, posZ + sizeZ, rect.X + rect.Width, rect.Y, color);

        // Top (max Y)
        rect = textureCoords[4];
        GeometryModel.AddVertex(data, posX, posY + sizeY, posZ, rect.X, rect.Bottom, color);
        GeometryModel.AddVertex(data, posX, posY + sizeY, posZ + sizeZ, rect.X + rect.Width, rect.Bottom, color);
        GeometryModel.AddVertex(data, posX + sizeX, posY + sizeY, posZ + sizeZ, rect.X + rect.Width, rect.Y, color);
        GeometryModel.AddVertex(data, posX + sizeX, posY + sizeY, posZ, rect.X, rect.Y, color);

        // Bottom (min Y)
        rect = textureCoords[5];
        GeometryModel.AddVertex(data, posX, posY, posZ, rect.X, rect.Bottom, color);
        GeometryModel.AddVertex(data, posX, posY, posZ + sizeZ, rect.X + rect.Width, rect.Bottom, color);
        GeometryModel.AddVertex(data, posX + sizeX, posY, posZ + sizeZ, rect.X + rect.Width, rect.Y, color);
        GeometryModel.AddVertex(data, posX + sizeX, posY, posZ, rect.X, rect.Y, color);

        SubmitCuboid(openGlService, meshDrawer, data);
    }

    /// <summary>
    /// Draws a cuboid using model-space winding order, where the front face
    /// is at maximum Z. Used for animated model nodes rendered by
    /// <see cref="AnimatedModelRenderer"/>.
    /// </summary>
    public static void DrawCuboidModel(IOpenGlService openGlService, IMeshDrawer meshDrawer, float posX, float posY, float posZ,
        float sizeX, float sizeY, float sizeZ,
        RectangleF[] textureCoords, float light)
    {
        GeometryModel data = CreateCuboidBuffer(light, out int color);
        RectangleF rect;

        // Right (min X)
        rect = textureCoords[2];
        GeometryModel.AddVertex(data, posX, posY, posZ, rect.X, rect.Bottom, color);
        GeometryModel.AddVertex(data, posX, posY, posZ + sizeZ, rect.X + rect.Width, rect.Bottom, color);
        GeometryModel.AddVertex(data, posX, posY + sizeY, posZ + sizeZ, rect.X + rect.Width, rect.Y, color);
        GeometryModel.AddVertex(data, posX, posY + sizeY, posZ, rect.X, rect.Y, color);

        // Left (max X)
        rect = textureCoords[3];
        GeometryModel.AddVertex(data, posX + sizeX, posY, posZ + sizeZ, rect.X, rect.Bottom, color);
        GeometryModel.AddVertex(data, posX + sizeX, posY, posZ, rect.X + rect.Width, rect.Bottom, color);
        GeometryModel.AddVertex(data, posX + sizeX, posY + sizeY, posZ, rect.X + rect.Width, rect.Y, color);
        GeometryModel.AddVertex(data, posX + sizeX, posY + sizeY, posZ + sizeZ, rect.X, rect.Y, color);

        // Back (min Z)
        rect = textureCoords[1];
        GeometryModel.AddVertex(data, posX + sizeX, posY, posZ, rect.X, rect.Bottom, color);
        GeometryModel.AddVertex(data, posX, posY, posZ, rect.X + rect.Width, rect.Bottom, color);
        GeometryModel.AddVertex(data, posX, posY + sizeY, posZ, rect.X + rect.Width, rect.Y, color);
        GeometryModel.AddVertex(data, posX + sizeX, posY + sizeY, posZ, rect.X, rect.Y, color);

        // Front (max Z)
        rect = textureCoords[0];
        GeometryModel.AddVertex(data, posX + sizeX, posY, posZ + sizeZ, rect.X + rect.Width, rect.Bottom, color);
        GeometryModel.AddVertex(data, posX, posY, posZ + sizeZ, rect.X, rect.Bottom, color);
        GeometryModel.AddVertex(data, posX, posY + sizeY, posZ + sizeZ, rect.X, rect.Y, color);
        GeometryModel.AddVertex(data, posX + sizeX, posY + sizeY, posZ + sizeZ, rect.X + rect.Width, rect.Y, color);

        // Top (max Y)
        rect = textureCoords[4];
        GeometryModel.AddVertex(data, posX, posY + sizeY, posZ, rect.X, rect.Y, color);
        GeometryModel.AddVertex(data, posX, posY + sizeY, posZ + sizeZ, rect.X, rect.Bottom, color);
        GeometryModel.AddVertex(data, posX + sizeX, posY + sizeY, posZ + sizeZ, rect.X + rect.Width, rect.Bottom, color);
        GeometryModel.AddVertex(data, posX + sizeX, posY + sizeY, posZ, rect.X + rect.Width, rect.Y, color);

        // Bottom (min Y)
        rect = textureCoords[5];
        GeometryModel.AddVertex(data, posX, posY, posZ, rect.X, rect.Y, color);
        GeometryModel.AddVertex(data, posX, posY, posZ + sizeZ, rect.X, rect.Bottom, color);
        GeometryModel.AddVertex(data, posX + sizeX, posY, posZ + sizeZ, rect.X + rect.Width, rect.Bottom, color);
        GeometryModel.AddVertex(data, posX + sizeX, posY, posZ, rect.X + rect.Width, rect.Y, color);

        SubmitCuboid(openGlService, meshDrawer, data);
    }
}