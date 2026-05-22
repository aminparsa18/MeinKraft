using OpenTK.Mathematics;

/// <summary>
/// Discovers the connected region of crafting-table blocks starting from a given
/// position (flood-fill), then reads which items are placed on top of them.
/// </summary>
public class CraftingTableTool
{
    public required IMapStorage d_Map;
    public required IBlockRegistry d_Data;

    // ── Pre-allocated buffers ─────────────────────────────────────────────────
    // GetTable and GetOnTable are called together once per player interaction,
    // never concurrently. Keeping the work buffers as instance fields eliminates
    // three ~24 KB temporary array allocations per crafting-table open.

    private const int MaxCraftingTableSize = 2000;
    private const int BufferCapacity = 2048; // power-of-two headroom above max

    /// <summary>Output buffer for discovered crafting-table positions (reused each call).</summary>
    private readonly Vector3i[] _tableBuffer = new Vector3i[BufferCapacity];

    /// <summary>DFS frontier (reused each call).</summary>
    private readonly Vector3i[] _todoBuffer = new Vector3i[BufferCapacity];

    /// <summary>Output buffer for on-table block types (reused each call).</summary>
    private readonly int[] _onTableBuffer = new int[BufferCapacity];

    /// <summary>
    /// O(1) visited-position set.
    /// Replaces <c>Vector3IntRefArrayContains</c> which was O(n) per check,
    /// producing O(n²) total work for a fully-connected table of n blocks.
    /// <see cref="Vector3i"/> implements <see cref="IEquatable{T}"/> in OpenTK,
    /// so it is safe to use directly as a <see cref="HashSet{T}"/> key.
    /// </summary>
    private readonly HashSet<Vector3i> _visited = [];

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads the block type placed directly above each position in
    /// <paramref name="table"/> (i.e. at Z + 1).
    /// </summary>
    /// <param name="table">Crafting-table positions returned by <see cref="GetTable"/>.</param>
    /// <param name="tableCount">Number of valid entries in <paramref name="table"/>.</param>
    /// <param name="retCount">Returns the number of valid entries in the returned array.</param>
    /// <returns>
    /// A pre-allocated int buffer whose first <paramref name="retCount"/> elements
    /// contain block types. Valid until the next call to <see cref="GetOnTable"/>.
    /// </returns>
    public int[] GetOnTable(Vector3i[] table, int tableCount, out int retCount)
    {
        int count = 0;
        for (int i = 0; i < tableCount; i++)
        {
            Vector3i v = table[i];
            _onTableBuffer[count++] = d_Map.GetBlock(v.X, v.Y, v.Z + 1);
        }

        retCount = count;
        return _onTableBuffer;
    }

    /// <summary>
    /// Flood-fills the connected region of crafting-table blocks reachable from
    /// (<paramref name="posx"/>, <paramref name="posy"/>, <paramref name="posz"/>)
    /// in the XY plane and returns the discovered positions.
    /// </summary>
    /// <remarks>
    /// Uses a DFS frontier (<c>_todoBuffer</c>) and an O(1) <see cref="HashSet{T}"/>
    /// for visited tracking. The previous implementation used a linear-scan array
    /// which was O(n²) in the size of the discovered region.
    /// </remarks>
    /// <param name="posx">Starting block X coordinate.</param>
    /// <param name="posy">Starting block Y coordinate.</param>
    /// <param name="posz">Starting block Z coordinate.</param>
    /// <param name="retCount">Returns the number of valid entries in the returned array.</param>
    /// <returns>
    /// A pre-allocated <see cref="Vector3i"/> buffer whose first <paramref name="retCount"/>
    /// elements contain the table positions. Valid until the next call to <see cref="GetTable"/>.
    /// </returns>
    public Vector3i[] GetTable(int posx, int posy, int posz, out int retCount)
    {
        int lCount = 0;
        int todoCount = 0;

        // ── Reset reusable state ──────────────────────────────────────────────
        _visited.Clear();
        _todoBuffer[todoCount++] = new Vector3i(posx, posy, posz);

        // ── Cache block ID — it's constant for the entire flood-fill ─────────
        // Previously called 4× per loop iteration; now fetched once.
        int craftingTableId = d_Data.BlockIdCraftingTable;

        while (todoCount > 0 && lCount < MaxCraftingTableSize)
        {
            // Pop from the top of the DFS stack.
            Vector3i p = _todoBuffer[--todoCount];

            // ── O(1) visited check via HashSet ────────────────────────────────
            // Previously Vector3IntRefArrayContains scanned _tableBuffer linearly
            // — O(n) per check, O(n²) total for a table of n blocks.
            if (!_visited.Add(p))
            {
                continue;
            }

            _tableBuffer[lCount++] = p;

            // Expand in all four horizontal directions.
            TryEnqueue(p.X + 1, p.Y, p.Z, craftingTableId, _todoBuffer, ref todoCount);
            TryEnqueue(p.X - 1, p.Y, p.Z, craftingTableId, _todoBuffer, ref todoCount);
            TryEnqueue(p.X, p.Y + 1, p.Z, craftingTableId, _todoBuffer, ref todoCount);
            TryEnqueue(p.X, p.Y - 1, p.Z, craftingTableId, _todoBuffer, ref todoCount);
        }

        retCount = lCount;
        return _tableBuffer;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Pushes (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>) onto
    /// <paramref name="frontier"/> if the block there is a crafting table and the
    /// frontier has space.
    /// </summary>
    private void TryEnqueue(int x, int y, int z, int craftingTableId,
                             Vector3i[] frontier, ref int count)
    {
        if (count < BufferCapacity
            && d_Map.GetBlock(x, y, z) == craftingTableId)
        {
            frontier[count++] = new Vector3i(x, y, z);
        }
    }
}