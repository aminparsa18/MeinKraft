namespace LibNoise;

/// <summary>
/// Controls the interpolation quality used when sampling coherent noise.
/// Higher quality produces smoother results at the cost of additional computation.
/// </summary>
public enum NoiseQuality
{
    /// <summary>
    /// Linear interpolation — fastest but produces visible grid artifacts
    /// at low sample frequencies.
    /// </summary>
    Low,

    /// <summary>
    /// Cubic S-curve (smoothstep) interpolation — good balance of quality
    /// and performance. The default for most noise generators.
    /// </summary>
    Standard,

    /// <summary>
    /// Quintic S-curve (smootherstep) interpolation — smoothest result with
    /// zero first and second derivatives at cell boundaries, eliminating
    /// visual discontinuities. Slightly more expensive than <see cref="Standard"/>.
    /// </summary>
    High,
}