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
/// <summary>Rail track directions (bit-flags).</summary>
[Flags]
public enum RailDirectionFlags
{
    None = 0,
    Horizontal = 1,
    Vertical = 2,
    UpLeft = 4,
    UpRight = 8,
    DownLeft = 16,
    DownRight = 32,

    Corners = UpLeft | UpRight | DownLeft | DownRight,
    TwoHorizontalVertical = Horizontal | Vertical,
    Full = Horizontal | Vertical | Corners,
}
