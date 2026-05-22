/// <summary>
/// Platform-agnostic font descriptor. Replaces System.Drawing.Font at all call sites.
/// </summary>
public readonly record struct TextFont(string Family, float Size, bool Bold = false, bool Italic = false)
{
    public static readonly TextFont Default = new("sans-serif", 14);
}