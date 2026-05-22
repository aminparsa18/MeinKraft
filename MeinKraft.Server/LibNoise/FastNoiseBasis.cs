using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace LibNoise;

/// <summary>
/// Low-level gradient noise basis used by <see cref="FastNoise"/> and other
/// LibNoise modules. Implements the permutation-table approach from Ken Perlin's
/// original noise algorithm to produce smooth, coherent pseudo-random values.
///
/// Changes vs. previous version
/// ─────────────────────────────
/// 1. PERMUTATION CHAIN — the 3-level lookup chain has an irreducible 2-level
///    serial data dependency that cannot be removed without changing the noise
///    output:
///
///      Level 1 (scalar — serial, unavoidable):
///        a = perm[cx]     + cy
///        b = perm[cx + 1] + cy
///
///      Level 2 (4-wide gather — parallel once a and b are known):
///        perm[a], perm[a+1], perm[b], perm[b+1]  → [aa, ab, ba, bb]
///
///      Level 3 (8-wide gather — parallel once aa/ab/ba/bb are known):
///        grad[aa], grad[ab], grad[aa+1], grad[ab+1],
///        grad[ba], grad[bb], grad[ba+1], grad[bb+1]
///
///    The AVX2 path replaces the 4 sequential reads in level 2 with a single
///    GatherVector128 and the 8 sequential reads in level 3 with a single
///    GatherVector256.  Both gathers issue all cache-line fetches simultaneously,
///    hiding latency on cold data.
///
/// 2. AVX2 GRADIENT INDEX BUILD — after the 4-wide level-2 gather produces
///    corners = [aa, ab, ba, bb], two Sse2.Shuffle + add operations expand
///    this to the 8-element layout needed by GatherVector256:
///
///      Shuffle 0x44 → [aa, ab, aa, ab]  + [0,0,1,1] = [aa, ab, aa+1, ab+1]  (lower 128)
///      Shuffle 0xEE → [ba, bb, ba, bb]  + [0,0,1,1] = [ba, bb, ba+1, bb+1]  (upper 128)
///
///    Corner layout chosen so that GetLower / GetUpper naturally separate the
///    x=cx and x=cx+1 groups for the first lerp level (zero instructions).
///
/// 3. LERP TREE — identical to the one used in GradientNoiseBasis:
///      level 1  GetLower / GetUpper split  (zero cost — register alias)
///               + 128-bit FMA across x
///      level 2  Sse.Shuffle 0x88 / 0xDD  + 128-bit FMA across y
///      level 3  two GetElement + scalar lerp across z
///
/// 4. SCALAR FALLBACK — unchanged algorithm, kept for non-AVX2 hardware.
///    AggressiveInlining allows the JIT to eliminate it entirely when it can
///    prove s_useAvx2 is true at the call site.
///
/// 5. SEED SETTER — intermediate temp arrays eliminated; gradient table
///    mirroring uses Span.CopyTo for a single bulk memcpy.
/// </summary>
public class FastNoiseBasis
{
    // ── Permutation / gradient tables ─────────────────────────────────────────

    /// <summary>
    /// Doubled permutation table (0–511) so index arithmetic never wraps —
    /// avoids a modulo on every lookup.
    /// </summary>
    private readonly int[] _permutations = new int[512];

    /// <summary>
    /// Pre-computed gradient values in [-1, 1] mapped through the permutation
    /// table.  Doubled to 512 entries to match the permutation table layout.
    /// </summary>
    private readonly float[] _gradients = new float[512];

    private int _seed;

    // Runtime capability flag — checked once at construction, never again in
    // the hot path.
    private static readonly bool s_useAvx2 = Avx2.IsSupported && Fma.IsSupported;

    // ── Seed ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Random seed that determines the noise pattern.
    /// Setting this rebuilds the permutation and gradient tables immediately.
    /// Must be non-negative.
    /// </summary>
    public int Seed
    {
        get => _seed;
        set
        {
            _seed = value;

            // Fill perm[0..255] from a seeded RNG and mirror to [256..511].
            Random rng = new(_seed);
            for (int i = 0; i < 256; i++)
            {
                _permutations[i] = rng.Next(255);
            }

            _permutations.AsSpan(0, 256).CopyTo(_permutations.AsSpan(256));

            // Map each perm entry to a gradient in [-1, 1] and mirror.
            const float inv255 = 1f / 255f;
            for (int i = 0; i < 256; i++)
            {
                _gradients[i] = -1f + (2f * (_permutations[i] * inv255));
            }

            _gradients.AsSpan(0, 256).CopyTo(_gradients.AsSpan(256));
        }
    }

    // ── Construction ──────────────────────────────────────────────────────────

    /// <summary>Initialises a <see cref="FastNoiseBasis"/> with the given seed.</summary>
    /// <exception cref="ArgumentException">Thrown when <paramref name="seed"/> is negative.</exception>
    public FastNoiseBasis(int seed)
    {
        if (seed < 0)
        {
            throw new ArgumentException("Seed must be non-negative.", nameof(seed));
        }

        Seed = seed;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a coherent gradient noise value in approximately [-1, 1] at the
    /// given world position. Dispatches to the AVX2 path when available.
    /// </summary>
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public float GradientCoherentNoise(float x, float y, float z,
        int seed, NoiseQuality noiseQuality)
    {
        // Floor to integer cube origin (handles negative coordinates correctly).
        int ix = x > 0f ? (int)x : (int)x - 1;
        int iy = y > 0f ? (int)y : (int)y - 1;
        int iz = z > 0f ? (int)z : (int)z - 1;

        // Fractional position inside the unit cube.
        float fx = x - ix;
        float fy = y - iy;
        float fz = z - iz;

        // Wrap cube coordinates to [0, 255] for table lookup.
        int cx = ix & 0xFF;
        int cy = iy & 0xFF;
        int cz = iz & 0xFF;

        // Smoothing weights.
        float sx, sy, sz;
        switch (noiseQuality)
        {
            case NoiseQuality.Low:
                sx = fx; sy = fy; sz = fz;
                break;
            case NoiseQuality.Standard:
                sx = fx * fx * (3f - (2f * fx));
                sy = fy * fy * (3f - (2f * fy));
                sz = fz * fz * (3f - (2f * fz));
                break;
            default: // High — quintic, C2-continuous
                sx = fx * fx * fx * ((fx * ((6f * fx) - 15f)) + 10f);
                sy = fy * fy * fy * ((fy * ((6f * fy) - 15f)) + 10f);
                sz = fz * fz * fz * ((fz * ((6f * fz) - 15f)) + 10f);
                break;
        }

        return s_useAvx2
            ? EvaluateAvx2(cx, cy, cz, fx, fy, fz, sx, sy, sz)
            : EvaluateScalar(cx, cy, cz, sx, sy, sz);
    }

    // ── AVX2 path ─────────────────────────────────────────────────────────────
    //
    // Permutation chain
    // ──────────────────
    // The three-level lookup has one unavoidable serial barrier:
    //   level 1 must complete before level 2 can begin, and level 2 before
    //   level 3.  Within each level, however, all reads are independent and
    //   can therefore be issued as a single gather.
    //
    // Level 1 — scalar (serial)
    //   a = perm[cx]     + cy     ← depends on cx; result feeds all of level 2
    //   b = perm[cx + 1] + cy
    //
    // Level 2 — 4-wide GatherVector128 (parallel)
    //   Reads perm[a], perm[a+1], perm[b], perm[b+1] simultaneously.
    //   Adds cz to produce the four base gradient indices:
    //     corners = [aa, ab, ba, bb]
    //
    // Level 3 — 8-wide GatherVector256 (parallel)
    //   Expands corners to the full 8-element gradient index vector:
    //
    //     Sse2.Shuffle 0x44:  [aa, ab, aa, ab]  → + [0,0,1,1] = [aa, ab, aa+1, ab+1]
    //     Sse2.Shuffle 0xEE:  [ba, bb, ba, bb]  → + [0,0,1,1] = [ba, bb, ba+1, bb+1]
    //
    //   The lower 128 bits carry the x=cx group and the upper 128 the x=cx+1
    //   group, exactly as LerpTree expects.
    //
    // Gradient index safety
    // ──────────────────────
    //   _permutations values are rng.Next(255) → [0, 254].
    //   Worst-case index: perm[perm[cx]+cy]+cz = 254+255 = 509.
    //   Including the +1 variant: 510.  _gradients has 512 entries → safe.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe float EvaluateAvx2(
        int cx, int cy, int cz,
        float fx, float fy, float fz,
        float sx, float sy, float sz)
    {
        // ── Level 1: two scalar perm reads (serial dependency, unavoidable) ──
        ReadOnlySpan<int> perm = _permutations;
        int a = perm[cx] + cy;
        int b = perm[cx + 1] + cy;

        // ── Level 2: 4-wide gather → corners = [aa, ab, ba, bb] ──────────────
        Vector128<int> idx2 = Vector128.Create(a, a + 1, b, b + 1);

        fixed (int* pperm = _permutations)
        fixed (float* pgrad = _gradients)
        {
            Vector128<int> p2 = Avx2.GatherVector128(pperm, idx2, 4);
            // p2 = [perm[a], perm[a+1], perm[b], perm[b+1]]

            Vector128<int> corners = Sse2.Add(p2, Vector128.Create(cz));
            // corners = [aa, ab, ba, bb]

            // ── Level 3: expand to 8 gradient indices + gather ────────────────
            //
            // Target layout: lower = x=cx group, upper = x=cx+1 group
            //   lower 128: [aa, ab, aa+1, ab+1]
            //   upper 128: [ba, bb, ba+1, bb+1]
            //
            // Sse2.Shuffle control bytes (same-source, 4-element lane):
            //   0x44 = 0b01_00_01_00 → [c[0], c[1], c[0], c[1]] = [aa, ab, aa, ab]
            //   0xEE = 0b11_10_11_10 → [c[2], c[3], c[2], c[3]] = [ba, bb, ba, bb]

            Vector128<int> adv = Vector128.Create(0, 0, 1, 1);
            Vector128<int> lo128 = Sse2.Add(Sse2.Shuffle(corners, 0x44), adv); // [aa, ab, aa+1, ab+1]
            Vector128<int> hi128 = Sse2.Add(Sse2.Shuffle(corners, 0xEE), adv); // [ba, bb, ba+1, bb+1]
            Vector256<int> gradIdx = Vector256.Create(lo128, hi128);

            Vector256<float> grads = Avx2.GatherVector256(pgrad, gradIdx, 4);
            // grads = [grad[aa], grad[ab], grad[aa+1], grad[ab+1],
            //          grad[ba], grad[bb], grad[ba+1], grad[bb+1]]

            return LerpTree(grads, sx, sy, sz);
        }
    }

    // ── Scalar fallback path ──────────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float EvaluateScalar(
        int cx, int cy, int cz,
        float sx, float sy, float sz)
    {
        // Pin spans once — suppresses repeated bounds checks across all 8 lookups.
        ReadOnlySpan<int> perm = _permutations;
        ReadOnlySpan<float> grad = _gradients;

        int a = perm[cx] + cy;
        int aa = perm[a] + cz;
        int ab = perm[a + 1] + cz;
        int b = perm[cx + 1] + cy;
        int ba = perm[b] + cz;
        int bb = perm[b + 1] + cz;

        float x1 = Lerp(grad[aa], grad[ba], sx);
        float x2 = Lerp(grad[ab], grad[bb], sx);
        float y1 = Lerp(x1, x2, sy);

        float x3 = Lerp(grad[aa + 1], grad[ba + 1], sx);
        float x4 = Lerp(grad[ab + 1], grad[bb + 1], sx);
        float y2 = Lerp(x3, x4, sy);

        return Lerp(y1, y2, sz);
    }

    // ── Trilinear lerp reduction ───────────────────────────────────────────────
    //
    // Identical to GradientNoiseBasis.LerpTree.
    //
    // Input layout (8 gradient values):
    //   lower 128:  [grad[aa], grad[ab], grad[aa+1], grad[ab+1]]   x = cx
    //   upper 128:  [grad[ba], grad[bb], grad[ba+1], grad[bb+1]]   x = cx+1
    //
    //   In (x, y, z) terms:
    //     lower[0] = (cx,   cy,   cz)     upper[0] = (cx+1, cy,   cz)
    //     lower[1] = (cx,   cy+1, cz)     upper[1] = (cx+1, cy+1, cz)
    //     lower[2] = (cx,   cy,   cz+1)   upper[2] = (cx+1, cy,   cz+1)
    //     lower[3] = (cx,   cy+1, cz+1)   upper[3] = (cx+1, cy+1, cz+1)
    //
    // Level 1 — lerp across X
    //   GetLower / GetUpper split at the 128-bit boundary (zero instructions).
    //   FMA: nearX + sx*(farX - nearX)
    //   lx = [lx(y=cy,z=cz),  lx(y=cy+1,z=cz),  lx(y=cy,z=cz+1),  lx(y=cy+1,z=cz+1)]
    //
    // Level 2 — lerp across Y
    //   Sse.Shuffle 0x88: [lx[0], lx[0], lx[2], lx[2]]  (near-y pair, both z levels)
    //   Sse.Shuffle 0xDD: [lx[1], lx[1], lx[3], lx[3]]  (far-y pair,  both z levels)
    //   FMA: nearY + sy*(farY - nearY)
    //   ly = [bottom, bottom, top, top]
    //
    // Level 3 — lerp across Z (scalar)
    //   result = lerp(ly[0], ly[2], sz)

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float LerpTree(Vector256<float> dots, float sx, float sy, float sz)
    {
        // Level 1 — lerp across X
        Vector128<float> nearX = dots.GetLower();
        Vector128<float> farX = dots.GetUpper();
        Vector128<float> lx = Fma.MultiplyAdd(
            Vector128.Create(sx), Sse.Subtract(farX, nearX), nearX);

        // Level 2 — lerp across Y
        // 0x88 = 0b10_00_10_00: [src[0], src[0], src[2], src[2]]
        // 0xDD = 0b11_01_11_01: [src[1], src[1], src[3], src[3]]
        Vector128<float> nearY = Sse.Shuffle(lx, lx, 0x88);
        Vector128<float> farY = Sse.Shuffle(lx, lx, 0xDD);
        Vector128<float> ly = Fma.MultiplyAdd(
            Vector128.Create(sy), Sse.Subtract(farY, nearY), nearY);

        // Level 3 — lerp across Z (scalar)
        return Lerp(ly.GetElement(0), ly.GetElement(2), sz);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Lerp(float a, float b, float t) => a + (t * (b - a));
}