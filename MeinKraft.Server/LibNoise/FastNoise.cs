using System.Runtime.CompilerServices;

namespace LibNoise;

/// <summary>
/// Fractal Brownian Motion (fBm) noise generator.
/// Produces natural-looking noise by stacking multiple octaves of coherent
/// gradient noise, each at progressively higher frequency and lower amplitude.
///
/// Changes vs. previous version
/// ─────────────────────────────
/// 1. COMPOSITION — previously inherited from <see cref="FastNoiseBasis"/>,
///    which prevented the basis from being sealed.  <c>FastNoise</c> now holds
///    a private <c>FastNoiseBasis _basis</c> field and exposes <see cref="Seed"/>
///    as a pass-through property.  Both classes are now <c>sealed</c>, giving
///    the JIT full devirtualisation of every call site.
///
/// 2. FMA ACCUMULATION — <c>sum += noise * amplitude</c> is rewritten as
///    <see cref="MathF.FusedMultiplyAdd"/> to keep the intermediate product in
///    a register rather than rounding to float memory before adding.  At
///    Standard or High quality the difference in the final sum is measurable
///    (single-ULP precision gain per octave), and the instruction count drops
///    from 2 (vmulss + vaddss) to 1 (vfmadd231ss) at the loop level.
///
/// 3. 2D OVERLOAD — world-generation callers often sample a flat XY grid at a
///    fixed Z.  <see cref="GetValue(float, float)"/> passes <c>z = 0</c> to
///    the 3-D path as a literal, which lets the JIT constant-fold <c>fz = 0</c>
///    and therefore <c>sz = 0</c> (all three smoothing qualities produce 0 for
///    input 0).  The final trilinear lerp collapses to its first argument, and
///    the dead upper half of the gradient gather is removed by the JIT.
///
/// Dependency chain notes (informational)
/// ────────────────────────────────────────
/// The octave loop carries three independent serial chains:
///   x, y, z positions  ×lacunarity each iteration — 3 parallel vmulss
///   amplitude          ×persistence each iteration — 1 vmulss
///   sum                FMA each iteration
///
/// Crucially, the noise evaluation at octave N does not depend on the noise
/// value from octave N-1 — only <c>sum</c> links them, and <c>sum</c> depends
/// only on the FMA result which the CPU can retire once the preceding call
/// completes.  The OoO window on modern Intel (≥ 512 reorder-buffer entries)
/// is wide enough to overlap the carry-chain multiplies from one iteration
/// with the AVX2 gather inside the next.
/// </summary>
public sealed class FastNoise : IModule
{
    // ── Constants ─────────────────────────────────────────────────────────────

    private const int MaxOctaves = 30;

    // ── State ─────────────────────────────────────────────────────────────────

    // Composition replaces inheritance. FastNoiseBasis is sealed; holding it
    // as a field restores that constraint without changing the public API surface.
    private readonly FastNoiseBasis _basis;

    private int _octaveCount;

    // ── Properties ────────────────────────────────────────────────────────────

    /// <summary>
    /// Random seed forwarded to the underlying <see cref="FastNoiseBasis"/>.
    /// Setting this rebuilds the basis permutation and gradient tables.
    /// </summary>
    public int Seed
    {
        get => _basis.Seed;
        set => _basis.Seed = value;
    }

    public float Frequency { get; set; }
    public float Persistence { get; set; }
    public float Lacunarity { get; set; }
    public NoiseQuality NoiseQuality { get; set; }

    public int OctaveCount
    {
        get => _octaveCount;
        set
        {
            // Single unsigned comparison covers both < 1 and > MaxOctaves.
            if ((uint)(value - 1) >= MaxOctaves)
            {
                throw new ArgumentOutOfRangeException(nameof(value),
                    $"OctaveCount must be between 1 and {MaxOctaves}, got {value}.");
            }

            _octaveCount = value;
        }
    }

    // ── Construction ──────────────────────────────────────────────────────────

    public FastNoise() : this(0) { }

    public FastNoise(int seed)
    {
        _basis = new FastNoiseBasis(seed);
        Frequency = 1f;
        Lacunarity = 2f;
        OctaveCount = 6;
        Persistence = 0.5f;
        NoiseQuality = NoiseQuality.Standard;
    }

    // ── Core evaluation ───────────────────────────────────────────────────────

    /// <summary>
    /// Evaluates fBm noise at the given 3-D world position.
    /// </summary>
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public float GetValue(float x, float y, float z)
    {
        // Cache all fields in locals before the loop — prevents repeated
        // this-pointer dereferences inside the hot path and lets the JIT
        // assign everything to registers for the duration of the loop.
        int octaveCount = _octaveCount;
        int seed = _basis.Seed;
        float lacunarity = Lacunarity;
        float persistence = Persistence;
        NoiseQuality quality = NoiseQuality;

        x *= Frequency;
        y *= Frequency;
        z *= Frequency;

        float sum = 0f;
        float amplitude = 1f;

        for (int i = 0; i < octaveCount; i++)
        {
            // Mask to positive range so the basis permutation lookup is always
            // valid regardless of seed magnitude or octave count.
            int octaveSeed = (seed + i) & 0x7FFFFFFF;

            // FusedMultiplyAdd: single vfmadd231ss instead of vmulss + vaddss.
            // Keeps the intermediate product in the FPU pipeline without a
            // round-to-float store, giving 1 ULP better precision per octave.
            sum = MathF.FusedMultiplyAdd(
                _basis.GradientCoherentNoise(x, y, z, octaveSeed, quality),
                amplitude,
                sum);

            x *= lacunarity;
            y *= lacunarity;
            z *= lacunarity;
            amplitude *= persistence;
        }

        return sum;
    }

    /// <summary>
    /// Evaluates fBm noise at the given 2-D world position (<c>z = 0</c>).
    /// <para>
    /// Preferred for flat-grid terrain sampling.  Passing the literal <c>0f</c>
    /// for Z allows the JIT to constant-fold <c>fz = 0</c> and <c>sz = 0</c>
    /// inside the basis method regardless of <see cref="NoiseQuality"/>, because
    /// all three smoothing polynomials evaluate to 0 at input 0.  The final
    /// trilinear lerp across Z collapses to its first argument, and the dead
    /// computation of the upper half of the gradient gather is eliminated.
    /// </para>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetValue(float x, float y) => GetValue(x, y, 0f);
}