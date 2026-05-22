using OpenTK.Mathematics;

/// <summary>
/// Performs octree-based spatial searches over a 3D block world,
/// supporting line intersection tests against non-empty blocks.
///
/// All traversal is single-pass: the ray is tested inline at each node using
/// the slab method with a pre-computed <c>invDir</c>, children are visited in
/// ray-distance order for early exit, and results are written directly into a
/// pre-allocated hit buffer — no intermediate lists, no heap allocation per call.
/// </summary>
public class BlockOctreeSearcher
{
    /// <summary>
    /// The root bounding box of the octree search space.
    /// Must have equal power-of-two dimensions for subdivision to work correctly.
    /// </summary>
    public Box3 StartBox { get; set; }

    /// <summary>
    /// Maximum number of block hits returned per <see cref="LineIntersection"/> call.
    /// A pick ray in a voxel world realistically hits fewer than 30 blocks at any
    /// supported view distance; 256 is a safe upper bound.
    /// </summary>
    private const int MaxHits = 256;

    /// <summary>Pre-allocated hit buffer — reused across calls, zero heap allocation per query.</summary>
    private readonly BlockPosSide[] _hits = new BlockPosSide[MaxHits];

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Finds all non-empty blocks intersected by <paramref name="line"/>,
    /// returning their positions and exact collision points.
    /// </summary>
    /// <param name="isEmpty">Returns <c>true</c> if the block at (x, y, z) is empty.</param>
    /// <param name="getBlockHeight">Returns the height of the block at (x, y, z).</param>
    /// <param name="line">The line segment to test against the block world.</param>
    /// <param name="retCount">Number of hits written into the returned segment.</param>
    /// <returns>
    /// A segment of the internal hit buffer. Valid until the next call to
    /// <see cref="LineIntersection"/> on this instance.
    /// </returns>
    public ArraySegment<BlockPosSide> LineIntersection(
        IsBlockEmptyDelegate isEmpty,
        GetBlockHeightDelegate getBlockHeight,
        Line3D line,
        out int retCount)
    {
        retCount = 0;

        if (StartBox.Size.X == 0 && StartBox.Size.Y == 0 && StartBox.Size.Z == 0)
            return new ArraySegment<BlockPosSide>(_hits, 0, 0);

        Vector3 origin = line.Start;
        Vector3 dir = line.Direction;
        Vector3 invDir = Vector3.One / dir;

        // Test root once before entering traversal
        if (Intersection.SlabTest(StartBox.Min, StartBox.Max, origin, invDir, out float tEntry))
            Traverse(StartBox, origin, dir, invDir, tEntry, isEmpty, getBlockHeight, ref retCount);

        return new ArraySegment<BlockPosSide>(_hits, 0, retCount);
    }

    // ── Core traversal ────────────────────────────────────────────────────────

    /// <param name="tEntry">
    /// Ray entry distance for <paramref name="box"/>, already confirmed by the caller.
    /// Passed in to avoid re-testing a node that the parent already tested.
    /// </param>
    private void Traverse(
        Box3 box,
        Vector3 origin,
        Vector3 dir,
        Vector3 invDir,
        float tEntry,
        IsBlockEmptyDelegate isEmpty,
        GetBlockHeightDelegate getBlockHeight,
        ref int count)
    {
        // tEntry is already confirmed by the caller — no re-test here.

        if (box.Size.X <= 1f)
        {
            ProcessLeaf(box, origin, dir, invDir, isEmpty, getBlockHeight, ref count);
            return;
        }

        Span<Box3> children = stackalloc Box3[8];
        Subdivide(box, children);

        // Test each child once and collect only the ones the ray actually hits.
        // Missed children are never added to the sort buffer — no float.MaxValue
        // sentinels, no wasted sort comparisons.
        Span<(float t, int i)> order = stackalloc (float, int)[8];
        int hitCount = 0;

        for (int i = 0; i < 8; i++)
        {
            if (Intersection.SlabTest(children[i].Min, children[i].Max, origin, invDir, out float t))
                order[hitCount++] = (t, i);
        }

        // Insertion sort over hit children only — worst case 8, average much less.
        for (int i = 1; i < hitCount; i++)
        {
            (float t, int idx) key = order[i];
            int j = i - 1;
            while (j >= 0 && order[j].t > key.t) { order[j + 1] = order[j]; j--; }
            order[j + 1] = key;
        }

        for (int i = 0; i < hitCount; i++)
        {
            if (count >= MaxHits) return;
            (float t, int idx) = order[i];
            // Pass t down — child skips its own slab test at the top of Traverse.
            Traverse(children[idx], origin, dir, invDir, t, isEmpty, getBlockHeight, ref count);
        }
    }

    /// <summary>
    /// Tests a unit-sized leaf node against the actual block data.
    /// Skips empty blocks and applies the per-block height adjustment before
    /// computing the precise hit point.
    /// </summary>
    private void ProcessLeaf(
        Box3 box,
        Vector3 origin,
        Vector3 dir,
        Vector3 invDir,
        IsBlockEmptyDelegate isEmpty,
        GetBlockHeightDelegate getBlockHeight,
        ref int count)
    {
        int bx = (int)box.Min.X;
        int by = (int)box.Min.Z; // Y/Z swapped — world space convention
        int bz = (int)box.Min.Y;

        if (isEmpty(bx, by, bz)) return;

        // Adjust the box height for non-full blocks (slabs, sloped rails, etc.)
        Box3 adjustedBox = new(
            box.Min,
            new Vector3(box.Max.X, box.Min.Y + getBlockHeight(bx, by, bz), box.Max.Z));

        if (!Intersection.HitBoundingBoxSlabInvDir(
                adjustedBox.Min, adjustedBox.Max, origin, dir, invDir, out Vector3 hit))
            return;

        if (count < MaxHits)
            _hits[count++] = new BlockPosSide
            {
                BlockPos = new Vector3(bx, bz, by), // Y/Z swapped — world space convention
                CollisionPos = hit,
            };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Fills <paramref name="children"/> with the 8 equal sub-boxes produced
    /// by halving <paramref name="box"/> along each axis.
    /// All arithmetic is on the stack; no heap allocation.
    /// </summary>
    private static void Subdivide(Box3 box, Span<Box3> children)
    {
        float x = box.Min.X;
        float y = box.Min.Y;
        float z = box.Min.Z;
        float half = box.Size.X * 0.5f;

        children[0] = Child(x, y, z, half);
        children[1] = Child(x + half, y, z, half);
        children[2] = Child(x, y, z + half, half);
        children[3] = Child(x + half, y, z + half, half);
        children[4] = Child(x, y + half, z, half);
        children[5] = Child(x + half, y + half, z, half);
        children[6] = Child(x, y + half, z + half, half);
        children[7] = Child(x + half, y + half, z + half, half);
    }

    private static Box3 Child(float x, float y, float z, float half)
        => new(new Vector3(x, y, z), new Vector3(x + half, y + half, z + half));
}