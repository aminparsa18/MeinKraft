namespace LibNoise.Modifiers;

/// <summary>
/// Post-processor that applies a linear scale and bias to the output of another
/// module: <c>result = source * Scale + Bias</c>.
/// Useful for remapping noise into a specific value range — for example,
/// scaling terrain height into world units or biasing a [-1, 1] signal to [0, 1].
/// </summary>
public class ScaleBiasOutput : IModule
{
    public float Scale { get; set; }
    public float Bias { get; set; }
    public IModule SourceModule { get; set; }

    public ScaleBiasOutput(IModule sourceModule)
    {
        ArgumentNullException.ThrowIfNull(sourceModule);
        SourceModule = sourceModule;
        Scale = 1f;
        Bias = 0f;
    }

    public float GetValue(float x, float y, float z)
        => (SourceModule.GetValue(x, y, z) * Scale) + Bias;
}