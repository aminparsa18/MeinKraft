/// <summary>
/// Represents a string of text together with the font style used to render it.
/// </summary>
public class TextStyle
{
    /// <summary>The string content to render.</summary>
    public string Text { get; set; }

    /// <summary>Font size in points.</summary>
    public float FontSize { get; set; }

    /// <summary>ARGB color of the text.</summary>
    public int Color { get; set; }

    /// <summary>Font family name (e.g. <c>"Arial"</c>).</summary>
    public string FontFamily { get; set; }

    /// <summary>Font style flags (e.g. bold, italic). Interpretation is platform-defined.</summary>
    public bool Bold { get; set; }

    public bool Italic { get; set; }

    /// <summary>
    /// Returns <see langword="true"/> if all fields of this instance equal those of <paramref name="other"/>.
    /// </summary>
    public override bool Equals(object obj)
        => obj is TextStyle other
        && Text == other.Text
        && FontSize == other.FontSize
        && Color == other.Color
        && FontFamily == other.FontFamily
        && Bold == other.Bold
        && Italic == other.Italic;

    public override int GetHashCode()
        => HashCode.Combine(Text, Color, FontSize, FontFamily, Bold, Italic);
}
