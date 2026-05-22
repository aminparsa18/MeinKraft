using OpenTK.Mathematics;

/// <summary>
/// Controls an orbiting camera that always looks at a fixed <see cref="Center"/> point —
/// similar to a strategy-game camera or Minecraft's right-click orbit view.
/// Three parameters drive the orbit:
/// <list type="bullet">
///   <item><term>Distance</term><description>Orbit radius (zoom in/out).</description></item>
///   <item><term>Azimuth</term><description>Horizontal rotation around the center (spin left/right).</description></item>
///   <item><term>Angle</term><description>Elevation above the horizontal plane (ground level → bird's-eye).</description></item>
/// </list>
/// Elevation is clamped to [0°, 89°] so the camera can never flip upside-down.
/// Distance has a minimum so the camera can never pass through the target point.
/// </summary>
public interface ICameraService
{
    /// <summary>The world-space point the camera orbits around.</summary>
    Vector3 Center { get; set; }

    /// <summary>
    /// Computes the world-space position of the camera eye and writes it into <paramref name="ret"/>.
    /// Position is derived from the current azimuth, elevation angle, and orbit distance.
    /// </summary>
    void GetPosition(ref Vector3 ret);

    /// <summary>
    /// Applies a <see cref="CameraMoveArgs"/> input event to the camera for one frame.
    /// Turn deltas are scaled by 4; elevation deltas are scaled by 40.
    /// Distance is set directly from <see cref="CameraMoveArgs.Distance"/>.
    /// </summary>
    void Move(CameraMoveArgs cameraMoveArgs, float dt);

    /// <summary>Rotates the camera counter-clockwise (left) by <paramref name="delta"/> radians.</summary>
    void TurnLeft(float delta);

    /// <summary>Rotates the camera clockwise (right) by <paramref name="delta"/> radians.</summary>
    void TurnRight(float delta);

    /// <summary>Increases the elevation angle by <paramref name="delta"/> degrees, clamped to the valid range.</summary>
    void TurnUp(float delta);

    /// <summary>Decreases the elevation angle by <paramref name="delta"/> degrees, clamped to the valid range.</summary>
    void TurnDown(float delta);

    /// <summary>Distance from the player in overhead mode.</summary>
    float OverHeadCameraDistance { get; set; }

    /// <summary>Octree searcher used for block pick rays.</summary>
    BlockOctreeSearcher BlockOctreeSearcher { get; }
}