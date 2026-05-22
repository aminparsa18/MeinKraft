using MeinKraft;

public interface IBlockRegistry
{
    /// <summary>Block type definitions indexed by block type ID.</summary>
    Dictionary<int, BlockType> BlockTypes { get; set; }

    /// <summary>Pending block type definitions received from the server before map load.</summary>
    Dictionary<int, BlockType> NewBlockTypes { get; set; }

    int BlockIdAdminium { get; }
    int BlockIdCompass { get; }
    int BlockIdCraftingTable { get; }
    int BlockIdCuboid { get; }
    int BlockIdDirt { get; }
    int BlockIdEmpty { get; }
    int BlockIdEmptyHand { get; }
    int BlockIdFillArea { get; }
    int BlockIdFillStart { get; }
    int BlockIdLadder { get; }
    int BlockIdLava { get; }
    int BlockIdMinecart { get; }
    int BlockIdRailStart { get; }
    int BlockIdSponge { get; }
    int BlockIdStationaryLava { get; }
    int BlockIdTrampoline { get; }
    Dictionary<int, string[]> BreakSound { get; }
    Dictionary<int, string[]> BuildSound { get; }
    Dictionary<int, string[]> CloneSound { get; }
    Dictionary<int, int> DamageToPlayer { get; }
    int[] DefaultMaterialSlots { get; }
    Dictionary<int, bool> IsFlower { get; }
    Dictionary<int, bool> IsSlipperyWalk { get; }
    Dictionary<int, int> LightRadius { get; }
    Dictionary<int, int> Rail { get; }
    Dictionary<int, int> StartInventoryAmount { get; }
    Dictionary<int, float> Strength { get; }
    Dictionary<int, WalkableType> WalkableType { get; }
    Dictionary<int, string[]> WalkSound { get; }
    Dictionary<int, float> WalkSpeed { get; }
    Dictionary<int, int> WhenPlayerPlacesGetsConvertedTo { get; }

    bool IsRailTile(int id);
    bool IsValid(int blocktype);
    bool IsWater(int blockType);
    bool IsLava(int blockType);
    bool IsFillBlock(int blocktype);
    bool IsUsableBlock(int blocktype);
    void RegisterBlockType(int id, BlockType b);
    void Start();
    void UseBlockTypes(Dictionary<int, BlockType> blocktypes);
}