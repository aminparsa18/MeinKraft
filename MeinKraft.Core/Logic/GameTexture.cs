//This partial Game class manages all GPU texture loading and caching:
//White texture — lazily creates a 1×1 white pixel texture used as a neutral tint when no colour modulation is needed.
//Text texture cache — DeleteUnusedCachedTextTextures evicts text textures that haven't been used for over a second,
//releasing their GPU handles. MakeTextTexture renders a TextStyle to a bitmap and uploads it to the GPU.
//Named texture cache — GetTexture and GetTextureOrLoad load PNG assets into GPU textures and cache them by name.
//DeleteTexture removes a texture and releases its GPU handle.
//Terrain texture atlas — UseTerrainTextures builds a 2D atlas by loading all terrain tile PNGs
//and blitting them into a large atlas bitmap row-by-row. UseTerrainTextureAtlas2d uploads 
//that atlas and splits it into 1D strips for the tessellator.

public partial class Game
{
    // ── White texture ─────────────────────────────────────────────────────────

    /// <summary>
    /// Returns (and lazily creates) a 1×1 white GPU texture.
    /// Used as a neutral tint when no colour modulation is needed.
    /// Named <c>GetOrCreate</c> to signal that this has a side effect on first call.
    /// </summary>
    public int GetOrCreateWhiteTexture()
    {
        if (whitetexture == -1)
        {
            PixelBuffer buf = PixelBuffer.Create(1, 1);
            buf.SetPixel(0, 0, 255, 255, 255, 255);
            whitetexture = openGlService.LoadTextureRgba(buf.Rgba, 1, 1);
        }

        return whitetexture;
    }

    // ── Text texture cache ────────────────────────────────────────────────────

    private readonly List<TextStyle> _textStylesToRemove = new();

    /// <summary>
    /// Evicts text texture cache entries that have not been used for more than
    /// one second, releasing their GPU texture handles.
    /// instead of allocating a new one on every eviction frame.
    /// </summary>
    public void DeleteUnusedCachedTextTextures()
    {
        int now = gameService.TimeMillisecondsFromStart;

        _textStylesToRemove.Clear();
        foreach ((TextStyle? style, CachedTexture? tex) in CachedTextTextures)
        {
            if ((now - tex.LastUseMilliseconds) / 1000f > 1f)
            {
                _textStylesToRemove.Add(style);
            }
        }

        foreach (TextStyle key in _textStylesToRemove)
        {
            openGlService.GLDeleteTexture(CachedTextTextures[key].TextureId);
            CachedTextTextures.Remove(key);
        }
    }

    /// <summary>
    /// Renders <paramref name="t"/> to a <see cref="Bitmap"/> via
    /// <see cref="TextColorRenderer"/>, uploads it to the GPU, and returns
    /// the resulting <see cref="CachedTexture"/>.
    /// </summary>
    private CachedTexture MakeTextTexture(TextStyle t)
    {
        var (rgba, w, h) = TextColorRenderer.CreateTextTexture(t);
        return new CachedTexture
        {
            SizeX = w,
            SizeY = h,
            TextureId = openGlService.LoadTextureRgba(rgba, w, h),
        };
    }

    // ── Named texture cache ───────────────────────────────────────────────────

    /// <summary>
    /// Returns the GPU texture ID for the named asset, loading and caching it
    /// on first access.
    /// </summary>
    public int GetTexture(string p)
    {
        if (!textures.TryGetValue(p, out int id))
        {
            var (rgba, w, h) = PixelBuffer.RgbaFromPng(GetAssetFile(p), GetAssetFileLength(p));
            id = openGlService.LoadTextureRgba(rgba, w, h);
            textures[p] = id;
        }

        return id;
    }

    /// <summary>
    /// Returns the name of the texture asset for the given GPU texture ID.
    /// </summary>
    public string GetTextureNameById(int id) 
        => textures.FirstOrDefault(x => x.Value == id).Key;

    /// <summary>
    /// Returns the cached GPU texture for <paramref name="name"/>,
    /// uploading the RGBA pixel data on first access.
    /// </summary>
    public int GetTextureOrLoad(string name, byte[] rgba, int width, int height)
    {
        if (!textures.TryGetValue(name, out int id))
        {
            id = openGlService.LoadTextureRgba(rgba, width, height);
            textures[name] = id;
        }

        return id;
    }

    /// <summary>
    /// Removes the named texture from the cache and releases its GPU handle.
    /// </summary>
    /// <returns><see langword="true"/> if the texture was found and deleted.</returns>
    public bool DeleteTexture(string name)
    {
        if (name != null && textures.TryGetValue(name, out int id))
        {
            textures.Remove(name);
            openGlService.GLDeleteTexture(id);
            return true;
        }

        return false;
    }

    // ── Terrain texture atlas ─────────────────────────────────────────────────

    /// <summary>
    /// Uploads <paramref name="atlas2d"/> as the main terrain texture and splits
    /// it into 1-D atlas strips for indexed lookup by the tessellator.
    /// </summary>
    private void UseTerrainTextureAtlas2d(PixelBuffer atlas2d, int atlas2dWidth)
    {
        TerrainTexture = openGlService.LoadTextureRgba(atlas2d.Rgba, atlas2d.Width, atlas2d.Height);

        int atlas1dHeight = Atlas1dheight();
        int texturesPerAtlas = atlas1dHeight / (atlas2dWidth / GameConstants.MAX_BLOCKTYPES_SQRT);

        TerrainChunkTesselator.OnAtlasReady(texturesPerAtlas);

        var atlases1d = PixelBuffer.Atlas2dInto1d(atlas2d, GameConstants.MAX_BLOCKTYPES_SQRT, atlas1dHeight);

        TerrainChunkTesselator.TerrainTextures1d = new int[atlases1d.Length];
        for (int i = 0; i < atlases1d.Length; i++)
        {
            var (rgba, w, h) = atlases1d[i];
            TerrainChunkTesselator.TerrainTextures1d[i] = openGlService.LoadTextureRgba(rgba, w, h);
        }
    }

    /// <summary>
    /// Builds a 2-D texture atlas from the given terrain texture IDs and uploads
    /// it to the GPU via <see cref="UseTerrainTextureAtlas2d"/>.
    /// Each texture is loaded from a PNG asset file and blitted into the atlas.
    /// Textures that are missing or not exactly 32×32 pixels are skipped.
    /// </summary>
    /// <param name="textureIds">Texture asset names (without <c>.png</c>). Null entries are skipped.</param>
    /// <param name="textureIdsCount">Number of valid entries to process.</param>
    public void UseTerrainTextures(string[] textureIds, int textureIdsCount)
    {
        const int tilesize = 32;

        PixelBuffer atlas2d = PixelBuffer.Create(
            tilesize * GameConstants.MAX_BLOCKTYPES_SQRT,
            tilesize * GameConstants.MAX_BLOCKTYPES_SQRT);

        byte[] unknownPng = GetAssetFile("Unknown.png");

        for (int i = 0; i < textureIdsCount; i++)
        {
            if (textureIds[i] == null) continue;

            byte[] fileData = GetAssetFile(string.Concat(textureIds[i], ".png")) ?? unknownPng;
            if (fileData == null) continue;

            var (rgba, w, h) = PixelBuffer.RgbaFromPng(fileData, fileData.Length);

            if (w != tilesize || h != tilesize)
            {
                Console.WriteLine(
                    $"[Terrain] Skipping '{textureIds[i]}': expected {tilesize}×{tilesize}, got {w}×{h}.");
                continue;
            }

            int destX = i % GameConstants.MAX_BLOCKTYPES_SQRT * tilesize;
            int destY = i / GameConstants.MAX_BLOCKTYPES_SQRT * tilesize;

            for (int row = 0; row < tilesize; row++)
            {
                rgba.AsSpan(row * tilesize * 4, tilesize * 4)
                    .CopyTo(atlas2d.Rgba.AsSpan(
                        ((destY + row) * atlas2d.Width + destX) * 4, tilesize * 4));
            }
        }

        UseTerrainTextureAtlas2d(atlas2d, atlas2d.Width);
    }
}