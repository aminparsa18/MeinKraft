
/// <summary>
/// Defines the contract for handling player inventory interactions.
/// Implement this interface to provide custom inventory logic (local, networked, etc.).
/// </summary>
public interface IInventoryController
{
    /// <summary>
    /// Called when the player clicks on a position in the inventory grid.
    /// </summary>
    /// <param name="pos">The inventory grid position that was clicked.</param>
    void InventoryClick(Packet_InventoryPosition pos);

    /// <summary>
    /// Called when the player drags an item from one position to a wear slot (or vice versa).
    /// </summary>
    /// <param name="from">The source inventory position.</param>
    /// <param name="to">The destination inventory position (wear slot).</param>
    void WearItem(Packet_InventoryPosition from, Packet_InventoryPosition to);

    /// <summary>
    /// Called when the player moves an item back into the inventory from an external source
    /// (e.g., a chest or crafting table).
    /// </summary>
    /// <param name="from">The source position outside the inventory.</param>
    void MoveToInventory(Packet_InventoryPosition from);
}