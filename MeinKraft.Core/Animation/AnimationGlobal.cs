/// <summary>
/// Stores the shared texture atlas dimensions used by all nodes in the model.
/// These are used to normalize UV coordinates from pixel space to 0-1 range
/// in <see cref="CuboidRenderer.CuboidNetNormalize"/>.
/// </summary>
public class AnimationGlobal
{
    /// <summary>Width of the texture atlas, in pixels.</summary>
    public int TexW { get; set; }

    /// <summary>Height of the texture atlas, in pixels.</summary>
    public int TexH { get; set; }
}