namespace LibNoise;

/// <summary>
/// Provides a deterministic integer hash and its normalised floating-point
/// form for use as a building block in value noise generators.
/// <para>
/// <see cref="IntValueNoise"/> maps a 3D integer grid position and seed to a
/// pseudo-random integer in [0, 2³¹ − 1] using a polynomial bit-scrambling
/// hash. <see cref="ValueNoise"/> normalises that result to approximately
/// [−1, 1] by dividing by 2³⁰ and subtracting from 1.
/// </para>
/// <para>
/// Unlike gradient noise (<see cref="GradientNoiseBasis"/>), this produces
/// no permutation table or gradient vectors — it is cheap and stateless.
/// </para>
/// </summary>
public class ValueNoiseBasis
{
    /// <summary>
    /// Returns a deterministic pseudo-random integer in [0, 2³¹ − 1]
    /// for the given grid position and seed.
    /// <para>
    /// Uses a polynomial bit-scrambling hash:
    /// mix the coordinates and seed with primes, then apply an XOR-shift
    /// and a quadratic finalisation to produce a well-distributed result.
    /// </para>
    /// </summary>
    /// <param name="x">Integer grid X coordinate.</param>
    /// <param name="y">Integer grid Y coordinate.</param>
    /// <param name="z">Integer grid Z coordinate.</param>
    /// <param name="seed">Seed value that varies the output pattern.</param>
    public int IntValueNoise(int x, int y, int z, int seed)
    {
        // Mix coordinates and seed using large primes to spread bits.
        int n = ((1619 * x) + (31337 * y) + (6971 * z) + (1013 * seed)) & 0x7FFFFFFF;

        // XOR-shift to further scramble the bits.
        n = (n >> 13) ^ n;

        // Polynomial finalisation — produces a well-distributed output in [0, 2³¹ − 1].
        return ((n * ((n * n * 60493) + 19990303)) + 1376312589) & 0x7FFFFFFF;
    }

    /// <summary>
    /// Returns a normalised noise value in approximately [−1, 1] for the given
    /// grid position, using seed 0.
    /// </summary>
    public float ValueNoise(int x, int y, int z) => ValueNoise(x, y, z, 0);

    /// <summary>
    /// Returns a normalised noise value in approximately [−1, 1] for the given
    /// grid position and seed.
    /// Divides <see cref="IntValueNoise"/> by 2³⁰ (1 073 741 824) and subtracts
    /// from 1 to map the integer range to [−1, 1].
    /// </summary>
    public float ValueNoise(int x, int y, int z, int seed)
    => 1f - (IntValueNoise(x, y, z, seed) * (1f / 1_073_741_824f));
}