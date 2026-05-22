using MeinKraft;
using MemoryPack;
using OpenTK.Mathematics;
using System.Drawing;

//separate class because it's used by server and client.
public class InventoryUtil
{
    public InventoryUtil()
    {
        CellCountX = 12;
        CellCountY = 7 * 6;
    }
    public Inventory d_Inventory;
    public IGameDataItems d_Items;

    internal int CellCountX;
    internal int CellCountY;

    //returns null if area is invalid.
    public Point?[] ItemsAtArea(int pX, int pY, int sizeX, int sizeY, out int retCount)
    {
        Point?[] itemsAtArea = new Point?[256];
        int itemsAtAreaCount = 0;
        for (int xx = 0; xx < sizeX; xx++)
        {
            for (int yy = 0; yy < sizeY; yy++)
            {
                Point cell = new(pX + xx, pY + yy);
                if (!IsValidCell(cell))
                {
                    retCount = 0;
                    return null;
                }

                if (ItemAtCell(cell) != null)
                {
                    bool contains = false;
                    for (int i = 0; i < itemsAtAreaCount; i++)
                    {
                        if (itemsAtArea[i] == null)
                        {
                            continue;
                        }

                        if (itemsAtArea[i].Value.X == ItemAtCell(cell).Value.X
                            && itemsAtArea[i].Value.Y == ItemAtCell(cell).Value.Y)
                        {
                            contains = true;
                        }
                    }

                    if (!contains)
                    {
                        itemsAtArea[itemsAtAreaCount++] = ItemAtCell(cell);
                    }
                }
            }
        }

        retCount = itemsAtAreaCount;
        return itemsAtArea;
    }

    public bool IsValidCell(Point p) => !(p.X < 0 || p.Y < 0 || p.X >= CellCountX || p.Y >= CellCountY);

    public IEnumerable<Point> ItemCells(Point p)
    {
        InventoryItem item = d_Inventory.Items[new GridPoint(p.X, p.Y)];
        for (int x = 0; x < d_Items.ItemSizeX(item); x++)
        {
            for (int y = 0; y < d_Items.ItemSizeY(item); y++)
            {
                yield return new Point(p.X + x, p.Y + y);
            }
        }
    }

    public Point? ItemAtCell(Point p)
    {
        foreach (KeyValuePair<GridPoint, InventoryItem> k in d_Inventory.Items)
        {
            foreach (Point pp in ItemCells(new Point(k.Key.X, k.Key.Y)))
            {
                if (p.X == pp.X && p.Y == pp.Y)
                {
                    return new Point(k.Key.X, k.Key.Y);
                }
            }
        }

        return null;
    }

    public InventoryItem ItemAtWearPlace(int wearPlace, int activeMaterial)
    {
        return wearPlace switch
        {
            (int)WearPlace.RightHand => d_Inventory.RightHand[activeMaterial],
            (int)WearPlace.MainArmor => d_Inventory.MainArmor,
            (int)WearPlace.Boots => d_Inventory.Boots,
            (int)WearPlace.Helmet => d_Inventory.Helmet,
            (int)WearPlace.Gauntlet => d_Inventory.Gauntlet,
            _ => null,
        };
    }

    public void SetItemAtWearPlace(int wearPlace, int activeMaterial, InventoryItem item)
    {
        switch (wearPlace)
        {
            //case WearPlace.LeftHand: d_Inventory.LeftHand[activeMaterial] = item; break;
            case (int)WearPlace.RightHand: d_Inventory.RightHand[activeMaterial] = item; break;
            case (int)WearPlace.MainArmor: d_Inventory.MainArmor = item; break;
            case (int)WearPlace.Boots: d_Inventory.Boots = item; break;
            case (int)WearPlace.Helmet: d_Inventory.Helmet = item; break;
            case (int)WearPlace.Gauntlet: d_Inventory.Gauntlet = item; break;
            default: throw new Exception();
        }
    }

    public bool GrabItem(InventoryItem item, int ActiveMaterial)
    {
        switch (item.InventoryItemType)
        {
            case InventoryItemType.Block:
                if (item.BlockId == SpecialBlockId.Empty)
                {
                    return true;
                }
                //stacking
                for (int i = 0; i < 10; i++)
                {
                    if (d_Inventory.RightHand[i] == null)
                    {
                        continue;
                    }

                    InventoryItem result = d_Items.Stack(d_Inventory.RightHand[i], item);
                    if (result != null)
                    {
                        d_Inventory.RightHand[i] = result;
                        return true;
                    }
                }

                if (d_Inventory.RightHand[ActiveMaterial] == null)
                {
                    d_Inventory.RightHand[ActiveMaterial] = item;
                    return true;
                }
                //current hand
                if (d_Inventory.RightHand[ActiveMaterial].InventoryItemType == InventoryItemType.Block
                    && d_Inventory.RightHand[ActiveMaterial].BlockId == item.BlockId)
                {
                    d_Inventory.RightHand[ActiveMaterial].BlockCount++;
                    return true;
                }
                //any free hand
                for (int i = 0; i < 10; i++)
                {
                    if (d_Inventory.RightHand[i] == null)
                    {
                        d_Inventory.RightHand[i] = item;
                        return true;
                    }
                }
                //grab to main area - stacking
                for (int y = 0; y < CellCountY; y++)
                {
                    for (int x = 0; x < CellCountX; x++)
                    {
                        Point?[] p = ItemsAtArea(x, y, d_Items.ItemSizeX(item), d_Items.ItemSizeY(item), out int pCount);
                        if (p != null && pCount == 1)
                        {
                            InventoryItem stacked = d_Items.Stack(d_Inventory.Items[new GridPoint(p[0].Value.X, p[0].Value.Y)], item);
                            if (stacked != null)
                            {
                                d_Inventory.Items[new GridPoint(x, y)] = stacked;
                                return true;
                            }
                        }
                    }
                }
                //grab to main area - adding
                for (int y = 0; y < CellCountY; y++)
                {
                    for (int x = 0; x < CellCountX; x++)
                    {
                        Point?[] p = ItemsAtArea(x, y, d_Items.ItemSizeX(item), d_Items.ItemSizeY(item), out int pCount);
                        if (p != null && pCount == 0)
                        {
                            d_Inventory.Items[new GridPoint(x, y)] = item;
                            return true;
                        }
                    }
                }

                return false;
            default:
                throw new NotImplementedException();
        }
    }

    public int? FreeHand(int ActiveMaterial)
    {
        int? freehand = null;
        if (d_Inventory.RightHand[ActiveMaterial] == null)
        {
            return ActiveMaterial;
        }

        for (int i = 0; i < d_Inventory.RightHand.Length; i++)
        {
            if (d_Inventory.RightHand[i] == null)
            {
                return freehand;
            }
        }

        return null;
    }
}

public enum InventoryPositionType
{
    MainArea,
    Ground,
    MaterialSelector,
    WearPlace,
}

/// <summary>
/// Identifies a specific slot or position within the player's inventory UI.
/// Used to describe the source and destination of item move and click operations.
/// </summary>
[MemoryPackable]
public partial class InventoryPosition
{
    /// <summary>Which inventory region this position refers to (hotbar, main area, wear slot, etc.).</summary>
    public InventoryPositionType Type { get; set; }

    // ── Main inventory area ───────────────────────────────────────────────────

    /// <summary>Column index within the main inventory grid. Used when <see cref="Type"/> is <see cref="InventoryPositionType.MainArea"/>.</summary>
    public int AreaX { get; set; }

    /// <summary>Row index within the main inventory grid. Used when <see cref="Type"/> is <see cref="InventoryPositionType.MainArea"/>.</summary>
    public int AreaY { get; set; }

    // ── Hotbar ────────────────────────────────────────────────────────────────

    /// <summary>Hotbar slot index. Used when <see cref="Type"/> is <see cref="InventoryPositionType.MaterialSelector"/>.</summary>
    public int MaterialId { get; set; }

    /// <summary>Currently active hotbar slot index at the time of the operation.</summary>
    public int ActiveMaterial { get; set; }

    // ── Wear slots ────────────────────────────────────────────────────────────

    /// <summary>Wear slot identifier (armour slot). Used when <see cref="Type"/> is a wear position.</summary>
    public int WearPlace { get; set; }

    // ── Ground position ───────────────────────────────────────────────────────

    /// <summary>World X coordinate when dropping an item on the ground.</summary>
    public int GroundPositionX { get; set; }

    /// <summary>World Y coordinate when dropping an item on the ground.</summary>
    public int GroundPositionY { get; set; }

    /// <summary>World Z coordinate when dropping an item on the ground.</summary>
    public int GroundPositionZ { get; set; }

    // ── Factory helpers ───────────────────────────────────────────────────────

    /// <summary>Creates an <see cref="InventoryPosition"/> targeting a specific hotbar slot.</summary>
    /// <param name="materialId">Zero-based hotbar slot index.</param>
    public static InventoryPosition MaterialSelector(int materialId) => new()
    {
        Type = InventoryPositionType.MaterialSelector,
        MaterialId = materialId,
    };

    /// <summary>Creates an <see cref="InventoryPosition"/> targeting a cell in the main inventory grid.</summary>
    /// <param name="point">Grid coordinates of the target cell.</param>
    public static InventoryPosition MainArea(Point point) => new()
    {
        Type = InventoryPositionType.MainArea,
        AreaX = point.X,
        AreaY = point.Y,
    };
}

public interface IGameDataItems
{
    int ItemSizeX(InventoryItem item);
    int ItemSizeY(InventoryItem item);
    /// <summary>
    /// returns null if can't stack.
    /// </summary>
    InventoryItem Stack(InventoryItem itemA, InventoryItem itemB);
    bool CanWear(WearPlace selectedWear, InventoryItem item);
    string ItemGraphics(InventoryItem item);
}

public interface IDropItem
{
    void DropItem(ref InventoryItem item, Vector3i pos);
}

public class InventoryServer : IInventoryController
{
    public IGameDataItems? d_Items;
    public IDropItem? d_DropItem;

    public Inventory? d_Inventory;
    public InventoryUtil? d_InventoryUtil;

    public void InventoryClick(Packet_InventoryPosition pos)
    {
        if (pos.Type == PacketInventoryPositionType.MainArea)
        {
            Point? selected = null;
            foreach (KeyValuePair<GridPoint, InventoryItem> k in d_Inventory.Items)
            {
                if (pos.AreaX >= k.Key.X && pos.AreaY >= k.Key.Y
                    && pos.AreaX < k.Key.X + d_Items.ItemSizeX(k.Value)
                    && pos.AreaY < k.Key.Y + d_Items.ItemSizeY(k.Value))
                {
                    selected = new Point(k.Key.X, k.Key.Y);
                }
            }
            //drag
            if (selected != null && d_Inventory.DragDropItem == null)
            {
                d_Inventory.DragDropItem = d_Inventory.Items[new GridPoint(selected.Value.X, selected.Value.Y)];
                d_Inventory.Items.Remove(new GridPoint(selected.Value.X, selected.Value.Y));
                SendInventory();
            }
            //drop
            else if (d_Inventory.DragDropItem != null)
            {
                //make sure there is nothing blocking drop.
                Point?[] itemsAtArea = d_InventoryUtil.ItemsAtArea(pos.AreaX, pos.AreaY,
                    d_Items.ItemSizeX(d_Inventory.DragDropItem), d_Items.ItemSizeY(d_Inventory.DragDropItem), out int itemsAtAreaCount);
                if (itemsAtArea == null || itemsAtAreaCount > 1)
                {
                    //invalid area
                    return;
                }

                if (itemsAtAreaCount == 0)
                {
                    d_Inventory.Items.Add(new GridPoint(pos.AreaX, pos.AreaY), d_Inventory.DragDropItem);
                    d_Inventory.DragDropItem = null;
                }
                else //1
                {
                    Point? swapWith = itemsAtArea[0];
                    //try to stack                        
                    InventoryItem stackResult = d_Items.Stack(d_Inventory.Items[new GridPoint(swapWith.Value.X, swapWith.Value.Y)], d_Inventory.DragDropItem);
                    if (stackResult != null)
                    {
                        d_Inventory.Items[new GridPoint(swapWith.Value.X, swapWith.Value.Y)] = stackResult;
                        d_Inventory.DragDropItem = null;
                    }
                    else
                    {
                        //try to swap
                        //swap (swapWith, dragdropitem)
                        InventoryItem z = d_Inventory.Items[new GridPoint(swapWith.Value.X, swapWith.Value.Y)];
                        d_Inventory.Items.Remove(new GridPoint(swapWith.Value.X, swapWith.Value.Y));
                        d_Inventory.Items[new GridPoint(pos.AreaX, pos.AreaY)] = d_Inventory.DragDropItem;
                        d_Inventory.DragDropItem = z;
                    }
                }

                SendInventory();
            }
        }
        else if (pos.Type == PacketInventoryPositionType.Ground)
        {
            /*
            if (d_Inventory.DragDropItem != null)
            {
                d_DropItem.DropItem(ref d_Inventory.DragDropItem,
                    new Vector3i(pos.GroundPositionX, pos.GroundPositionY, pos.GroundPositionZ));
                SendInventory();
            }
            */
        }
        else if (pos.Type == PacketInventoryPositionType.MaterialSelector)
        {
            if (d_Inventory.DragDropItem == null && d_Inventory.RightHand[pos.MaterialId] != null)
            {
                d_Inventory.DragDropItem = d_Inventory.RightHand[pos.MaterialId];
                d_Inventory.RightHand[pos.MaterialId] = null;
            }
            else if (d_Inventory.DragDropItem != null && d_Inventory.RightHand[pos.MaterialId] == null)
            {
                if (d_Items.CanWear(WearPlace.RightHand, d_Inventory.DragDropItem))
                {
                    d_Inventory.RightHand[pos.MaterialId] = d_Inventory.DragDropItem;
                    d_Inventory.DragDropItem = null;
                }
            }
            else if (d_Inventory.DragDropItem != null && d_Inventory.RightHand[pos.MaterialId] != null)
            {
                if (d_Items.CanWear(WearPlace.RightHand, d_Inventory.DragDropItem))
                {
                    InventoryItem oldHand = d_Inventory.RightHand[pos.MaterialId];
                    d_Inventory.RightHand[pos.MaterialId] = d_Inventory.DragDropItem;
                    d_Inventory.DragDropItem = oldHand;
                }
            }

            SendInventory();
        }
        else if (pos.Type == PacketInventoryPositionType.WearPlace)
        {
            //just swap.
            InventoryItem wear = d_InventoryUtil.ItemAtWearPlace(pos.WearPlace, pos.ActiveMaterial);
            if (d_Items.CanWear((WearPlace)pos.WearPlace, d_Inventory.DragDropItem))
            {
                d_InventoryUtil.SetItemAtWearPlace(pos.WearPlace, pos.ActiveMaterial, d_Inventory.DragDropItem);
                d_Inventory.DragDropItem = wear;
            }

            SendInventory();
        }
        else
        {
            throw new Exception();
        }
    }

    private static void SendInventory()
    {
    }

    public void WearItem(Packet_InventoryPosition from, Packet_InventoryPosition to)
    {
        //todo
        GridPoint originPoint = new(from.AreaX, from.AreaY);
        if (from.Type == PacketInventoryPositionType.MainArea
            && to.Type == PacketInventoryPositionType.MaterialSelector
            && d_Inventory.RightHand[to.MaterialId] == null
            && d_Inventory.Items.ContainsKey(originPoint)
            && d_Items.CanWear(WearPlace.RightHand, d_Inventory.Items[originPoint]))
        {
            d_Inventory.RightHand[to.MaterialId] = d_Inventory.Items[originPoint];
            d_Inventory.Items.Remove(originPoint);
        }
    }

    public void MoveToInventory(Packet_InventoryPosition from)
    {
        //todo
        if (from.Type == PacketInventoryPositionType.MaterialSelector)
        {
            //duplicate code with GrabItem().
            InventoryItem item = d_Inventory.RightHand[from.MaterialId];
            if (item == null)
            {
                return;
            }
            //grab to main area - stacking
            for (int x = 0; x < d_InventoryUtil.CellCountX; x++)
            {
                for (int y = 0; y < d_InventoryUtil.CellCountY; y++)
                {
                    Point?[] p = d_InventoryUtil.ItemsAtArea(x, y, d_Items.ItemSizeX(item), d_Items.ItemSizeY(item), out int pCount);
                    if (p != null && pCount == 1)
                    {
                        InventoryItem stacked = d_Items.Stack(d_Inventory.Items[new GridPoint(p[0].Value.X, p[0].Value.Y)], item);
                        if (stacked != null)
                        {
                            d_Inventory.Items[new GridPoint(x, y)] = stacked;
                            d_Inventory.RightHand[from.MaterialId] = null;
                            return;
                        }
                    }
                }
            }
            //grab to main area - adding
            for (int x = 0; x < d_InventoryUtil.CellCountX; x++)
            {
                for (int y = 0; y < d_InventoryUtil.CellCountY; y++)
                {
                    Point?[] p = d_InventoryUtil.ItemsAtArea(x, y, d_Items.ItemSizeX(item), d_Items.ItemSizeY(item), out int pCount);
                    if (p != null && pCount == 0)
                    {
                        d_Inventory.Items[new GridPoint(x, y)] = item;
                        d_Inventory.RightHand[from.MaterialId] = null;
                        return;
                    }
                }
            }
        }
    }
}
public class GameDataItemsBlocks : IGameDataItems
{
    public IBlockRegistry? d_Data;

    public int ItemSizeX(InventoryItem item)
    {
        if (item.InventoryItemType == InventoryItemType.Block)
        {
            return 1;
        }

        throw new NotImplementedException();
    }

    public int ItemSizeY(InventoryItem item)
    {
        if (item.InventoryItemType == InventoryItemType.Block)
        {
            return 1;
        }

        throw new NotImplementedException();
    }

    public InventoryItem Stack(InventoryItem itemA, InventoryItem itemB)
    {
        if (itemA.InventoryItemType == InventoryItemType.Block
            && itemB.InventoryItemType == InventoryItemType.Block)
        {
            int railcountA = DirectionUtils.RailDirectionFlagsCount(d_Data.Rail[itemA.BlockId]);
            int railcountB = DirectionUtils.RailDirectionFlagsCount(d_Data.Rail[itemB.BlockId]);
            if ((itemA.BlockId != itemB.BlockId) && (!(railcountA > 0 && railcountB > 0)))
            {
                return null;
            }
            //todo stack size limit
            InventoryItem ret = new()
            {
                InventoryItemType = itemA.InventoryItemType,
                BlockId = itemA.BlockId,
                BlockCount = itemA.BlockCount + itemB.BlockCount
            };
            return ret;
        }
        else
        {
            return null;
        }
    }

    public bool CanWear(WearPlace selectedWear, InventoryItem item)
    {
        if (item == null)
        {
            return true;
        }

        if (item == null)
        {
            return true;
        }

        return selectedWear switch
        {
            //case WearPlace.LeftHand: return false;
            WearPlace.RightHand => item.InventoryItemType == InventoryItemType.Block,
            WearPlace.MainArmor => false,
            WearPlace.Boots => false,
            WearPlace.Helmet => false,
            WearPlace.Gauntlet => false,
            _ => throw new Exception(),
        };
    }

    public string ItemGraphics(InventoryItem item) => throw new NotImplementedException();
}
