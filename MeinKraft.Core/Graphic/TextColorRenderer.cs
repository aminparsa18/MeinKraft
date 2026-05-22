using SkiaSharp;
using System.Runtime.InteropServices;
using System.Text;

/// <summary>Renders multi-colored text into a single RGBA buffer using SkiaSharp.</summary>
public class TextColorRenderer
{

    /// <summary>
    /// Renders a <see cref="TextStyle"/> (which may contain inline &amp;X color codes) into
    /// a power-of-two RGBA pixel buffer ready for GL upload.
    /// </summary>
    public static (byte[] Rgba, int Width, int Height) CreateTextTexture(TextStyle t)
    {
        TextPart[] parts = DecodeColors(t.Text, t.Color);

        // ── Measure pass ──────────────────────────────────────────────────────
        float totalWidth = 0;
        float maxHeight = 0;
        float[] partWidths = new float[parts.Length];

        // Use a throwaway paint just for measurement — color doesn't matter here.
        using (SKFont measure = MakeFont(t.FontSize))
        {
            maxHeight = measure.Spacing;
            for (int i = 0; i < parts.Length; i++)
            {
                measure.MeasureText(parts[i].text, out SKRect b);
                partWidths[i] = b.Width + 2; // small per-segment padding
                totalWidth += partWidths[i];
            }
        }

        int w = NextPowerOfTwo(Math.Max(1, (int)MathF.Ceiling(totalWidth) + 2));
        int h = NextPowerOfTwo(Math.Max(1, (int)MathF.Ceiling(maxHeight) + 2));

        // ── Draw pass ─────────────────────────────────────────────────────────
        SKImageInfo info = new(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using SKSurface surface = SKSurface.Create(info);
        surface.Canvas.Clear(SKColors.Transparent);

        float curX = 0;
        for (int i = 0; i < parts.Length; i++)
        {
            if (string.IsNullOrEmpty(parts[i].text))
            {
                continue;
            }

            using SKPaint paint = MakePaint((uint)parts[i].color);
            using SKFont font = MakeFont(t.FontSize);
            font.MeasureText(parts[i].text, out SKRect bounds, paint);

            // DrawText origin is the baseline — offset by top of bounds to stay inside canvas.
            surface.Canvas.DrawText(parts[i].text, curX - bounds.Left, -bounds.Top + 1, SKTextAlign.Center, font, paint);
            curX += partWidths[i];
        }

        // ── Export ────────────────────────────────────────────────────────────
        using var image = surface.Snapshot();
        using var bmp = SKBitmap.FromImage(image);
        byte[] rgba = new byte[w * h * 4];
        Marshal.Copy(bmp.GetPixels(), rgba, 0, rgba.Length);
        return (rgba, w, h);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private static SKPaint MakePaint(uint colorArgb) => new()
    {
        IsAntialias = false,                     // pixel font — no blur
        Color = new SKColor(colorArgb),
    };

    private static SKFont MakeFont(float fontSize) => new()
    {
        Size = fontSize,
        Typeface = GameTypeface.Instance,     // single shared SKTypeface
    };

    /// <summary>
    /// Splits <paramref name="s"/> into colored segments by parsing inline color codes of the
    /// form <c>&amp;X</c> where X is a hex digit (0–9, a–f). Unrecognised sequences are kept as-is.
    /// </summary>
    private static TextPart[] DecodeColors(string s, int defaultcolor)
    {
        List<TextPart> parts = [];
        int currentcolor = defaultcolor;
        StringBuilder currenttext = new();

        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '&' && i + 1 < s.Length)
            {
                int color = HexToInt(s[i + 1]);
                if (color != -1)
                {
                    if (currenttext.Length > 0)
                    {
                        parts.Add(new TextPart { text = currenttext.ToString(), color = currentcolor });
                        currenttext.Clear();
                    }

                    currentcolor = GetColor(color);
                    i++; // skip the hex digit
                    continue;
                }
            }

            currenttext.Append(s[i]);
        }

        if (currenttext.Length > 0)
        {
            parts.Add(new TextPart { text = currenttext.ToString(), color = currentcolor });
        }

        return [.. parts];
    }

    /// <summary>
    /// Returns the smallest power of two that is greater than or equal to <paramref name="x"/>.
    /// Handles values up to 2^31.
    /// </summary>
    private static int NextPowerOfTwo(int x)
    {
        x--;
        x |= x >> 1;  // handle  2-bit numbers
        x |= x >> 2;  // handle  4-bit numbers
        x |= x >> 4;  // handle  8-bit numbers
        x |= x >> 8;  // handle 16-bit numbers
        x |= x >> 16; // handle 32-bit numbers
        x++;
        return x;
    }

    // ARGB color palette indexed by the hex digit in a color code sequence.
    // Loosely follows the classic 16-color terminal palette.
    private static readonly int[] ColorPalette =
    [
        ColorUtils.ColorFromArgb(255,   0,   0,   0), // 0 black
        ColorUtils.ColorFromArgb(255,   0,   0, 191), // 1 dark blue
        ColorUtils.ColorFromArgb(255,   0, 191,   0), // 2 dark green
        ColorUtils.ColorFromArgb(255,   0, 191, 191), // 3 dark cyan
        ColorUtils.ColorFromArgb(255, 191,   0,   0), // 4 dark red
        ColorUtils.ColorFromArgb(255, 191,   0, 191), // 5 dark magenta
        ColorUtils.ColorFromArgb(255, 191, 191,   0), // 6 dark yellow
        ColorUtils.ColorFromArgb(255, 191, 191, 191), // 7 light grey
        ColorUtils.ColorFromArgb(255,  40,  40,  40), // 8 dark grey
        ColorUtils.ColorFromArgb(255,  64,  64, 255), // 9 blue
        ColorUtils.ColorFromArgb(255,  64, 255,  64), // a green
        ColorUtils.ColorFromArgb(255,  64, 255, 255), // b cyan
        ColorUtils.ColorFromArgb(255, 255,  64,  64), // c red
        ColorUtils.ColorFromArgb(255, 255,  64, 255), // d magenta
        ColorUtils.ColorFromArgb(255, 255, 255,  64), // e yellow
        ColorUtils.ColorFromArgb(255, 255, 255, 255), // f white
    ];

    private static int GetColor(int index)
    {
        if (index >= 0 && index < ColorPalette.Length)
        {
            return ColorPalette[index];
        }

        return ColorPalette[15]; // default white
    }

    /// <summary>
    /// Converts a hex character (0–9, a–f, A–F) to its integer value, or -1 if not a hex digit.
    /// </summary>
    private static int HexToInt(char c)
    {
        if (c is >= '0' and <= '9')
        {
            return c - '0';
        }

        if (c is >= 'a' and <= 'f')
        {
            return c - 'a' + 10;
        }

        if (c is >= 'A' and <= 'F')
        {
            return c - 'A' + 10;
        }

        return -1;
    }
}

public class TextPart
{
    internal int color;
    internal string text;
}
