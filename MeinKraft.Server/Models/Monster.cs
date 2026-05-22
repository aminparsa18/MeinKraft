#region Using Statements
using MemoryPack;
using OpenTK.Mathematics;
#endregion

/// <summary>
/// Represents a single monster entity in the world.
/// Persisted as part of the <see cref="ServerChunk"/> it occupies.
/// </summary>
[MemoryPackable]
public partial class Monster
{
    /// <summary>Unique monster ID, assigned by the server at spawn time.</summary>
    public int Id { get; set; }

    /// <summary>Monster type index, used to look up behaviour and appearance.</summary>
    public int MonsterType { get; set; }

    /// <summary>World X position in blocks.</summary>
    public int X { get; set; }

    /// <summary>World Y position in blocks.</summary>
    public int Y { get; set; }

    /// <summary>World Z position in blocks.</summary>
    public int Z { get; set; }

    /// <summary>Current health points. Not persisted — reset on world load.</summary>
    [MemoryPackIgnore]
    public int Health { get; set; }

    /// <summary>Current movement direction. Not persisted — recalculated each tick.</summary>
    [MemoryPackIgnore]
    public Vector3i WalkDirection { get; set; }

    /// <summary>
    /// Fractional progress [0, 1] through the current movement step.
    /// Not persisted — reset on world load.
    /// </summary>
    [MemoryPackIgnore]
    public float WalkProgress { get; set; }
}
