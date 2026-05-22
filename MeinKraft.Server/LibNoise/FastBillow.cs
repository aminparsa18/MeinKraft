namespace LibNoise;

/// <summary>
/// Billowing fBm noise generator backed by <see cref="FastNoiseBasis"/>.
/// Produces the same cloud-like, folded appearance as <see cref="Billow"/> but
/// uses the cheaper permutation-table gradient lookup instead of full 3-D vector
/// dot products, making it faster at the cost of slightly lower noise quality.
/// <para>
/// Each octave folds the gradient signal with <c>2 × |signal| − 1</c>, weights
/// by the pre-computed amplitude, then samples the next octave at a higher
/// frequency.  A final +0.5 bias lifts the output to approximately [0, 1].
/// </para>
///
/// Changes vs. previous version
/// ─────────────────────────────
/// Identical optimisation to <see cref="FastNoise"/>: the four serial multiply
/// chains that advanced x/y/z/amplitude each iteration are replaced by
/// pre-computed <c>_scales[]</c> and <c>_amplitudes[]</c> tables, making each
/// octave an independent expression with no data dependency on the previous one.
///
/// The billow fold (<c>MathF.Abs</c> + scale + bias) operates on the noise
/// return value, not on the coordinates, so it does not affect the independence
/// analysis and compiles to a single ANDPS + VFMADD instruction pair.
///
/// See <see cref="FastNoise"/> for full commentary on the table pre-computation
/// strategy, lazy rebuild, allocation reuse, and sealed-class devirtualization.
/// </summary>
public sealed class FastBillow : FastNoiseBasis, IModule
{
    private const int MaxOctaves = 30;

    // ── Pre-computed octave tables ────────────────────────────────────────────
    //
    // _scales[i]     = Frequency × Lacunarity^i
    // _amplitudes[i] = Persistence^i

    private float[] _scales = [];
    private float[] _amplitudes = [];
    private bool _tablesDirty = true;

    // ── Backing fields ────────────────────────────────────────────────────────

    private int _octaveCount = 6;
    private float _frequency = 1f;
    private float _lacunarity = 2f;
    private float _persistence = 0.5f;
    private NoiseQuality _noiseQuality = NoiseQuality.Standard;

    // ── Properties ────────────────────────────────────────────────────────────

    public NoiseQuality NoiseQuality
    {
        get => _noiseQuality;
        set => _noiseQuality = value;
    }

    public float Frequency
    {
        get => _frequency;
        set
        {
            _frequency = value;
            _tablesDirty = true;
        }
    }

    public float Lacunarity
    {
        get => _lacunarity;
        set
        {
            _lacunarity = value;
            _tablesDirty = true;
        }
    }

    public float Persistence
    {
        get => _persistence;
        set
        {
            _persistence = value;
            _tablesDirty = true;
        }
    }

    public int OctaveCount
    {
        get => _octaveCount;
        set
        {
            if ((uint)(value - 1) >= MaxOctaves)
            {
                throw new ArgumentException(
                    $"OctaveCount must be between 1 and {MaxOctaves}, got {value}.");
            }

            _octaveCount = value;
            _tablesDirty = true;
        }
    }

    // ── Construction ──────────────────────────────────────────────────────────

    public FastBillow() : this(0) { }

    public FastBillow(int seed) : base(seed) { }

    // ── Core ──────────────────────────────────────────────────────────────────

    /// <summary>Evaluates the billowing fBm signal at the given world position.</summary>
    public float GetValue(float x, float y, float z)
    {
        if (_tablesDirty)
        {
            RebuildTables();
        }

        float sum = 0f;
        int seed = Seed;
        NoiseQuality quality = _noiseQuality;

        for (int i = 0; i < _octaveCount; i++)
        {
            float s = _scales[i];
            float signal = GradientCoherentNoise(x * s, y * s, z * s,
                                                 unchecked(seed + i) & 0x7FFFFFFF,
                                                 quality);

            // Billow fold: reflect negative values upward and rescale to [-1, 1].
            // MathF.Abs compiles to ANDPS (clears sign bit — one instruction).
            // The 2×|signal|−1 expression then maps [0,1] → [-1,1] before weighting.
            sum += ((2f * MathF.Abs(signal)) - 1f) * _amplitudes[i];
        }

        // Lift output to approximately [0, 1].
        return sum + 0.5f;
    }

    // ── Table management ──────────────────────────────────────────────────────

    private void RebuildTables()
    {
        int n = _octaveCount;

        if (_scales.Length != n)
        {
            _scales = new float[n];
            _amplitudes = new float[n];
        }

        float scale = _frequency;
        float amplitude = 1f;

        for (int i = 0; i < n; i++)
        {
            _scales[i] = scale;
            _amplitudes[i] = amplitude;
            scale *= _lacunarity;
            amplitude *= _persistence;
        }

        _tablesDirty = false;
    }
}