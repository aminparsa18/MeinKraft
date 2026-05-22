using MeinKraft;

/// <summary>
/// Provides utility methods for querying and manipulating the client-side inventory grid.
/// This class does not own inventory state — it reads from an injected
/// <see cref="Packet_Inventory"/> and <see cref="InventoryService"/>.
/// </summary>
public class InventoryUtilClient
{
    // -------------------------------------------------------------------------
    // Constants
    // -------------------------------------------------------------------------

    /// <summary>
    /// The number of material/hand slots available in the right-hand array.
    /// </summary>
    private const int HandSlotCount = 10;

    // -------------------------------------------------------------------------
    // Fields
    // -------------------------------------------------------------------------

    /// <summary>The live inventory packet received from the server.</summary>
    private Packet_Inventory _inventory;

    /// <summary>Item metadata and sizing rules.</summary>
    private readonly InventoryService _items;

    /// <summary>
    /// A flat lookup table mapping every grid cell (<see cref="Point"/>) to the
    /// top-left origin of the item occupying it, rebuilt whenever the inventory changes.
    /// </summary>
    private readonly Dictionary<Point, Point> _cellToItemOrigin;

    // -------------------------------------------------------------------------
    // Properties
    // -------------------------------------------------------------------------

    /// <summary>Gets the total number of columns in the inventory grid.</summary>
    public int CellCountX { get; }

    /// <summary>Gets the total number of rows in the inventory grid.</summary>
    public int CellCountY { get; }

    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    /// <summary>
    /// Initialises a new <see cref="InventoryUtilClient"/> with the given inventory
    /// packet and item database.
    /// </summary>
    /// <param name="inventory">
    ///     The inventory packet containing the player's current items and wear slots.
    ///     Must not be <c>null</c>.
    /// </param>
    /// <param name="items">
    ///     The client-side item database used to look up item sizes and metadata.
    ///     Must not be <c>null</c>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown if <paramref name="inventory"/> or <paramref name="items"/> is <c>null</c>.
    /// </exception>
    public InventoryUtilClient(Packet_Inventory inventory, InventoryService items)
    {
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _items = items ?? throw new ArgumentNullException(nameof(items));

        CellCountX = 12;
        CellCountY = 7 * 6;

        _cellToItemOrigin = new Dictionary<Point, Point>();
        RebuildCellMap();
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the distinct set of item origins whose footprint overlaps the
    /// rectangular area defined by (<paramref name="pX"/>, <paramref name="pY"/>)
    /// with dimensions (<paramref name="sizeX"/> × <paramref name="sizeY"/>).
    /// </summary>
    /// <param name="pX">Left column of the query rectangle (0-based).</param>
    /// <param name="pY">Top row of the query rectangle (0-based).</param>
    /// <param name="sizeX">Width of the query rectangle in cells.</param>
    /// <param name="sizeY">Height of the query rectangle in cells.</param>
    /// <returns>
    ///     A <see cref="HashSet{T}"/> of <see cref="Point"/> values, each representing
    ///     the top-left origin of a unique item found in the area.
    ///     Returns <c>null</c> if any cell in the area falls outside the grid bounds.
    /// </returns>
    public HashSet<Point>? ItemsAtArea(int pX, int pY, int sizeX, int sizeY)
    {
        HashSet<Point> found = new();

        for (int dx = 0; dx < sizeX; dx++)
        {
            for (int dy = 0; dy < sizeY; dy++)
            {
                Point cell = new(pX + dx, pY + dy);

                if (!IsValidCell(cell))
                {
                    return null; // Area partially out-of-bounds — abort.
                }

                if (_cellToItemOrigin.TryGetValue(cell, out Point origin))
                {
                    found.Add(origin); // HashSet handles deduplication automatically.
                }
            }
        }

        return found;
    }

    /// <summary>
    /// Determines whether the given grid cell falls within the valid inventory bounds.
    /// </summary>
    /// <param name="p">The cell position to validate.</param>
    /// <returns>
    ///     <c>true</c> if <paramref name="p"/> is within [0, <see cref="CellCountX"/>)
    ///     × [0, <see cref="CellCountY"/>); otherwise <c>false</c>.
    /// </returns>
    public bool IsValidCell(Point p)
    {
        return p.X >= 0 && p.Y >= 0
            && p.X < CellCountX
            && p.Y < CellCountY;
    }

    /// <summary>
    /// Updates the inventory packet and rebuilds the internal cell map.
    /// Call this whenever a new inventory sync is received from the server.
    /// </summary>
    /// <param name="inventory">The updated inventory packet. Must not be <c>null</c>.</param>
    public void UpdateInventory(Packet_Inventory inventory)
    {
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        RebuildCellMap();
    }

    /// <summary>
    /// Returns the item currently equipped in the specified wear slot, or <c>null</c>
    /// if the slot is empty or unrecognised.
    /// </summary>
    /// <param name="wearPlace">The target <see cref="WearPlace"/> slot.</param>
    /// <param name="activeMaterial">
    ///     The active material index, used to select the correct hand slot when
    ///     <paramref name="wearPlace"/> is <see cref="WearPlace.RightHand"/>.
    /// </param>
    /// <returns>The <see cref="InventoryItem"/> in the slot, or <c>null</c>.</returns>
    public InventoryItem? ItemAtWearPlace(WearPlace wearPlace, int activeMaterial)
    {
        return wearPlace switch
        {
            WearPlace.RightHand => _inventory.RightHand[activeMaterial],
            WearPlace.MainArmor => _inventory.MainArmor,
            WearPlace.Boots => _inventory.Boots,
            WearPlace.Helmet => _inventory.Helmet,
            WearPlace.Gauntlet => _inventory.Gauntlet,
            _ => null,
        };
    }

    /// <summary>
    /// Returns the top-left origin of the item occupying the given cell, or <c>null</c>
    /// if the cell is empty.
    /// </summary>
    /// <param name="cell">The cell to query.</param>
    /// <returns>
    ///     The <see cref="Point"/> origin of the occupying item, or <c>null</c>.
    /// </returns>
    public Point? ItemAtCell(Point cell)
    {
        return _cellToItemOrigin.TryGetValue(cell, out Point origin)
            ? origin
            : null;
    }

    /// <summary>
    /// Finds the first available right-hand slot, preferring the active material slot.
    /// </summary>
    /// <param name="activeMaterial">
    ///     The currently selected material index (checked first).
    /// </param>
    /// <returns>
    ///     The index of a free hand slot, or <c>null</c> if all
    ///     <see cref="HandSlotCount"/> slots are occupied.
    /// </returns>
    public int? FreeHand(int activeMaterial)
    {
        if (_inventory.RightHand[activeMaterial] == null)
        {
            return activeMaterial;
        }

        for (int i = 0; i < HandSlotCount; i++)
        {
            if (_inventory.RightHand[i] == null)
            {
                return i;
            }
        }

        return null;
    }

    /// <summary>
    /// Rebuilds the internal cell→origin lookup table from the current inventory state.
    /// Call this whenever the inventory packet is updated (e.g., after a server sync).
    /// </summary>
    public void RebuildCellMap()
    {
        _cellToItemOrigin.Clear();

        for (int i = 0; i < _inventory.Items.Length; i++)
        {
            Packet_PositionItem posItem = _inventory.Items[i];
            InventoryItem item = posItem.Value_;
            Point origin = new(posItem.X, posItem.Y);

            int w = InventoryService.ItemSizeX(item);
            int h = InventoryService.ItemSizeY(item);

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    Point cell = new(posItem.X + x, posItem.Y + y);
                    // Last write wins — handles overlapping data gracefully.
                    _cellToItemOrigin[cell] = origin;
                }
            }
        }
    }
}
