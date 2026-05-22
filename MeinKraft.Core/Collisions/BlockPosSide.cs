using OpenTK.Mathematics;

/// <summary>
/// Represents the result of a block hit test, storing both the block's
/// grid position and the exact world-space collision point.
/// Used to determine which face of a block was hit and where.
/// </summary>
public class BlockPosSide
{
    /// <summary>The integer grid position of the hit block in world space.</summary>
    public Vector3 BlockPos { get; set; }

    /// <summary>
    /// The exact world-space position where the collision occurred,
    /// typically on the surface of the block face that was hit.
    /// </summary>
    public Vector3 CollisionPos { get; set; }

    /// <summary>
    /// Returns the block position translated by one unit in the direction
    /// of the hit face, giving the position of the adjacent block on that side.
    /// Used to determine where a newly placed block should be positioned.
    /// </summary>
    public float[] Translated()
    {
        float[] translated = [BlockPos[0], BlockPos[1], BlockPos[2]];

        if (CollisionPos[0] == BlockPos[0])
        {
            translated[0] -= 1;
        }

        if (CollisionPos[1] == BlockPos[1])
        {
            translated[1] -= 1;
        }

        if (CollisionPos[2] == BlockPos[2])
        {
            translated[2] -= 1;
        }

        if (CollisionPos[0] == BlockPos[0] + 1)
        {
            translated[0] += 1;
        }

        if (CollisionPos[1] == BlockPos[1] + 1)
        {
            translated[1] += 1;
        }

        if (CollisionPos[2] == BlockPos[2] + 1)
        {
            translated[2] += 1;
        }

        return translated;
    }

    /// <summary>
    /// Returns the block's grid position in world space.
    /// </summary>
    public Vector3 Current() => BlockPos;
}
