namespace LibNoise.Modifiers;

public class Constant : IModule
{
    public float Value { get; set; }

    public float GetValue(float x, float y, float z) => Value;
}
