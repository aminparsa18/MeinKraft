using OpenTK.Mathematics;
/// <summary>
/// Stores and provides the most recently set modelview and projection matrices.
/// Updated each frame by the rendering pipeline via <see cref="LastModelViewMatrix"/>
/// and <see cref="LastProjectionMatrix"/>.
/// </summary>
public class CameraMatrixProvider : ICameraMatrixProvider
{
    /// <summary>The most recently set modelview matrix.</summary>
    public Matrix4 LastModelViewMatrix { get; set; }

    /// <summary>The most recently set projection matrix.</summary>
    public Matrix4 LastProjectionMatrix { get; set; }

    /// <inheritdoc/>
    public Matrix4 GetModelViewMatrix() => LastModelViewMatrix;

    /// <inheritdoc/>
    public Matrix4 GetProjectionMatrix() => LastProjectionMatrix;
}
