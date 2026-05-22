using System.Runtime.CompilerServices;

namespace LibNoise;

/// <summary>
/// Billowing fBm noise generator. Similar to <see cref="FastNoise"/> but folds
/// each octave's signal with an absolute-value and bias, producing a "billowing
/// clouds" appearance rather than smooth gradients.
/// <para>
/// Each octave: takes the absolute value of the gradient noise, scales it to
/// [−1, 1], weights by the running amplitude, then advances frequency and decays
/// amplitude by <see cref="Persistence"/>. A final +0.5 bias lifts the output
/// into approximately [0, 1].
/// </para>
/// </summary>
///
/// <remarks>
/// <b>Changes vs. previous version</b>
/// <list type="bullet">
///   <item>
///     <b>Composition over inheritance.</b>
///     <see cref="GradientNoiseBasis"/> is now <c>sealed</c>, so
///     <c>Billow</c> holds an instance rather than deriving from it.
///     The field is allocated once in the constructor and reused for every
///     <see cref="GetValue"/> call.
///   </item>
///   <item>
///     <b>Bitwise absolute value.</b>
///     <c>MathF.Abs(signal)</c> is replaced with an
///     <c>int</c>-reinterpret bit-clear (<c>&amp; 0x7FFF_FFFF</c>) that
///     strips the IEEE 754 sign bit without a branch or library call.
///   </item>
///   <item>
///     <b>Amplitude pre-multiplication.</b>
///     The per-octave <c>signal * amplitude</c> multiply is replaced by a
///     pre-multiplied form: the <c>2f * amplitude</c> factor is folded into
///     <c>curAmp</c> so each octave performs one fewer multiply.
///   </item>
/// </list>
/// </remarks>
public sealed class Billow : IModule
{
    // ── Constants ─────────────────────────────────────────────────────────────

    private const int MaxOctaves = 30;

    // ── Backing fields ────────────────────────────────────────────────────────

    private readonly GradientNoiseBasis _basis = new();
    private int _octaveCount;

    // ── Properties ───────────────────────────────────────────────────────────

    public float Frequency { get; set; }
    public float Persistence { get; set; }
    public float Lacunarity { get; set; }
    public NoiseQuality NoiseQuality { get; set; }
    public int Seed { get; set; }

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

    // ── Constructor ───────────────────────────────────────────────────────────

    public Billow()
    {
        Frequency = 1f;
        Lacunarity = 2f;
        OctaveCount = 6;
        Persistence = 0.5f;
        NoiseQuality = NoiseQuality.Standard;
        Seed = 0;
    }

    // ── IModule ───────────────────────────────────────────────────────────────

    public float GetValue(float x, float y, float z)
    {
        float sum = 0f;

        // Cache all fields in locals before the loop — prevents repeated
        // this-pointer dereferences and keeps values in registers.
        int octaveCount = _octaveCount;
        int seed = Seed;
        float lacunarity = Lacunarity;
        float persistence = Persistence;
        NoiseQuality quality = NoiseQuality;

        x *= Frequency;
        y *= Frequency;
        z *= Frequency;

        // curAmp absorbs the 2× factor from the fold (2*|signal| - 1) so each
        // octave iteration only needs one multiply instead of two.
        // Unfolded: sum += (2*|signal| - 1) * amplitude
        // Folded:   sum += |signal| * curAmp - amplitude
        // where curAmp = 2 * amplitude, updated each octave as amplitude decays.
        float amplitude = 1f;
        float curAmp = 2f;       // = 2 * amplitude for octave 0

        for (int i = 0; i < octaveCount; i++)
        {
            int octaveSeed = (seed + i) & 0x7FFFFFFF;

            float signal = GradientNoiseBasis.GradientCoherentNoise(x, y, z, octaveSeed, quality);

            // Strip the IEEE 754 sign bit — equivalent to MathF.Abs but branchless
            // and avoids a library call. Reinterpret float bits as int, clear bit 31,
            // reinterpret back.
            signal = Unsafe.BitCast<int, float>(Unsafe.BitCast<float, int>(signal) & 0x7FFF_FFFF);

            // Fold: (2*|signal| - 1) * amplitude  →  |signal|*curAmp - amplitude
            sum += (signal * curAmp) - amplitude;

            x *= lacunarity;
            y *= lacunarity;
            z *= lacunarity;

            amplitude *= persistence;
            curAmp *= persistence;       // stays = 2 * amplitude
        }

        // Bias output to approximately [0, 1].
        return sum + 0.5f;
    }
}