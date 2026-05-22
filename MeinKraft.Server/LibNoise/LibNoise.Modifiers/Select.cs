using System.Runtime.CompilerServices;

namespace LibNoise.Modifiers;

/// <summary>
/// Blends between two source modules based on the output of a control module.
/// When the control value falls cleanly inside [LowerBound, UpperBound] the
/// output is taken entirely from <see cref="SourceModule2"/>; outside that range
/// it comes from <see cref="SourceModule1"/>. A non-zero <see cref="EdgeFalloff"/>
/// adds a smooth transition band of that width on each side of both bounds,
/// using a cubic S-curve blend.
/// </summary>
public class Select : IModule
{
    private float _lowerBound;
    private float _upperBound;
    private float _edgeFalloff;

    public IModule ControlModule { get; set; }
    public IModule SourceModule1 { get; set; }
    public IModule SourceModule2 { get; set; }

    public float LowerBound => _lowerBound;
    public float UpperBound => _upperBound;

    /// <summary>
    /// Width of the smooth transition band on each side of the selection
    /// boundaries. Clamped to half the range width to prevent bands overlapping.
    /// </summary>
    public float EdgeFalloff
    {
        get => _edgeFalloff;
        set
        {
            float halfRange = (_upperBound - _lowerBound) * 0.5f;
            _edgeFalloff = value > halfRange ? halfRange : value;
        }
    }

    /// <summary>
    /// Sets the selection range and clamps the current <see cref="EdgeFalloff"/>
    /// to the new half-range if necessary.
    /// </summary>
    public void SetBounds(float lower, float upper)
    {
        _lowerBound = lower;
        _upperBound = upper;
        EdgeFalloff = _edgeFalloff; // re-clamp falloff to new range
    }

    public float GetValue(float x, float y, float z)
    {
        float control = ControlModule.GetValue(x, y, z);
        float falloff = _edgeFalloff;
        float lowerBound = _lowerBound;
        float upperBound = _upperBound;

        if (falloff > 0f)
        {
            // Pre-compute the four band edges once — used in both the branch
            // condition and the blend math, so this saves four subtractions.
            float lLow = lowerBound - falloff;
            float lHigh = lowerBound + falloff;
            float uLow = upperBound - falloff;
            float uHigh = upperBound + falloff;

            if (control < lLow)
            {
                return SourceModule1.GetValue(x, y, z);
            }

            if (control < lHigh)
            {
                // Blend zone at lower boundary: Module1 → Module2.
                // Each source is sampled exactly once and cached.
                float alpha = SCurve3((control - lLow) / (lHigh - lLow));
                return Lerp(
                    SourceModule1.GetValue(x, y, z),
                    SourceModule2.GetValue(x, y, z),
                    alpha);
            }

            if (control < uLow)
            {
                return SourceModule2.GetValue(x, y, z);
            }

            if (control < uHigh)
            {
                // Blend zone at upper boundary: Module2 → Module1.
                float alpha = SCurve3((control - uLow) / (uHigh - uLow));
                return Lerp(
                    SourceModule2.GetValue(x, y, z),
                    SourceModule1.GetValue(x, y, z),
                    alpha);
            }

            return SourceModule1.GetValue(x, y, z);
        }

        // Hard select — no blending.
        return (control >= lowerBound && control <= upperBound)
            ? SourceModule2.GetValue(x, y, z)
            : SourceModule1.GetValue(x, y, z);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Cubic S-curve: 3t² - 2t³.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float SCurve3(float t) => t * t * (3f - (2f * t));

    /// <summary>Inline linear interpolation.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Lerp(float a, float b, float t) => a + (t * (b - a));
}