//Block definition:
//
//      Z
//      |
//      |
//      |
//      +----- X
//     /
//    /
//   Y
//

// <summary>
// Generates triangles for a single 16x16x16 chunk.
// Needs to know the surrounding of the chunk (18x18x18 blocks total).
// This class is heavily inlined and unrolled for performance.
// Special-shape (rare) blocks don't need as much performance.
// </summary>
/// <summary>
/// Per-block corner height modifiers for sloped geometry (rails, half-blocks).
/// A value-type struct stored as a field — no heap allocation per block.
/// </summary>
public struct CornerHeights
{
    public float TopLeft;
    public float TopRight;
    public float BottomLeft;
    public float BottomRight;

    /// <summary>Returns the height for the given corner index.</summary>
    public readonly float this[Corner c] => c switch
    {
        Corner.TopLeft => TopLeft,
        Corner.TopRight => TopRight,
        Corner.BottomLeft => BottomLeft,
        Corner.BottomRight => BottomRight,
        _ => 0f,
    };

    /// <summary>Resets all corners to zero for the next block.</summary>
    public void Clear() => TopLeft = TopRight = BottomLeft = BottomRight = 0f;
}
