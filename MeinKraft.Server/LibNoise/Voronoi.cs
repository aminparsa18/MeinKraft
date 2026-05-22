namespace LibNoise;

/// <summary>
/// Voronoi (cellular/Worley) noise generator.
/// Divides space into irregular cells, each owned by the nearest randomly
/// jittered feature point, and returns a value based on that nearest point.
/// <para>
/// For each sample the algorithm searches a 5×5×5 neighbourhood of integer
/// grid cells. Each cell's centre is jittered by <see cref="ValueNoise"/> to
/// produce a pseudo-random feature point. The nearest feature point is found,
/// and the output is:
/// <list type="bullet">
///   <item>The noise value at the nearest feature point's grid cell (always).</item>
///   <item>Plus the Euclidean distance to that point scaled by
///         <see cref="Math.Sqrt3"/> (only when <see cref="DistanceEnabled"/>).</item>
/// </list>
/// Used in <c>DefaultWorldGenerator</c> for flower and decoration placement.
/// </para>
/// </summary>
public class Voronoi : ValueNoiseBasis, IModule
{
    // ── Parameters ────────────────────────────────────────────────────────────
    private static readonly float Sqrt3 = 1.7320508075688772f;

    /// <summary>
    /// Scales the input coordinates before cell lookup.
    /// Higher values produce smaller, more densely packed cells. Default is <c>1.0</c>.
    /// </summary>
    public float Frequency { get; set; }

    /// <summary>
    /// Scales the cell colour value added to the output.
    /// Higher values produce stronger contrast between adjacent cells. Default is <c>1.0</c>.
    /// </summary>
    public float Displacement { get; set; }

    /// <summary>
    /// When <see langword="true"/>, the Euclidean distance to the nearest feature
    /// point is added to the output, producing raised ridges at cell boundaries.
    /// Default is <see langword="false"/> (flat cell interiors).
    /// </summary>
    public bool DistanceEnabled { get; set; }

    /// <summary>Random seed used to jitter feature points. Default is <c>0</c>.</summary>
    public int Seed { get; set; }

    // ── IModule ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the Voronoi noise value at world position
    /// (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>).
    /// </summary>
    public float GetValue(float x, float y, float z)
    {
        // Cache all fields in locals for the hot 5×5×5 loop.
        int seed = Seed;
        float displacement = Displacement;
        bool distEnabled = DistanceEnabled;

        x *= Frequency;
        y *= Frequency;
        z *= Frequency;

        // Integer cell containing the sample point (floor, handles negatives).
        int cellX = x > 0f ? (int)x : (int)x - 1;
        int cellY = y > 0f ? (int)y : (int)y - 1;
        int cellZ = z > 0f ? (int)z : (int)z - 1;

        // Find the nearest jittered feature point in the 5×5×5 neighbourhood.
        // Track float coords for the distance contribution and the final
        // ValueNoise colour lookup (jitter can push a point outside its cell,
        // so we must floor the winning float coords rather than reuse ix/iy/iz).
        float minDistSq = float.MaxValue;
        float nearestX = 0f;
        float nearestY = 0f;
        float nearestZ = 0f;

        for (int iz = cellZ - 2; iz <= cellZ + 2; iz++)
        {
            for (int iy = cellY - 2; iy <= cellY + 2; iy++)
            {
                for (int ix = cellX - 2; ix <= cellX + 2; ix++)
                {
                    // Jitter each cell centre with three independent value-noise samples.
                    float fpX = ix + ValueNoise(ix, iy, iz, seed);
                    float fpY = iy + ValueNoise(ix, iy, iz, seed + 1);
                    float fpZ = iz + ValueNoise(ix, iy, iz, seed + 2);

                    float dx = fpX - x;
                    float dy = fpY - y;
                    float dz = fpZ - z;
                    float distSq = (dx * dx) + (dy * dy) + (dz * dz);

                    if (distSq < minDistSq)
                    {
                        minDistSq = distSq;
                        nearestX = fpX;
                        nearestY = fpY;
                        nearestZ = fpZ;
                    }
                }
            }
        }

        // Optionally add distance to the nearest feature point, normalised so
        // the maximum possible distance maps to approximately 1.
        float distanceContribution = 0f;
        if (distEnabled)
        {
            float dx = nearestX - x;
            float dy = nearestY - y;
            float dz = nearestZ - z;
            distanceContribution = (MathF.Sqrt((dx * dx) + (dy * dy) + (dz * dz)) * Sqrt3) - 1f;
        }

        // Cell colour: noise value at the nearest feature point's integer cell.
        // Jitter can shift a feature point outside its origin cell, so floor the
        // float coords rather than reusing ix/iy/iz from the search loop.
        int nx = nearestX > 0f ? (int)nearestX : (int)nearestX - 1;
        int ny = nearestY > 0f ? (int)nearestY : (int)nearestY - 1;
        int nz = nearestZ > 0f ? (int)nearestZ : (int)nearestZ - 1;

        return distanceContribution + (displacement * ValueNoise(nx, ny, nz));
    }
}