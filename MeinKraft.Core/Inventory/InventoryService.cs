using MeinKraft;

/// <summary>
/// Provides client-side metadata and operations for inventory items, including
/// display names, grid sizes, stacking rules, and wear eligibility.
/// </summary>
public class InventoryService : IInventoryService
{
    // -------------------------------------------------------------------------
    // Fields
    // -------------------------------------------------------------------------

    /// <summary>Reference to the core game instance (language, platform, block types).</summary>
    private readonly IGame _game;
    private readonly IBlockRegistry _blockTypeRegistry;

    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    /// <summary>
    /// Initialises a new <see cref="InventoryService"/> bound to the given game.
    /// </summary>
    /// <param name="game">The active <see cref="Game"/> instance. Must not be <c>null</c>.</param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown if <paramref name="game"/> is <c>null</c>.
    /// </exception>
    public InventoryService(IGame game, IBlockRegistry blockTypeRegistry)
    {
        _game = game ?? throw new ArgumentNullException(nameof(game));
        _blockTypeRegistry = blockTypeRegistry;
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the localised display name for the given item.
    /// </summary>
    /// <param name="item">The item to describe. Must not be <c>null</c>.</param>
    /// <returns>A localised human-readable name string.</returns>
    /// <exception cref="NotSupportedException">
    ///     Thrown if the item's <see cref="InventoryItemType"/> is not yet handled.
    /// </exception>
    public string ItemInfo(InventoryItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.InventoryItemType == InventoryItemType.Block)
        {
            string key = string.Concat("Block_", _blockTypeRegistry.BlockTypes[item.BlockId].Name);
            return _game.Language.Get(key);
        }

        throw new NotSupportedException($"ItemInfo is not implemented for ItemClass '{item.InventoryItemType}'.");
    }

    /// <summary>
    /// Returns the width (in grid cells) of the given item's inventory footprint.
    /// </summary>
    /// <param name="item">The item to measure. Must not be <c>null</c>.</param>
    /// <returns>The item's column span (always ≥ 1).</returns>
    /// <exception cref="NotSupportedException">
    ///     Thrown if the item's <see cref="InventoryItemType"/> is not yet handled.
    /// </exception>
    public static int ItemSizeX(InventoryItem item)
    {
        return item == null
            ? throw new ArgumentNullException(nameof(item))
            : item.InventoryItemType switch
            {
                InventoryItemType.Block => 1,
                _ => throw new NotSupportedException($"ItemSizeX not implemented for ItemClass '{item.InventoryItemType}'.")
            };
    }

    /// <summary>
    /// Returns the height (in grid cells) of the given item's inventory footprint.
    /// </summary>
    /// <param name="item">The item to measure. Must not be <c>null</c>.</param>
    /// <returns>The item's row span (always ≥ 1).</returns>
    /// <exception cref="NotSupportedException">
    ///     Thrown if the item's <see cref="Packet_ItemClassEnum"/> is not yet handled.
    /// </exception>
    public static int ItemSizeY(InventoryItem item)
    {
        return item == null
            ? throw new ArgumentNullException(nameof(item))
            : item.InventoryItemType switch
            {
                InventoryItemType.Block => 1,
                _ => throw new NotSupportedException($"ItemSizeY not implemented for ItemClass '{item.InventoryItemType}'.")
            };
    }

    /// <summary>
    /// Determines whether a given item is eligible to be placed in the specified wear slot.
    /// </summary>
    /// <param name="wearPlace">The target <see cref="WearPlace"/> slot.</param>
    /// <param name="item">
    ///     The item to test. Pass <c>null</c> to always get <c>true</c>
    ///     (an empty item can always "be placed" in any slot — it clears it).
    /// </param>
    /// <returns>
    ///     <c>true</c> if the item can be equipped in <paramref name="wearPlace"/>;
    ///     <c>false</c> otherwise.
    /// </returns>
    public static bool CanWear(WearPlace wearPlace, InventoryItem? item)
    {
        if (item == null)
        {
            return true;
        }

        return wearPlace switch
        {
            WearPlace.RightHand => item.InventoryItemType == InventoryItemType.Block,
            WearPlace.MainArmor => false,
            WearPlace.Boots => false,
            WearPlace.Helmet => false,
            WearPlace.Gauntlet => false,
            _ => false,
        };
    }

    /// <summary>
    /// Returns the texture/sprite path used to render the given item in the inventory UI.
    /// </summary>
    /// <param name="item">The item whose graphic is requested.</param>
    /// <returns>
    ///     A string path to the item's graphic asset, or <c>null</c> if no custom
    ///     graphic is defined (the caller should fall back to a default block texture).
    /// </returns>
    /// <remarks>
    ///     TODO: Implement per-item-class graphic resolution.
    ///     Currently returns <c>null</c> for all items, causing callers to use the default.
    /// </remarks>
    public static string? ItemGraphics(InventoryItem item) => null;

    /// <summary>
    /// Returns the array of texture IDs used to render inventory UI elements.
    /// </summary>
    /// <returns>An integer array of texture atlas IDs owned by the game instance.</returns>
    public Dictionary<int, int> TextureIdForInventory() => _game.TextureIdForInventory;
}
