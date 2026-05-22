//This partial Game class handles all 2D rendering and block geometry utilities.
//Block geometry — Blockheight scans a column downward to find the topmost solid block.
// Getblockheight returns the visual fraction height of a block (rails are 30%, half-blocks are 50%,
//flat blocks are 5%, everything else 100%).
//2D drawing — a set of methods for drawing textured quads on screen (UI elements, inventory icons, HUD). 
//It routes through three paths: a simple full - texture quad, an atlas sub-region quad, 
//and a batch path that combines many quads into one GPU call. All three reuse pre-allocated
//geometry models to avoid per-frame heap allocations.
//Text rendering — Draw2dText renders text via a texture cache keyed on font+string+color. Draw2dText1
//adds a font cache on top so new Font() isn't called every frame.
//Circle — Circle3i draws a 2D circle outline using a pre-allocated line -
//loop model whose vertex positions are updated in-place each call.
//TextureAtlasCi is a small helper that converts a packed-atlas tile index into UV coordinates.

using MeinKraft;

public partial class Game
{
    // ── Constants ─────────────────────────────────────────────────────────────

    /// <summary>Height of a rail block as a fraction of a full block.</summary>
    private const float RailBlockHeight = 3f / 10f;

    /// <summary>Number of line segments used to approximate a circle.</summary>
    private const int CircleSegments = 32;

    // ── Pre-allocated 2D draw models ──────────────────────────────────────────

    /// <summary>Reusable model for full-texture quad draws.</summary>
    private GeometryModel _quadModel;

    /// <summary>
    /// Reusable model for atlas-sourced and part-texture quad draws.
    /// Arrays are overwritten in-place before each GPU upload — no per-call allocation.
    /// </summary>
    private GeometryModel _atlasQuadModel;

    /// <summary>
    /// Pre-allocated combined output model for <see cref="Draw2dTextures"/>.
    /// Fix #2: resized only when capacity is exceeded, not reallocated every frame.
    /// </summary>
    private GeometryModel _combinedModel;

    /// <summary>
    /// Pre-allocated per-element scratch models for <see cref="Draw2dTextures"/>.
    /// Fix #1: each slot is initialised once; only its arrays are overwritten per call.
    /// </summary>
    private readonly GeometryModel[] _batchModelScratch = new GeometryModel[512];

    /// <summary>
    /// Lazy-initialised per-radius circle geometry.
    /// Indices, Rgba and Uv are built once; only Xyz is updated per call.
    /// </summary>
    private GeometryModel _circleModelData;

    /// <summary>
    /// Font cache keyed by point size. Avoids allocating a new <see cref="Font"/>
    /// object on every <see cref="Draw2dText1"/> call.
    /// Fix #6: fonts are disposed and the cache is cleared when fonts are rebuilt.
    /// </summary>
    private readonly Dictionary<float, TextFont> _fontCache = new();

    // ── Block geometry ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the height (in blocks) of the highest non-air block at
    /// (<paramref name="x"/>, <paramref name="y"/>) below or at <paramref name="z_"/>.
    /// Returns 0 when the column is entirely air.
    /// </summary>
    public int Blockheight(int x, int y, int z_)
    {
        for (int z = z_; z >= 0; z--)
        {
            if (_voxelMap.GetBlock(x, y, z) != 0)
            {
                return z + 1;
            }
        }

        return 0;
    }

    /// <summary>
    /// Returns the visual height of the block at (<paramref name="x"/>,
    /// <paramref name="y"/>, <paramref name="z"/>) as a fraction of a full block.
    /// Returns 1 for out-of-bounds positions (treat as solid).
    /// </summary>
    public float Getblockheight(int x, int y, int z)
    {
        if (!_voxelMap.IsValidPos(x, y, z))
        {
            return 1f;
        }

        int block = _voxelMap.GetBlock(x, y, z);
        if (_blockRegistry.BlockTypes[block].Rail != 0)
        {
            return RailBlockHeight;
        }

        if (_blockRegistry.BlockTypes[block].DrawType == DrawType.HalfHeight)
        {
            return 0.5f;
        }

        if (_blockRegistry.BlockTypes[block].DrawType == DrawType.Flat)
        {
            return 0.05f;
        }

        return 1f;
    }

    // ── 2D drawing ────────────────────────────────────────────────────────────

    /// <summary>
    /// Draws a 2D texture at the given screen position.
    /// Routes to the simple (full-texture) or atlas path depending on whether
    /// <paramref name="inAtlasId"/> is specified and whether the colour is plain white.
    /// </summary>
    public void Draw2dTexture(int textureid, float x1, float y1, float width, float height,
        int? inAtlasId, int atlastextures, int color, bool enabledepthtest)
    {
        if (color == ColorUtils.ColorFromArgb(255, 255, 255, 255) && inAtlasId == null)
        {
            Draw2dTextureSimple(textureid, x1, y1, width, height, enabledepthtest);
        }
        else
        {
            Draw2dTextureInAtlas(textureid, x1, y1, width, height,
                inAtlasId, atlastextures, color, enabledepthtest);
        }
    }

    /// <summary>
    /// Draws a full-texture quad using the cached <see cref="_quadModel"/>.
    /// Fix #4: matrix operations collapsed from 5 calls to 3 by pre-computing
    /// the effective translation and scale instead of stacking them separately.
    /// </summary>
    private void Draw2dTextureSimple(int textureid, float x1, float y1,
        float width, float height, bool enabledepthtest)
    {
        openGlService.GlDisableCullFace();
        openGlService.BindTexture2d(textureid);
        if (!enabledepthtest)
        {
            openGlService.GlDisableDepthTest();
        }

        _quadModel ??= openGlService.CreateModel(Quad.Create());

        // Collapsed: Translate(x1,y1) · Scale(w,h) · Scale(0.5,0.5) · Translate(1,1)
        // = Translate(x1 + w*0.5, y1 + h*0.5) · Scale(w*0.5, h*0.5)
        meshDrawer.GLPushMatrix();
        meshDrawer.GLTranslate(x1 + (width * 0.5f), y1 + (height * 0.5f), 0f);
        meshDrawer.GLScale(width * 0.5f, height * 0.5f, 0f);
        meshDrawer.DrawModel(_quadModel);
        meshDrawer.GLPopMatrix();

        if (!enabledepthtest)
        {
            openGlService.GlEnableDepthTest();
        }

        openGlService.GlEnableCullFace();
    }

    /// <summary>
    /// Draws a sub-region of a texture atlas at the given screen position.
    /// Uses a single pre-allocated <see cref="_atlasQuadModel"/> updated in-place.
    /// </summary>
    private void Draw2dTextureInAtlas(int textureid, float x1, float y1,
        float width, float height, int? inAtlasId, int atlastextures, int color, bool enabledepthtest)
    {
        if (inAtlasId == null)
        {
            return;
        }

        RectangleF rect = TextureAtlas.TextureCoords2d(inAtlasId.Value, atlastextures);
        FillAtlasQuadModel(rect.X, rect.Y, rect.Width, rect.Height,
            x1, y1, width, height, color);

        openGlService.GlDisableCullFace();
        openGlService.BindTexture2d(textureid);
        if (!enabledepthtest)
        {
            openGlService.GlDisableDepthTest();
        }

        openGlService.UpdateModel(_atlasQuadModel);
        meshDrawer.DrawModelData(_atlasQuadModel);
        if (!enabledepthtest)
        {
            openGlService.GlEnableDepthTest();
        }

        openGlService.GlEnableCullFace();
    }

    /// <summary>
    /// Draws a portion of a texture (defined by source UV extents) onto a
    /// destination rectangle in screen space.
    /// </summary>
    public void Draw2dTexturePart(int textureid, float srcwidth, float srcheight,
        float dstx, float dsty, float dstwidth, float dstheight, int color, bool enabledepthtest)
    {
        FillAtlasQuadModel(0f, 0f, srcwidth, srcheight,
            dstx, dsty, dstwidth, dstheight, color);

        openGlService.GlDisableCullFace();
        openGlService.BindTexture2d(textureid);
        if (!enabledepthtest)
        {
            openGlService.GlDisableDepthTest();
        }

        openGlService.UpdateModel(_atlasQuadModel);
        meshDrawer.DrawModelData(_atlasQuadModel);
        if (!enabledepthtest)
        {
            openGlService.GlEnableDepthTest();
        }

        openGlService.GlEnableCullFace();
    }

    /// <summary>
    /// Initialises <see cref="_atlasQuadModel"/> on first call and overwrites its
    /// Xyz, Uv and Rgba arrays in-place. The index array is set once and never changes.
    /// Fix #7: colour bytes written as 16 direct assignments instead of a loop with
    /// index arithmetic.
    /// </summary>
    private void FillAtlasQuadModel(float sx, float sy, float sw, float sh,
        float dx, float dy, float dw, float dh, int color)
    {
        _atlasQuadModel ??= new GeometryModel
        {
            Xyz = new float[4 * 3],
            Uv = new float[4 * 2],
            Rgba = new byte[4 * 4],
            Indices = [0, 1, 2, 0, 2, 3],
            VerticesCount = 4,
            IndicesCount = 6,
            Mode = (int)DrawMode.Triangles,
        };

        float[] xyz = _atlasQuadModel.Xyz;
        xyz[0] = dx;
        xyz[1] = dy;
        xyz[2] = 0f;
        xyz[3] = dx + dw;
        xyz[4] = dy;
        xyz[5] = 0f;
        xyz[6] = dx + dw;
        xyz[7] = dy + dh;
        xyz[8] = 0f;
        xyz[9] = dx;
        xyz[10] = dy + dh;
        xyz[11] = 0f;

        float[] uv = _atlasQuadModel.Uv;
        uv[0] = sx;
        uv[1] = sy;
        uv[2] = sx + sw;
        uv[3] = sy;
        uv[4] = sx + sw;
        uv[5] = sy + sh;
        uv[6] = sx;
        uv[7] = sy + sh;

        // Fix #7: 16 direct assignments — clearer and avoids multiply+add per vertex.
        byte r = (byte)ColorUtils.ColorR(color);
        byte g = (byte)ColorUtils.ColorG(color);
        byte b = (byte)ColorUtils.ColorB(color);
        byte a = (byte)ColorUtils.ColorA(color);
        byte[] rgba = _atlasQuadModel.Rgba;
        rgba[0] = r;
        rgba[1] = g;
        rgba[2] = b;
        rgba[3] = a;
        rgba[4] = r;
        rgba[5] = g;
        rgba[6] = b;
        rgba[7] = a;
        rgba[8] = r;
        rgba[9] = g;
        rgba[10] = b;
        rgba[11] = a;
        rgba[12] = r;
        rgba[13] = g;
        rgba[14] = b;
        rgba[15] = a;
    }

    /// <summary>
    /// Draws a batch of textured quads in a single GPU call.
    /// Fix #1: each slot in <see cref="_batchModelScratch"/> is lazily initialised
    /// once; subsequent calls only overwrite the array values in-place.
    /// Fix #2: <see cref="_combinedModel"/> is reused across frames and only
    /// resized when the batch exceeds its current capacity.
    /// Fix #3: <see cref="DeleteUnusedCachedTextTextures"/> is NOT called here —
    /// call it once per frame at the end of the 2D draw pass instead.
    /// </summary>
    public void Draw2dTextures(Draw2dData[] todraw, int todrawLength, int textureid)
    {
        int count = 0;
        for ( int i = 0; i < todrawLength; i++)
        {
            Draw2dData d = todraw[i];
            RectangleF rect = TextureAtlas.TextureCoords2d(d.InAtlasId, GameConstants.MAX_BLOCKTYPES_SQRT);

            // Fix #1: lazily allocate each scratch slot once, then overwrite in-place.
            GeometryModel m = _batchModelScratch[count];
            if (m == null)
            {
                m = new GeometryModel
                {
                    Xyz = new float[4 * 3],
                    Uv = new float[4 * 2],
                    Rgba = new byte[4 * 4],
                    Indices = [0, 1, 2, 0, 2, 3],
                    VerticesCount = 4,
                    IndicesCount = 6,
                };
                _batchModelScratch[count] = m;
            }

            FillQuadModel(m, rect.X, rect.Y, rect.Width, rect.Height,
                d.X1, d.Y1, d.Width, d.Height,
                (byte)ColorUtils.ColorR(d.Color),
                (byte)ColorUtils.ColorG(d.Color),
                (byte)ColorUtils.ColorB(d.Color),
                (byte)ColorUtils.ColorA(d.Color));
            count++;
        }

        CombineModelDataInPlace(_batchModelScratch, count, ref _combinedModel);
        _combinedModel.Mode = (int)DrawMode.Triangles;

        openGlService.GlDisableCullFace();
        openGlService.BindTexture2d(textureid);
        openGlService.GlDisableDepthTest();
        openGlService.UpdateModel(_combinedModel);
        meshDrawer.DrawModelData(_combinedModel);
        openGlService.GlEnableDepthTest();
        openGlService.GlEnableCullFace();
    }

    /// <summary>
    /// Fills a pre-allocated <see cref="GeometryModel"/> quad in-place.
    /// Used by <see cref="Draw2dTextures"/> to avoid per-element allocation.
    /// </summary>
    private static void FillQuadModel(GeometryModel m,
        float sx, float sy, float sw, float sh,
        float dx, float dy, float dw, float dh,
        byte r, byte g, byte b, byte a)
    {
        float[] xyz = m.Xyz;
        xyz[0] = dx;
        xyz[1] = dy;
        xyz[2] = 0f;
        xyz[3] = dx + dw;
        xyz[4] = dy;
        xyz[5] = 0f;
        xyz[6] = dx + dw;
        xyz[7] = dy + dh;
        xyz[8] = 0f;
        xyz[9] = dx;
        xyz[10] = dy + dh;
        xyz[11] = 0f;

        float[] uv = m.Uv;
        uv[0] = sx;
        uv[1] = sy;
        uv[2] = sx + sw;
        uv[3] = sy;
        uv[4] = sx + sw;
        uv[5] = sy + sh;
        uv[6] = sx;
        uv[7] = sy + sh;

        byte[] rgba = m.Rgba;
        rgba[0] = r;
        rgba[1] = g;
        rgba[2] = b;
        rgba[3] = a;
        rgba[4] = r;
        rgba[5] = g;
        rgba[6] = b;
        rgba[7] = a;
        rgba[8] = r;
        rgba[9] = g;
        rgba[10] = b;
        rgba[11] = a;
        rgba[12] = r;
        rgba[13] = g;
        rgba[14] = b;
        rgba[15] = a;
    }

    /// <summary>Runs the 2D draw pass for all registered mods.</summary>
    private void Draw2d(float dt)
    {
        if (!ENABLE_DRAW2D)
        {
            return;
        }

        meshDrawer.OrthoMode(gameService.CanvasWidth, gameService.CanvasHeight);

        for (int i = 0; i < ClientMods.Count; i++)
        {
            ClientMods[i]?.OnRender2d(dt);
        }

        DeleteUnusedCachedTextTextures();

        meshDrawer.PerspectiveMode();
    }

    /// <summary>
    /// Fix #2: merges <paramref name="count"/> geometry models into
    /// <paramref name="combined"/>, reusing its arrays when capacity allows.
    /// Only reallocates when the required size exceeds current capacity.
    /// </summary>
    private static void CombineModelDataInPlace(
        GeometryModel[] modelDatas, int count, ref GeometryModel combined)
    {
        int totalIndices = 0;
        int totalVertices = 0;
        for (int i = 0; i < count; i++)
        {
            totalIndices += modelDatas[i].IndicesCount;
            totalVertices += modelDatas[i].VerticesCount;
        }

        // Only reallocate when existing arrays are too small.
        if (combined == null
         || combined.Indices.Length < totalIndices
         || combined.Xyz.Length < totalVertices * 3)
        {
            combined = new GeometryModel
            {
                Indices = new int[totalIndices],
                Xyz = new float[totalVertices * 3],
                Uv = new float[totalVertices * 2],
                Rgba = new byte[totalVertices * 4],
            };
        }

        combined.IndicesCount = 0;
        combined.VerticesCount = 0;

        for (int i = 0; i < count; i++)
        {
            GeometryModel m = modelDatas[i];
            int baseVertex = combined.VerticesCount;
            int baseIndex = combined.IndicesCount;

            for (int k = 0; k < m.IndicesCount; k++)
            {
                combined.Indices[baseIndex + k] = m.Indices[k] + baseVertex;
            }

            combined.IndicesCount += m.IndicesCount;

            m.Xyz.AsSpan(0, m.VerticesCount * 3)
                  .CopyTo(combined.Xyz.AsSpan(baseVertex * 3));
            m.Uv.AsSpan(0, m.VerticesCount * 2)
                 .CopyTo(combined.Uv.AsSpan(baseVertex * 2));
            m.Rgba.AsSpan(0, m.VerticesCount * 4)
                   .CopyTo(combined.Rgba.AsSpan(baseVertex * 4));

            combined.VerticesCount += m.VerticesCount;
        }
    }

    // ── 2D text ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Draws <paramref name="text"/> using Arial at the given point size.
    /// Fonts are cached by size to avoid per-call allocation.
    /// </summary>
    public void Draw2dText1(string text, int x, int y, int fontsize, int? color, bool enabledepthtest)
    {
        if (!_fontCache.TryGetValue(fontsize, out TextFont font))
        {
            font = GameFonts.Default;
            _fontCache[fontsize] = font;
        }

        Draw2dText(text, font, x, y, color, enabledepthtest);
    }

    /// <summary>
    /// Draws <paramref name="text"/> to the screen using a cached texture.
    /// here — it runs once per frame in <see cref="Draw2d"/> instead.
    /// </summary>
    public void Draw2dText(string text, TextFont font, float x, float y, int? color, bool enabledepthtest)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        color ??= ColorUtils.ColorFromArgb(255, 255, 255, 255);
        TextStyle t = new()
        {
            Text = text,
            Color = color.Value,
            FontSize = font.Size,
            FontFamily = font.Family,
            Bold = font.Bold,
            Italic = font.Italic,
        };

        if (!CachedTextTextures.TryGetValue(t, out CachedTexture cached))
        {
            cached = MakeTextTexture(t);
            if (cached == null)
            {
                return;
            }

            CachedTextTextures[t] = cached;
        }

        cached.LastUseMilliseconds = gameService.TimeMillisecondsFromStart;
        Draw2dTexture(cached.TextureId, x, y, cached.SizeX, cached.SizeY,
            null, 0, ColorUtils.ColorFromArgb(255, 255, 255, 255), enabledepthtest);
    }

    // ── Primitives ────────────────────────────────────────────────────────────

    /// <summary>
    /// Draws a 2D circle outline at screen position (<paramref name="x"/>,
    /// <paramref name="y"/>) with the given <paramref name="radius"/>.
    /// Uses a pre-allocated <see cref="_circleModelData"/> whose Xyz array is
    /// updated in-place on each call; indices and colours are set once.
    /// </summary>
    public void Circle3i(float x, float y, float radius)
    {
        if (_circleModelData == null)
        {
            _circleModelData = new GeometryModel
            {
                Mode = (int)DrawMode.Lines,
                Indices = new int[CircleSegments * 2],
                Xyz = new float[CircleSegments * 3],
                Rgba = new byte[CircleSegments * 4],
                Uv = new float[CircleSegments * 2],
                IndicesCount = CircleSegments * 2,
                VerticesCount = CircleSegments,
            };
            for (int i = 0; i < CircleSegments; i++)
            {
                _circleModelData.Indices[i * 2] = i;
                _circleModelData.Indices[(i * 2) + 1] = (i + 1) % CircleSegments;
            }

            _circleModelData.Rgba.AsSpan().Fill(255);
        }

        for (int i = 0; i < CircleSegments; i++)
        {
            float angle = i * 2f * MathF.PI / CircleSegments;
            _circleModelData.Xyz[(i * 3) + 0] = x + (MathF.Cos(angle) * radius);
            _circleModelData.Xyz[(i * 3) + 1] = y + (MathF.Sin(angle) * radius);
            _circleModelData.Xyz[(i * 3) + 2] = 0f;
        }

        openGlService.UpdateModel(_circleModelData);

        meshDrawer.GLPushMatrix();
        meshDrawer.GLLoadIdentity();
        meshDrawer.DrawModelData(_circleModelData);
        meshDrawer.GLPopMatrix();
    }
}

// ── Fix #5: static class — no instances, no state ─────────────────────────────

/// <summary>
/// Converts packed-atlas tile indices to UV coordinates.
/// </summary>
public static class TextureAtlas
{
    public static RectangleF TextureCoords2d(int textureId, int texturesPacked)
    {
        float step = 1f / texturesPacked;
        return new RectangleF
        {
            X = step * (textureId % texturesPacked),
            Y = step * (textureId / texturesPacked),
            Width = step,
            Height = step,
        };
    }
}