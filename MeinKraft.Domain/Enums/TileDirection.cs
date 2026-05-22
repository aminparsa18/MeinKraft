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
/// <summary>The 8 surrounding directions plus center, used for occlusion sampling.</summary>
public enum TileDirection
{
    Top = 0,
    Bottom = 1,
    Left = 2,
    Right = 3,
    TopLeft = 4,
    TopRight = 5,
    BottomLeft = 6,
    BottomRight = 7,
    Center = 8,
    Count = 9,
}
