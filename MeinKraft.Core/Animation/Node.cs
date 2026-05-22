/// <summary>
/// Represents a single node (bone) in the animated model hierarchy.
/// Nodes are arranged in a parent-child tree; each node's transform
/// is applied relative to its parent's transform.
/// Default values for all transform properties are used when no
/// keyframe data overrides them for a given animation frame.
/// </summary>
public class Node
{
    /// <summary>Unique name identifying this node within the model.</summary>
    public string Name { get; set; }

    /// <summary>
    /// Name of the parent node. Use <c>"root"</c> for top-level nodes
    /// that have no parent.
    /// </summary>
    public string ParentName { get; set; }

    /// <summary>Default local position X offset, in model units.</summary>
    public float PosX { get; set; }

    /// <summary>Default local position Y offset, in model units.</summary>
    public float PosY { get; set; }

    /// <summary>Default local position Z offset, in model units.</summary>
    public float PosZ { get; set; }

    /// <summary>Default rotation around the X axis, in degrees.</summary>
    public float RotateX { get; set; }

    /// <summary>Default rotation around the Y axis, in degrees.</summary>
    public float RotateY { get; set; }

    /// <summary>Default rotation around the Z axis, in degrees.</summary>
    public float RotateZ { get; set; }

    /// <summary>Default cuboid width (X dimension), in pixels on the texture net.</summary>
    public float SizeX { get; set; }

    /// <summary>Default cuboid height (Y dimension), in pixels on the texture net.</summary>
    public float SizeY { get; set; }

    /// <summary>Default cuboid depth (Z dimension), in pixels on the texture net.</summary>
    public float SizeZ { get; set; }

    /// <summary>Horizontal start position of this node's texture region, in pixels.</summary>
    public float U { get; set; }

    /// <summary>Vertical start position of this node's texture region, in pixels.</summary>
    public float V { get; set; }

    /// <summary>Pivot point X offset, used as the center of rotation, in model units.</summary>
    public float PivotX { get; set; }

    /// <summary>Pivot point Y offset, used as the center of rotation, in model units.</summary>
    public float PivotY { get; set; }

    /// <summary>Pivot point Z offset, used as the center of rotation, in model units.</summary>
    public float PivotZ { get; set; }

    /// <summary>Default scale multiplier along the X axis.</summary>
    public float ScaleX { get; set; }

    /// <summary>Default scale multiplier along the Y axis.</summary>
    public float ScaleY { get; set; }

    /// <summary>Default scale multiplier along the Z axis.</summary>
    public float ScaleZ { get; set; }

    /// <summary>
    /// When set to <c>1</c>, this node rotates with the player's head pitch,
    /// allowing it to look up and down independently of the body.
    /// </summary>
    public float Head { get; set; }
}