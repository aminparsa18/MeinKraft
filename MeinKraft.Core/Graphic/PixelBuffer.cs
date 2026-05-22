using SkiaSharp;
using System.Runtime.InteropServices;

/// <summary>
/// A platform-independent RGBA pixel buffer used for texture atlas construction
/// and image decoding. Backed by a raw byte array (4 bytes per pixel, RGBA order).
/// All System.Drawing dependencies have been removed — SkiaSharp handles all
/// image I/O, making this class safe on Windows, Android, Linux, and macOS.
/// </summary>
public class PixelBuffer
{
    /// <summary>Width of the buffer in pixels.</summary>
    public int Width { get; private set; }

    /// <summary>Height of the buffer in pixels.</summary>
    public int Height { get; private set; }

    /// <summary>
    /// Raw RGBA pixel data in row-major order.
    /// Layout: [R, G, B, A, R, G, B, A, ...], stride = Width * 4.
    /// </summary>
    public byte[] Rgba { get; private set; }

    // ── Factory ───────────────────────────────────────────────────────────────

    /// <summary>Creates a zeroed (fully transparent) <see cref="PixelBuffer"/> of the given dimensions.</summary>
    public static PixelBuffer Create(int width, int height) => new()
    {
        Width = width,
        Height = height,
        Rgba = new byte[width * height * 4],
    };

    // ── Pixel access ──────────────────────────────────────────────────────────

    /// <summary>Sets the RGBA color of the pixel at (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public void SetPixel(int x, int y, byte r, byte g, byte b, byte a)
    {
        int idx = (x + y * Width) * 4;
        Rgba[idx] = r;
        Rgba[idx + 1] = g;
        Rgba[idx + 2] = b;
        Rgba[idx + 3] = a;
    }

    /// <summary>Gets the RGBA components of the pixel at (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public (byte R, byte G, byte B, byte A) GetPixel(int x, int y)
    {
        int idx = (x + y * Width) * 4;
        return (Rgba[idx], Rgba[idx + 1], Rgba[idx + 2], Rgba[idx + 3]);
    }

    // ── PNG decoding ──────────────────────────────────────────────────────────

    /// <summary>
    /// Decodes raw PNG bytes into an RGBA pixel buffer.
    /// Returns a 1×1 orange fallback if the data is null, empty, or corrupt.
    /// </summary>
    /// <param name="data">Raw PNG file bytes.</param>
    /// <param name="dataLength">Number of valid bytes in <paramref name="data"/>.</param>
    public static (byte[] Rgba, int Width, int Height) RgbaFromPng(byte[] data, int dataLength)
    {
        if (data == null || dataLength == 0)
            return OrangeFallback();

        using var skBmp = SKBitmap.Decode(new ReadOnlySpan<byte>(data, 0, dataLength));
        if (skBmp == null)
            return OrangeFallback();

        // Normalise to RGBA8888 — source may be indexed, RGB, BGRA, etc.
        using var converted = skBmp.Copy(SKColorType.Rgba8888);
        if (converted == null)
            return OrangeFallback();

        byte[] rgba = new byte[converted.Width * converted.Height * 4];
        Marshal.Copy(converted.GetPixels(), rgba, 0, rgba.Length);
        return (rgba, converted.Width, converted.Height);
    }

    private static (byte[] Rgba, int Width, int Height) OrangeFallback()
        => (new byte[] { 255, 165, 0, 255 }, 1, 1);

    // ── Atlas utilities ───────────────────────────────────────────────────────

    /// <summary>
    /// Splits a square 2-D texture atlas into one or more 1-D (vertical strip) atlases,
    /// each no taller than <paramref name="atlasSizeLimit"/> pixels.
    /// Returns the produced strips as raw RGBA buffers.
    /// </summary>
    /// <param name="atlas2d">The source square atlas buffer.</param>
    /// <param name="tiles">Number of tiles along each axis (e.g. 32 for a 32×32 tile grid).</param>
    /// <param name="atlasSizeLimit">Maximum height in pixels for each output strip.</param>
    /// <returns>Array of RGBA strips; each entry is (Rgba, Width, Height).</returns>
    public static (byte[] Rgba, int Width, int Height)[] Atlas2dInto1d(
        PixelBuffer atlas2d, int tiles, int atlasSizeLimit)
    {
        int tilesize = atlas2d.Width / tiles;
        int totalTiles = tiles * tiles;
        int atlasesCount = Math.Max(1, totalTiles * tilesize / atlasSizeLimit);
        int tilesPerAtlas = totalTiles / atlasesCount;

        var results = new (byte[] Rgba, int Width, int Height)[atlasesCount];
        int atlasIdx = 0;
        PixelBuffer current = null;

        for (int i = 0; i < totalTiles; i++)
        {
            if (i % tilesPerAtlas == 0)
            {
                if (current != null)
                    results[atlasIdx++] = (current.Rgba, current.Width, current.Height);

                current = Create(tilesize, atlasSizeLimit);
            }

            int tileX = i % tiles;
            int tileY = i / tiles;
            int destY = i % tilesPerAtlas * tilesize;
            int srcBaseY = tileY * tilesize;

            for (int row = 0; row < tilesize; row++)
            {
                int srcOffset = ((srcBaseY + row) * atlas2d.Width + tileX * tilesize) * 4;
                int dstOffset = ((destY + row) * tilesize) * 4;
                atlas2d.Rgba.AsSpan(srcOffset, tilesize * 4)
                            .CopyTo(current.Rgba.AsSpan(dstOffset, tilesize * 4));
            }
        }

        if (current != null)
            results[atlasIdx] = (current.Rgba, current.Width, current.Height);

        return results;
    }
}