using SkiaSharp;

/// <summary>
/// Single application-wide SKTypeface for PressStart2P.
/// Loaded once from the embedded MAUI font resource.
/// </summary>
public static class GameTypeface
{
    public static readonly SKTypeface Instance = Load();

    private static SKTypeface Load()
    {
        var assembly = typeof(GameTypeface).Assembly;

        // Debug: see what's actually embedded
        string name = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("PressStart2P-Regular.ttf"));

        if (name == null)
        {
            // Fallback to system sans-serif so the game doesn't crash on load.
            return SKTypeface.FromFamilyName("sans-serif") ?? SKTypeface.Default;
        }

        using Stream stream = assembly.GetManifestResourceStream(name)!;
        return SKTypeface.FromStream(stream);
    }

    public static (int Width, int Height) Measure(string text, float fontSize)
    {
        using SKFont paint = new()
        {
            Size = fontSize,
            Typeface = Instance,
        };
        paint.MeasureText(text, out SKRect bounds);
        return ((int)MathF.Ceiling(bounds.Width), (int)MathF.Ceiling(bounds.Height));
    }
}