/// <summary>
/// Carries per-frame hints from the game logic to the animation system,
/// describing the player's current physical state so the renderer can
/// select or blend the appropriate animation.
/// </summary>
public class AnimationHint
{
    /// <summary>
    /// Whether the player is currently inside a vehicle.
    /// Used to switch to a seated animation pose.
    /// </summary>
    public bool InVehicle { get; set; }

    /// <summary>
    /// World-space draw offset applied to the model's render position.
    /// Used to fine-tune alignment when the model origin does not match
    /// the entity's logical position.
    /// </summary>
    public float DrawFixX { get; set; }

    /// <inheritdoc cref="DrawFixX"/>
    public float DrawFixY { get; set; }

    /// <inheritdoc cref="DrawFixX"/>
    public float DrawFixZ { get; set; }

    /// <summary>
    /// Whether the player is pressing the strafe-left key (A).
    /// Used to trigger a lean-left animation.
    /// </summary>
    public bool LeanLeft { get; set; }

    /// <summary>
    /// Whether the player is pressing the strafe-right key (D).
    /// Used to trigger a lean-right animation.
    /// </summary>
    public bool LeanRight { get; set; }
}