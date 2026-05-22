using System.ComponentModel.DataAnnotations;

using System.Reflection;

/// <summary>
/// Defines the set of properties that can be animated on a node.
/// Each type represents a 3-component (x, y, z) value.
/// </summary>
public enum KeyframeType
{
    /// <summary>Local position offset of the node, in model units.</summary>
    [Display(Name = "pos")] Position,

    /// <summary>Euler rotation of the node, in degrees.</summary>
    [Display(Name = "rot")] Rotation,

    /// <summary>Dimensions of the node's cuboid.</summary>
    [Display(Name = "siz")] Size,

    /// <summary>Pivot point offset, used as the center of rotation.</summary>
    [Display(Name = "piv")] Pivot,

    /// <summary>Non-uniform scale multiplier applied to the node.</summary>
    [Display(Name = "sca")] Scale,
}

/// <summary>
/// Extension methods for <see cref="KeyframeType"/> serialization.
/// </summary>
public static class KeyframeTypeExtensions
{
    /// <summary>
    /// Returns the short serialized name for this <see cref="KeyframeType"/>,
    /// as defined by its <see cref="DisplayAttribute"/>.
    /// </summary>
    public static string ToSerializedName(this KeyframeType type)
    {
        return type.GetType()
            .GetField(type.ToString())
            .GetCustomAttribute<DisplayAttribute>()
            .Name;
    }

    /// <summary>
    /// Parses a short serialized name back to a <see cref="KeyframeType"/>.
    /// Throws <see cref="ArgumentException"/> for unknown names.
    /// </summary>
    public static KeyframeType FromSerializedName(string name)
    {
        foreach (KeyframeType type in Enum.GetValues<KeyframeType>())
        {
            if (type.ToSerializedName() == name)
            {
                return type;
            }
        }

        throw new ArgumentException($"Unknown KeyframeType serialized name: '{name}'");
    }
}