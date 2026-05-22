using MeinKraft;

public interface IInventoryService
{
    string ItemInfo(InventoryItem item);
    Dictionary<int, int> TextureIdForInventory();
}