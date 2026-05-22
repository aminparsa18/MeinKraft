/// <summary>
/// Represents a complete animated model, containing the node hierarchy,
/// keyframe data, animation definitions, and texture metadata.
/// </summary>
public class AnimatedModel
{
    /// <summary>
    /// Initializes a new empty <see cref="AnimatedModel"/> with
    /// pre-allocated lists ready for deserialization.
    /// </summary>
    public AnimatedModel()
    {
        Nodes = [];
        Keyframes = [];
        Animations = [];
        Global = new AnimationGlobal();
    }

    /// <summary>The node (bone) hierarchy that makes up the model's skeleton.</summary>
    public List<Node> Nodes { get; set; }

    /// <summary>All keyframes across all animations and nodes.</summary>
    public List<Keyframe> Keyframes { get; set; }

    /// <summary>The set of named animations defined for this model.</summary>
    public List<Animation> Animations { get; set; }

    /// <summary>Texture dimensions shared across all nodes in this model.</summary>
    public AnimationGlobal Global { get; set; }
}