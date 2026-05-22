/// <summary>
/// Defines a named animation clip, specifying its identifier and total duration.
/// </summary>
public class Animation
{
    /// <summary>
    /// Unique name identifying this animation clip within the model.
    /// Used to look up the animation by name in <see cref="AnimatedModelRenderer.SetAnimation"/>.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Total duration of this animation clip, measured in frames.
    /// At <see cref="AnimatedModelRenderer.AnimationFrameRate"/> fps, divide by that value to get seconds.
    /// </summary>
    public int Length { get; set; }
}