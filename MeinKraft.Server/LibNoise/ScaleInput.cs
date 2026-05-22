namespace LibNoise;

/// <summary>
/// Pre-processor that independently scales each input axis before passing the
/// coordinates to the source module. Allows stretching or compressing the noise
/// field per-axis — for example, flattening terrain noise vertically or
/// elongating cloud patterns horizontally.
/// </summary>
public class ScaleInput : IModule
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }

    public IModule SourceModule { get; set; }

    public ScaleInput(IModule sourceModule, float x = 1f, float y = 1f, float z = 1f)
    {
        ArgumentNullException.ThrowIfNull(sourceModule);
        SourceModule = sourceModule;
        X = x;
        Y = y;
        Z = z;
    }

    public float GetValue(float x, float y, float z)
        => SourceModule.GetValue(x * X, y * Y, z * Z);
}