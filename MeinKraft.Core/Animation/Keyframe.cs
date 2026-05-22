/// <summary>
/// Stores the value of one animated property for a specific node
/// at a specific point in time within a named animation.
/// </summary>
public class Keyframe
{
    /// <summary>The animation this keyframe belongs to.</summary>
    public string AnimationName { get; set; }

    /// <summary>The node (bone) this keyframe affects.</summary>
    public string NodeName { get; set; }

    /// <summary>Time position within the animation, measured in frames.</summary>
    public int Frame { get; set; }

    /// <summary>The property being animated. See <see cref="KeyframeType"/>.</summary>
    public KeyframeType Type { get; set; }

    /// <summary>X component of the keyed value.</summary>
    public float X { get; set; }

    /// <summary>Y component of the keyed value.</summary>
    public float Y { get; set; }

    /// <summary>Z component of the keyed value.</summary>
    public float Z { get; set; }
}