using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace LibNoise;

/// <summary>
/// Optimised gradient noise basis.
///
/// Changes vs. previous version
/// ─────────────────────────────
/// 1. HASH — 64-bit long arithmetic replaced with 32-bit unchecked int.
///    The six constants (1619, 31337, 6971, 1013) fit in 32 bits; only the
///    low 8 bits of the final result are ever consumed, so wrapping is harmless.
///    Eliminates 64-bit multiplies and halves the hash register footprint.
///
/// 2. GRADIENT TABLE — original XYZW-interleaved layout decomposed into three
///    flat SoA arrays (s_gx / s_gy / s_gz), pre-scaled by 2.12f.
///    Flat layout removes the stride-4 multiply on every lookup and enables
///    AVX2 GatherVector256 to load all 8 X-, Y-, and Z-components in parallel.
///    Pre-scaling removes the final vmulps from the dot-product chain.
///
/// 3. AVX2 + FMA HOT PATH — all 8 unit-cube corners evaluated simultaneously:
///      Stage 1  8 × int32 hashes via SIMD integer add + shift + xor
///      Stage 2  3 × GatherVector256 loads gradient X, Y, Z components
///      Stage 3  FMA chain  gz·dz → FMA(gy,dy,·) → FMA(gx,dx,·)
///               = 8 scaled dot products in one 256-bit register, 3 instructions
///      Stage 4  Trilinear lerp reduction:
///               level 1  GetLower / GetUpper  (zero cost — register alias)
///                        + 128-bit FMA across the x boundary
///               level 2  Sse.Shuffle + 128-bit FMA across y
///               level 3  two GetElement + scalar lerp across z
///
/// 4. SCALAR FALLBACK — identical 32-bit hash and SoA lookup, retained for
///    non-AVX2 hardware.
///
/// Corner layout used by the AVX2 path
/// ─────────────────────────────────────
///   Lower 128 bits  x = x0 group:
///     [0] (x0,y0,z0)  [1] (x0,y1,z0)  [2] (x0,y0,z1)  [3] (x0,y1,z1)
///   Upper 128 bits  x = x1 group:
///     [4] (x1,y0,z0)  [5] (x1,y1,z0)  [6] (x1,y0,z1)  [7] (x1,y1,z1)
///
/// GetLower / GetUpper map directly onto this split, so the first lerp level
/// (across x) costs no instructions — the JIT models both as aliases of the
/// same ymm register.
/// </summary>
public sealed class GradientNoiseBasis
{
    // ── Gradient source table (XYZW interleaved, used only during static init) ──

    private static ReadOnlySpan<float> RvData
    => [
        -0.763874f, -0.596439f, -0.246489f, 0f,  0.396055f,  0.904518f, -0.158073f, 0f,
        -0.499004f, -0.866500f, -0.013163f, 0f,  0.468724f, -0.824756f,  0.316346f, 0f,
         0.829598f,  0.431950f,  0.353816f, 0f, -0.454473f,  0.629497f, -0.630228f, 0f,
        -0.162349f, -0.869962f, -0.465628f, 0f,  0.932805f,  0.253451f,  0.256198f, 0f,
        -0.345419f,  0.927299f, -0.144227f, 0f, -0.715026f, -0.293698f, -0.634413f, 0f,
        -0.245997f,  0.717467f, -0.651711f, 0f, -0.967409f, -0.250435f, -0.037451f, 0f,
         0.901729f,  0.397108f, -0.170852f, 0f,  0.892657f, -0.072062f, -0.444938f, 0f,
         0.026008f, -0.036170f,  0.999007f, 0f,  0.949107f, -0.194860f,  0.247439f, 0f,
         0.471803f, -0.807064f, -0.355036f, 0f,  0.879737f,  0.141845f,  0.453809f, 0f,
         0.570747f,  0.696415f,  0.435033f, 0f, -0.141751f, -0.988233f, -0.057458f, 0f,
        -0.582190f, -0.030301f,  0.812488f, 0f, -0.609220f,  0.239482f, -0.755975f, 0f,
         0.299394f, -0.197066f, -0.933557f, 0f, -0.851615f, -0.220702f, -0.475440f, 0f,
         0.848886f,  0.341829f, -0.403169f, 0f, -0.156129f, -0.687241f,  0.709453f, 0f,
        -0.665651f,  0.626724f,  0.405124f, 0f,  0.595914f, -0.674582f,  0.435690f, 0f,
         0.171025f, -0.509292f,  0.843428f, 0f,  0.786050f,  0.536414f, -0.307222f, 0f,
         0.189050f, -0.791613f,  0.581042f, 0f, -0.294916f,  0.844994f,  0.446105f, 0f,
         0.342031f, -0.587360f, -0.733500f, 0f,  0.571550f,  0.786900f,  0.232635f, 0f,
         0.885026f, -0.408223f,  0.223791f, 0f, -0.789518f,  0.571645f,  0.223347f, 0f,
         0.774571f,  0.315660f,  0.548087f, 0f, -0.796950f, -0.043360f, -0.602487f, 0f,
        -0.142425f, -0.473249f, -0.869339f, 0f, -0.069884f,  0.170442f,  0.982886f, 0f,
         0.687815f, -0.484748f,  0.540306f, 0f,  0.543703f, -0.534446f, -0.647112f, 0f,
         0.971860f,  0.184391f, -0.146588f, 0f,  0.707084f,  0.485713f, -0.513921f, 0f,
         0.942302f,  0.331945f,  0.043348f, 0f,  0.499084f,  0.599922f,  0.625307f, 0f,
        -0.289203f,  0.211107f,  0.933700f, 0f,  0.412433f, -0.716670f, -0.562390f, 0f,
         0.877210f, -0.082816f,  0.472910f, 0f, -0.420685f, -0.214278f,  0.881538f, 0f,
         0.752558f, -0.039158f,  0.657361f, 0f,  0.076573f, -0.996789f,  0.023408f, 0f,
        -0.544312f, -0.309435f, -0.779727f, 0f, -0.455358f, -0.415572f,  0.787368f, 0f,
        -0.874586f,  0.483746f,  0.033013f, 0f,  0.245172f, -0.083862f,  0.965846f, 0f,
         0.382293f, -0.432813f,  0.816410f, 0f, -0.287735f, -0.905514f,  0.311853f, 0f,
        -0.667704f,  0.704955f, -0.239186f, 0f,  0.717885f, -0.464002f, -0.518983f, 0f,
         0.976342f, -0.214895f,  0.024005f, 0f, -0.073310f, -0.921136f,  0.382276f, 0f,
        -0.986284f,  0.151224f, -0.066138f, 0f, -0.899319f, -0.429671f,  0.081291f, 0f,
         0.652102f, -0.724625f,  0.222893f, 0f,  0.203761f,  0.458023f, -0.865272f, 0f,
        -0.030396f,  0.698724f, -0.714745f, 0f, -0.460232f,  0.839138f,  0.289887f, 0f,
        -0.089860f,  0.837894f,  0.538386f, 0f, -0.731595f,  0.079378f,  0.677102f, 0f,
        -0.447236f, -0.788397f,  0.422386f, 0f,  0.186481f,  0.645855f, -0.740335f, 0f,
        -0.259006f,  0.935463f,  0.240467f, 0f,  0.445839f,  0.819655f, -0.359712f, 0f,
         0.349962f,  0.755022f, -0.554499f, 0f, -0.997078f, -0.035958f,  0.067398f, 0f,
        -0.431163f, -0.147516f, -0.890133f, 0f,  0.299648f, -0.639140f,  0.708316f, 0f,
         0.397043f,  0.566526f, -0.722084f, 0f, -0.502489f,  0.438308f, -0.745246f, 0f,
         0.068724f,  0.354097f,  0.932680f, 0f, -0.047665f, -0.462597f,  0.885286f, 0f,
        -0.221934f,  0.900739f, -0.373383f, 0f, -0.956107f, -0.225676f,  0.186893f, 0f,
        -0.187627f,  0.391487f, -0.900852f, 0f, -0.224209f, -0.315405f,  0.922090f, 0f,
        -0.730807f, -0.537068f,  0.421283f, 0f, -0.035314f, -0.816748f,  0.575913f, 0f,
        -0.941391f,  0.176991f, -0.287153f, 0f, -0.154174f,  0.390458f,  0.907620f, 0f,
        -0.283847f,  0.533842f,  0.796519f, 0f, -0.482737f, -0.850448f,  0.209052f, 0f,
        -0.649175f,  0.477748f,  0.591886f, 0f,  0.885373f, -0.405387f, -0.227543f, 0f,
        -0.147261f,  0.181623f, -0.972279f, 0f,  0.095924f, -0.115847f, -0.988624f, 0f,
        -0.897240f, -0.191348f,  0.397928f, 0f,  0.903553f, -0.428461f, -0.003505f, 0f,
         0.849072f, -0.295807f, -0.437693f, 0f,  0.655510f,  0.741754f, -0.141804f, 0f,
         0.615980f, -0.178669f,  0.767232f, 0f,  0.011297f,  0.932256f, -0.361623f, 0f,
        -0.793031f,  0.258012f,  0.551845f, 0f,  0.421933f,  0.454311f,  0.784585f, 0f,
        -0.319993f,  0.040162f, -0.946568f, 0f, -0.815710f,  0.551307f, -0.175151f, 0f,
        -0.377644f,  0.003223f,  0.925945f, 0f,  0.129759f, -0.666581f, -0.734052f, 0f,
         0.601901f, -0.654237f, -0.457919f, 0f, -0.927463f, -0.034358f, -0.372334f, 0f,
        -0.438663f, -0.868301f, -0.231578f, 0f, -0.648845f, -0.749138f, -0.133387f, 0f,
         0.507393f, -0.588294f,  0.629653f, 0f,  0.726958f,  0.623665f,  0.287358f, 0f,
         0.411159f,  0.367614f, -0.834151f, 0f,  0.806333f,  0.585117f, -0.086402f, 0f,
         0.263935f, -0.880876f,  0.392932f, 0f,  0.421546f, -0.201336f,  0.884174f, 0f,
        -0.683198f, -0.569557f, -0.456996f, 0f, -0.117116f, -0.040665f, -0.992285f, 0f,
        -0.643679f, -0.109196f, -0.757465f, 0f, -0.561559f, -0.629890f,  0.536554f, 0f,
         0.062842f,  0.104677f, -0.992519f, 0f,  0.480759f, -0.286700f, -0.828658f, 0f,
        -0.228559f, -0.228965f, -0.946222f, 0f, -0.101940f, -0.657060f, -0.746914f, 0f,
         0.068919f, -0.678236f,  0.731605f, 0f,  0.401019f, -0.754026f,  0.520220f, 0f,
        -0.742141f,  0.547083f, -0.387203f, 0f, -0.002106f, -0.796417f, -0.604745f, 0f,
         0.296725f, -0.409909f, -0.862513f, 0f, -0.260932f, -0.798201f,  0.542945f, 0f,
        -0.641628f,  0.742379f,  0.192838f, 0f, -0.186009f, -0.101514f,  0.977290f, 0f,
         0.106711f, -0.962067f,  0.251079f, 0f, -0.743499f,  0.309880f, -0.592607f, 0f,
        -0.795853f, -0.605066f, -0.022661f, 0f, -0.828661f, -0.419471f, -0.370628f, 0f,
         0.084722f, -0.489815f, -0.867700f, 0f, -0.381405f,  0.788019f, -0.483276f, 0f,
         0.282042f, -0.953394f,  0.107205f, 0f,  0.530774f,  0.847413f,  0.013070f, 0f,
         0.051540f,  0.922524f,  0.382484f, 0f, -0.631467f, -0.709046f,  0.313852f, 0f,
         0.688248f,  0.517273f,  0.508668f, 0f,  0.646689f, -0.333782f, -0.685845f, 0f,
        -0.932528f, -0.247532f, -0.262906f, 0f,  0.630609f,  0.687570f, -0.359973f, 0f,
         0.577805f, -0.394189f,  0.714673f, 0f, -0.887833f, -0.437301f, -0.143250f, 0f,
         0.690982f,  0.174003f,  0.701617f, 0f, -0.866701f,  0.011818f,  0.498689f, 0f,
        -0.482876f,  0.727143f,  0.487949f, 0f, -0.577567f,  0.682593f, -0.447752f, 0f,
         0.373768f,  0.098299f,  0.922299f, 0f,  0.170744f,  0.964243f, -0.202687f, 0f,
         0.993654f, -0.035791f, -0.106632f, 0f,  0.587065f,  0.414300f, -0.695493f, 0f,
        -0.396509f,  0.265090f, -0.878924f, 0f, -0.086685f,  0.835530f, -0.542563f, 0f,
         0.923193f,  0.133398f, -0.360443f, 0f,  0.003791f, -0.258618f,  0.965972f, 0f,
         0.239144f,  0.245154f, -0.939526f, 0f,  0.758731f, -0.555871f,  0.339610f, 0f,
         0.295355f,  0.309513f,  0.903862f, 0f,  0.053122f, -0.910030f, -0.411124f, 0f,
         0.270452f,  0.022944f, -0.962460f, 0f,  0.563634f,  0.032435f,  0.825387f, 0f,
         0.156326f,  0.147392f,  0.976646f, 0f, -0.041014f,  0.981824f,  0.185309f, 0f,
        -0.385562f, -0.576343f, -0.720535f, 0f,  0.388281f,  0.904441f,  0.176702f, 0f,
         0.945561f, -0.192859f, -0.262146f, 0f,  0.844504f,  0.520193f,  0.127325f, 0f,
         0.033089f,  0.999121f, -0.025751f, 0f, -0.592616f, -0.482475f, -0.644999f, 0f,
         0.539471f,  0.631024f, -0.557476f, 0f,  0.655851f, -0.027319f, -0.754396f, 0f,
         0.274465f,  0.887659f,  0.369772f, 0f, -0.123419f,  0.975177f, -0.183842f, 0f,
        -0.223429f,  0.708045f,  0.669890f, 0f, -0.908654f,  0.196302f,  0.368528f, 0f,
        -0.957590f, -0.008637f,  0.288005f, 0f,  0.960535f,  0.030592f,  0.276472f, 0f,
        -0.413146f,  0.907537f,  0.075416f, 0f, -0.847992f,  0.350849f, -0.397259f, 0f,
         0.614736f,  0.395841f,  0.682210f, 0f, -0.503504f, -0.666128f, -0.550234f, 0f,
        -0.268833f, -0.738524f, -0.618314f, 0f,  0.792737f, -0.600010f, -0.107502f, 0f,
        -0.637582f,  0.508144f, -0.579032f, 0f,  0.750105f,  0.282165f, -0.598101f, 0f,
        -0.351199f, -0.392294f, -0.850155f, 0f,  0.250126f, -0.960993f, -0.118025f, 0f,
        -0.732341f,  0.680909f, -0.006327f, 0f, -0.760674f, -0.141009f,  0.633634f, 0f,
         0.222823f, -0.304012f,  0.926243f, 0f,  0.209178f,  0.505671f,  0.836984f, 0f,
         0.757914f, -0.566290f, -0.323857f, 0f, -0.782926f, -0.339196f,  0.521510f, 0f,
        -0.462952f,  0.585565f,  0.665424f, 0f,  0.618790f,  0.194119f, -0.761194f, 0f,
         0.741388f, -0.276743f,  0.611357f, 0f,  0.707571f,  0.702621f,  0.075287f, 0f,
         0.156562f,  0.819977f,  0.550569f, 0f, -0.793606f,  0.440216f,  0.420000f, 0f,
         0.234547f,  0.885309f, -0.401517f, 0f,  0.132598f,  0.801150f, -0.583590f, 0f,
        -0.377899f, -0.639179f,  0.669808f, 0f, -0.865993f, -0.396465f,  0.304748f, 0f,
        -0.624815f, -0.442830f,  0.643046f, 0f, -0.485705f,  0.825614f, -0.287146f, 0f,
        -0.971788f,  0.175535f,  0.157529f, 0f, -0.456027f,  0.392629f,  0.798675f, 0f,
        -0.010444f,  0.521623f, -0.853112f, 0f, -0.660575f, -0.745190f,  0.091282f, 0f,
        -0.015770f, -0.307475f, -0.951425f, 0f, -0.603467f, -0.250192f,  0.757121f, 0f,
         0.506876f,  0.250060f,  0.824952f, 0f,  0.255404f,  0.966794f,  0.008845f, 0f,
         0.466764f, -0.874228f, -0.133625f, 0f,  0.475077f, -0.068235f, -0.877295f, 0f,
        -0.224967f, -0.938972f, -0.260233f, 0f, -0.377929f, -0.814757f, -0.439705f, 0f,
        -0.305847f,  0.542333f, -0.782517f, 0f,  0.266580f, -0.902905f, -0.337191f, 0f,
         0.027577f,  0.322158f, -0.946284f, 0f,  0.018542f,  0.716349f,  0.697496f, 0f,
        -0.204830f,  0.978416f,  0.027337f, 0f, -0.898276f,  0.373969f,  0.230752f, 0f,
        -0.009094f,  0.546594f,  0.837349f, 0f,  0.660200f, -0.751089f,  0.000959f, 0f,
         0.855301f, -0.303056f,  0.420259f, 0f,  0.797138f,  0.062301f, -0.600574f, 0f,
         0.489470f, -0.866813f,  0.095151f, 0f,  0.251142f,  0.674531f,  0.694216f, 0f,
        -0.578422f, -0.737373f, -0.348867f, 0f, -0.254689f, -0.514807f,  0.818601f, 0f,
         0.374972f,  0.761612f,  0.528529f, 0f,  0.640303f, -0.734271f, -0.225517f, 0f,
        -0.638076f,  0.285527f,  0.715075f, 0f,  0.772956f, -0.159840f, -0.613995f, 0f,
         0.798217f, -0.590628f,  0.118356f, 0f, -0.986276f, -0.057834f, -0.154644f, 0f,
        -0.312988f, -0.945490f,  0.089927f, 0f, -0.497338f,  0.178325f,  0.849032f, 0f,
        -0.101136f, -0.981014f,  0.165477f, 0f, -0.521688f,  0.055343f, -0.851339f, 0f,
        -0.786182f, -0.583814f,  0.202678f, 0f, -0.565191f,  0.821858f, -0.071466f, 0f,
         0.437895f,  0.152598f, -0.885981f, 0f, -0.923940f,  0.353436f, -0.146350f, 0f,
         0.212189f, -0.815162f, -0.538969f, 0f, -0.859262f,  0.143405f, -0.491024f, 0f,
         0.991353f,  0.112814f,  0.067027f, 0f,  0.033788f, -0.979891f, -0.196654f, 0f,
    ];

    // ── SoA gradient arrays (pre-scaled by 2.12f, built once at class load) ──
    //
    // Pre-scaling absorbs the constant factor from the dot product, so the
    // return value of Contrib and the dots register in EvaluateAvx2 require
    // no final multiply.
    //
    // Flat index (no stride) matches GatherVector256 with scale=4 directly:
    //   result[i] = *(base + indices[i] * sizeof(float))

    private static readonly float[] s_gx = new float[256];
    private static readonly float[] s_gy = new float[256];
    private static readonly float[] s_gz = new float[256];

    // Checked once at class load; never tested again in the hot path.
    private static readonly bool s_useAvx2 = Avx2.IsSupported && Fma.IsSupported;

    static GradientNoiseBasis()
    {
        const float Scale = 2.12f;
        ReadOnlySpan<float> src = RvData;
        for (int i = 0; i < 256; i++)
        {
            int b = i << 2;             // stride-4 source index
            s_gx[i] = src[b] * Scale;
            s_gy[i] = src[b + 1] * Scale;
            s_gz[i] = src[b + 2] * Scale;
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a coherent gradient noise value in approximately [-1, 1] at the
    /// given world position. Dispatches to the AVX2 path when available.
    /// </summary>
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static float GradientCoherentNoise(float x, float y, float z,
        int seed, NoiseQuality noiseQuality)
    {
        // Integer unit-cube corners.
        int x0 = x > 0f ? (int)x : (int)x - 1;
        int y0 = y > 0f ? (int)y : (int)y - 1;
        int z0 = z > 0f ? (int)z : (int)z - 1;
        int x1 = x0 + 1, y1 = y0 + 1, z1 = z0 + 1;

        // Fractional position inside the unit cube.
        float fx = x - x0, fy = y - y0, fz = z - z0;

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

        // 32-bit hash contributions per axis.
        // Unchecked wrapping preserves the low 8 bits identically to the
        // previous 64-bit version — only those 8 bits are ever consumed.
        unchecked
        {
            int hx0 = 1619 * x0, hx1 = 1619 * x1;
            int hy0 = 31337 * y0, hy1 = 31337 * y1;
            int hz0 = 6971 * z0, hz1 = 6971 * z1;
            int hs = 1013 * seed;

            return s_useAvx2
                ? EvaluateAvx2(fx, fy, fz, sx, sy, sz, hx0, hx1, hy0, hy1, hz0, hz1, hs)
                : EvaluateScalar(fx, fy, fz, sx, sy, sz, hx0, hx1, hy0, hy1, hz0, hz1, hs);
        }
    }

    // ── AVX2 + FMA path ───────────────────────────────────────────────────────
    //
    // Stage 1 — 8 × int32 hashes
    //   combined[i] = hx[xi] + hy[yi] + hz[zi] + hs
    //   h[i]        = combined[i] ^ (combined[i] >> 8)  (arithmetic shift)
    //   index[i]    = h[i] & 0xFF
    //
    //   Column vectors reproduce the corner layout (x varies slowest):
    //     hx: [hx0 hx0 hx0 hx0 | hx1 hx1 hx1 hx1]
    //     hy: [hy0 hy1 hy0 hy1 | hy0 hy1 hy0 hy1]
    //     hz: [hz0 hz0 hz1 hz1 | hz0 hz0 hz1 hz1]
    //
    // Stage 2 — 3 × GatherVector256
    //   Loads s_gx[index[i]], s_gy[index[i]], s_gz[index[i]] for all 8 corners.
    //   scale=4 matches sizeof(float) so indices are used directly as subscripts.
    //
    // Stage 3 — dot products (3 instructions per 8 corners)
    //   t0   = gz  * dz                 (vmulps)
    //   t1   = gy  * dy + t0            (vfmadd213ps)
    //   dots = gx  * dx + t1            (vfmadd213ps)
    //   No final scale multiply — 2.12f is already baked into s_gx/s_gy/s_gz.
    //
    // Stage 4 — trilinear lerp (see LerpTree)

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe float EvaluateAvx2(
        float fx, float fy, float fz,
        float sx, float sy, float sz,
        int hx0, int hx1, int hy0, int hy1, int hz0, int hz1, int hs)
    {
        // ── Stage 1: 8 × int32 hashes ────────────────────────────────────────
        Vector256<int> vhx = Vector256.Create(hx0, hx0, hx0, hx0, hx1, hx1, hx1, hx1);
        Vector256<int> vhy = Vector256.Create(hy0, hy1, hy0, hy1, hy0, hy1, hy0, hy1);
        Vector256<int> vhz = Vector256.Create(hz0, hz0, hz1, hz1, hz0, hz0, hz1, hz1);
        Vector256<int> vhs = Vector256.Create(hs);

        Vector256<int> combined = Avx2.Add(Avx2.Add(Avx2.Add(vhx, vhy), vhz), vhs);
        Vector256<int> hashed = Avx2.Xor(combined, Avx2.ShiftRightArithmetic(combined, 8));
        Vector256<int> indices = Avx2.And(hashed, Vector256.Create(0xFF));

        // ── Stage 2: gather gradient components ───────────────────────────────
        fixed (float* pgx = s_gx, pgy = s_gy, pgz = s_gz)
        {
            Vector256<float> gx = Avx2.GatherVector256(pgx, indices, 4);
            Vector256<float> gy = Avx2.GatherVector256(pgy, indices, 4);
            Vector256<float> gz = Avx2.GatherVector256(pgz, indices, 4);

            // ── Stage 3: fractional offsets + dot products ────────────────────
            float fx1 = fx - 1f, fy1 = fy - 1f, fz1 = fz - 1f;
            Vector256<float> vdx = Vector256.Create(fx, fx, fx, fx, fx1, fx1, fx1, fx1);
            Vector256<float> vdy = Vector256.Create(fy, fy1, fy, fy1, fy, fy1, fy, fy1);
            Vector256<float> vdz = Vector256.Create(fz, fz, fz1, fz1, fz, fz, fz1, fz1);

            Vector256<float> dots = Fma.MultiplyAdd(gx, vdx,
                       Fma.MultiplyAdd(gy, vdy,
                       Avx.Multiply(gz, vdz)));

            // ── Stage 4: trilinear lerp ───────────────────────────────────────
            return LerpTree(dots, sx, sy, sz);
        }
    }

    /// <summary>
    /// Reduces 8 corner dot-products to a scalar via trilinear lerp.
    ///
    /// Input layout  [d0..d7]:
    ///   lower 128:  [d0=(x0,y0,z0)  d1=(x0,y1,z0)  d2=(x0,y0,z1)  d3=(x0,y1,z1)]
    ///   upper 128:  [d4=(x1,y0,z0)  d5=(x1,y1,z0)  d6=(x1,y0,z1)  d7=(x1,y1,z1)]
    ///
    /// Level 1 — lerp across X
    ///   GetLower() / GetUpper() split at the 128-bit boundary (zero instructions).
    ///   FMA:  nearX + sx * (farX - nearX)
    ///   lx = [lx(y0,z0)  lx(y1,z0)  lx(y0,z1)  lx(y1,z1)]
    ///
    /// Level 2 — lerp across Y
    ///   Sse.Shuffle 0x88 duplicates elements 0 and 2: [lx[0], lx[0], lx[2], lx[2]]
    ///   Sse.Shuffle 0xDD duplicates elements 1 and 3: [lx[1], lx[1], lx[3], lx[3]]
    ///   FMA:  nearY + sy * (farY - nearY)
    ///   ly = [bottom, bottom, top, top]
    ///
    /// Level 3 — lerp across Z (scalar)
    ///   Extract ly[0] = bottom and ly[2] = top; one scalar Lerp.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float LerpTree(Vector256<float> dots, float sx, float sy, float sz)
    {
        // Level 1 — lerp across X
        Vector128<float> nearX = dots.GetLower();
        Vector128<float> farX = dots.GetUpper();
        Vector128<float> lx = Fma.MultiplyAdd(
            Vector128.Create(sx), Sse.Subtract(farX, nearX), nearX);

        // Level 2 — lerp across Y
        // Sse.Shuffle control bytes (within each 128-bit lane, same vector for both args):
        //   0x88 = 0b10_00_10_00 → output = [src[0], src[0], src[2], src[2]]
        //   0xDD = 0b11_01_11_01 → output = [src[1], src[1], src[3], src[3]]
        Vector128<float> nearY = Sse.Shuffle(lx, lx, 0x88);
        Vector128<float> farY = Sse.Shuffle(lx, lx, 0xDD);
        Vector128<float> ly = Fma.MultiplyAdd(
            Vector128.Create(sy), Sse.Subtract(farY, nearY), nearY);

        // Level 3 — lerp across Z (scalar)
        return Lerp(ly.GetElement(0), ly.GetElement(2), sz);
    }

    // ── Scalar fallback path ──────────────────────────────────────────────────
    //
    // Shares the 32-bit hash and SoA gradient lookup with the AVX2 path.
    // Inlined so the JIT can eliminate this entirely when it can prove
    // s_useAvx2 is true at the call site.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float EvaluateScalar(
        float fx, float fy, float fz,
        float sx, float sy, float sz,
        int hx0, int hx1, int hy0, int hy1, int hz0, int hz1, int hs)
    {
        float fx1 = fx - 1f, fy1 = fy - 1f, fz1 = fz - 1f;
        int bz0 = hz0 + hs, bz1 = hz1 + hs;

        ReadOnlySpan<float> gx = s_gx, gy = s_gy, gz = s_gz;

        float b00 = Contrib(gx, gy, gz, fx, fy, fz, hx0 + hy0 + bz0);
        float b10 = Contrib(gx, gy, gz, fx1, fy, fz, hx1 + hy0 + bz0);
        float b01 = Contrib(gx, gy, gz, fx, fy1, fz, hx0 + hy1 + bz0);
        float b11 = Contrib(gx, gy, gz, fx1, fy1, fz, hx1 + hy1 + bz0);
        float bottom = Lerp(Lerp(b00, b10, sx), Lerp(b01, b11, sx), sy);

        float t00 = Contrib(gx, gy, gz, fx, fy, fz1, hx0 + hy0 + bz1);
        float t10 = Contrib(gx, gy, gz, fx1, fy, fz1, hx1 + hy0 + bz1);
        float t01 = Contrib(gx, gy, gz, fx, fy1, fz1, hx0 + hy1 + bz1);
        float t11 = Contrib(gx, gy, gz, fx1, fy1, fz1, hx1 + hy1 + bz1);
        float top = Lerp(Lerp(t00, t10, sx), Lerp(t01, t11, sx), sy);

        return Lerp(bottom, top, sz);
    }

    /// <summary>
    /// Hash one corner and return its gradient dot product.
    /// No final multiply needed — 2.12f is pre-baked into the SoA arrays.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Contrib(
        ReadOnlySpan<float> gx, ReadOnlySpan<float> gy, ReadOnlySpan<float> gz,
        float dx, float dy, float dz, int hashBase)
    {
        int h = hashBase ^ (hashBase >> 8);
        int i = h & 0xFF;
        return (gx[i] * dx) + (gy[i] * dy) + (gz[i] * dz);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Lerp(float a, float b, float t) => a + (t * (b - a));
}