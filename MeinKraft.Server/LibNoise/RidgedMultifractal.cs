using System.Runtime.CompilerServices;

namespace LibNoise;

/// <summary>
/// Ridged multifractal noise generator. Produces sharp ridges and smooth valleys,
/// suitable for mountain ranges, cliff edges, and canyon walls.
/// <para>
/// Unlike standard fBm (<see cref="Perlin"/>), each octave's signal is
/// folded (absolute value), inverted, and squared before being weighted by
/// the previous octave's output. This feedback loop creates sharp discontinuities
/// at ridge lines while keeping valleys smooth.
/// </para>
/// <para>
/// Used in <c>DefaultWorldGenerator</c> for the mountain terrain layer.
/// </para>
/// </summary>
public sealed class RidgedMultifractal : IModule
{
    private const int MaxOctaves = 30;

    private readonly GradientNoiseBasis _basis = new();
    private int _octaveCount;
    private float _lacunarity;

    /// <summary>
    /// Pre-computed per-octave amplitude weights based on <see cref="Lacunarity"/>.
    /// Recalculated whenever <see cref="Lacunarity"/> changes.
    /// </summary>
    private readonly float[] _spectralWeights = new float[MaxOctaves];

    // ── Parameters ────────────────────────────────────────────────────────────

    public float Frequency { get; set; }
    public NoiseQuality NoiseQuality { get; set; }
    public int Seed { get; set; }

    /// <summary>
    /// Frequency multiplier between successive octaves.
    /// Setting this recalculates <see cref="_spectralWeights"/> immediately.
    /// </summary>
    public float Lacunarity
    {
        get => _lacunarity;
        set
        {
            _lacunarity = value;
            CalculateSpectralWeights();
        }
    }

    public int OctaveCount
    {
        get => _octaveCount;
        set
        {
            if (value is < 1 or > MaxOctaves)
            {
                throw new ArgumentException(
                    $"OctaveCount must be between 1 and {MaxOctaves}, got {value}.");
            }

            _octaveCount = value;
        }
    }

    // ── Construction ──────────────────────────────────────────────────────────

    public RidgedMultifractal()
    {
        Frequency = 1f;
        Lacunarity = 2f;   // also calls CalculateSpectralWeights()
        OctaveCount = 6;
        NoiseQuality = NoiseQuality.Standard;
        Seed = 0;
    }

    // ── IModule ───────────────────────────────────────────────────────────────

    public float GetValue(float x, float y, float z)
    {
        x *= Frequency;
        y *= Frequency;
        z *= Frequency;

        float sum = 0f;
        float weight = 1f;

        const float offset = 1f;
        const float gain = 2f;

        int octaveCount = _octaveCount;
        float lacunarity = _lacunarity;
        int seed = Seed;
        NoiseQuality quality = NoiseQuality;
        ReadOnlySpan<float> sw = _spectralWeights;

        for (int i = 0; i < octaveCount; i++)
        {
            int octaveSeed = (seed + i) & 0x7FFFFFFF;

            float signal = GradientNoiseBasis.GradientCoherentNoise(x, y, z, octaveSeed, quality);

            // Bitwise abs: clear the IEEE 754 sign bit — branchless, no library call.
            signal = Unsafe.BitCast<int, float>(Unsafe.BitCast<float, int>(signal) & 0x7FFF_FFFF);

            signal = offset - signal;
            signal *= signal;

            // Feedback: weight this octave by the previous signal so ridges
            // compound across octaves rather than blending uniformly.
            signal *= weight;
            weight = Clamp01(signal * gain);

            sum += signal * sw[i];

            x *= lacunarity;
            y *= lacunarity;
            z *= lacunarity;
        }

        return (sum * 1.25f) - 1f;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

    // ── Spectral weights ──────────────────────────────────────────────────────

    private void CalculateSpectralWeights()
    {
        const float H = 1f;
        float frequency = 1f;
        for (int i = 0; i < MaxOctaves; i++)
        {
            _spectralWeights[i] = MathF.Pow(frequency, -H);
            frequency *= _lacunarity;
        }
    }
}