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
/// A single entry in the chunk tessellator's output buffer.
/// Stored as a struct so the pre-allocated return array is fully contiguous
/// in memory — no per-entry heap allocation.
/// </summary>
public struct VerticesIndicesToLoad
{
    public GeometryModel ModelData { get; set; }
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public float PositionZ { get; set; }
    public bool Transparent { get; set; }
    public int Texture { get; set; }
}
