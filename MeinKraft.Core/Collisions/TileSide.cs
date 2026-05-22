
/// <summary>
/// Identifies one of the 6 faces of a tile (block) in world space.
/// </summary>
public enum TileSide
{
    /// <summary>The face on the positive Y axis.</summary>
    Top = 0,

    /// <summary>The face on the negative Y axis.</summary>
    Bottom = 1,

    /// <summary>The face on the positive Z axis.</summary>
    Front = 2,

    /// <summary>The face on the negative Z axis.</summary>
    Back = 3,

    /// <summary>The face on the negative X axis.</summary>
    Left = 4,

    /// <summary>The face on the positive X axis.</summary>
    Right = 5,
}
