using MeinKraft;

// When the game server sends the client a list of all block types (dirt, lava, ladder, rail, etc.), this class reads that list and stores every block's properties in arrays, indexed by the block's ID number. 
// So if dirt is block #5, then WalkSpeed[5], Strength[5], DamageToPlayer[5] etc. all tell you everything about dirt.
//The game then consults this registry constantly to answer questions like:

//Physics — is this block slippery? How fast can you walk on it? Can you walk through it at all?
//Combat — does standing in this block hurt you (like lava)?
//Mining — how long does this block take to break?
//Rendering — does this block glow? Does it draw like a flower/plant?
//Sound — what sound plays when you walk on, break, or place this block?
//Rails — is this block part of a rail track?

//It also keeps a shortlist of special block IDs (lava, ladder, trampoline, compass, etc.) that the game needs to reference by name for specific logic — like "is the player standing in lava?" checks DamageToPlayer[BlockIdLava].
//So essentially: the server defines the rules, this class stores them, and the rest of the game reads from it constantly.When the game server sends the client a list of all block types (dirt, lava, ladder, rail, etc.), this class reads that list and stores every block's properties in arrays, indexed by the block's ID number. So if dirt is block #5, then WalkSpeed[5], Strength[5], DamageToPlayer[5] etc. all tell you everything about dirt.
//The game then consults this registry constantly to answer questions like:

//Physics — is this block slippery? How fast can you walk on it? Can you walk through it at all?
//Combat — does standing in this block hurt you (like lava)?
//Mining — how long does this block take to break?
//Rendering — does this block glow? Does it draw like a flower/plant?
//Sound — what sound plays when you walk on, break, or place this block?
//Rails — is this block part of a rail track?

//It also keeps a shortlist of special block IDs (lava, ladder, trampoline, compass, etc.) that the game needs to reference by name for specific logic — like "is the player standing in lava?" checks DamageToPlayer[BlockIdLava].
//So essentially: the server defines the rules, this class stores them, and the rest of the game reads from it constantly.

/// <summary>
/// Registry of per-block-type gameplay properties derived from server
/// <see cref="Packet_BlockType"/> descriptors.
/// </summary>
/// <remarks>
/// <para>
/// Each public array property is indexed by block-type ID in the range
/// [0, <see cref="GameConstants.MAX_BLOCKTYPES"/>). All arrays are allocated in
/// <see cref="Start"/> and populated via <see cref="UseBlockTypes"/>.
/// </para>
/// <para>
/// <b>Rename note:</b> This class was previously named <c>GameData</c>.
/// <c>BlockTypeRegistry</c> better describes its responsibility — it registers
/// block-type metadata (physics, sounds, special IDs) sent by the server.
/// </para>
/// </remarks>
public class BlockRegistry : IBlockRegistry
{
    /// <summary>Block type definitions indexed by block type ID.</summary>
    public Dictionary<int, BlockType> BlockTypes { get; set; } = [];

    /// <summary>Pending block type definitions received from the server before map load.</summary>
    public Dictionary<int, BlockType> NewBlockTypes { get; set; } = [];

    // ── Block-type property dictionaries (keyed by block ID) ──────────────────

    /// <summary>Maps a placed block ID to the block ID it converts into on placement.</summary>
    public Dictionary<int, int> WhenPlayerPlacesGetsConvertedTo { get; private set; } = [];

    /// <summary>Whether each block renders as a flower/plant (cross-face geometry).</summary>
    public Dictionary<int, bool> IsFlower { get; private set; } = [];

    /// <summary>Rail sub-type for each block ID (0 = not a rail).</summary>
    public Dictionary<int, int> Rail { get; private set; } = [];

    /// <summary>Walk speed multiplier for each block type (default 1.0).</summary>
    public Dictionary<int, float> WalkSpeed { get; private set; } = [];

    /// <summary>Whether each block type causes slippery walking physics.</summary>
    public Dictionary<int, bool> IsSlipperyWalk { get; private set; } = [];

    /// <summary>Per-block walk sound filenames. Indexed as [blockId][soundVariant].</summary>
    public Dictionary<int, string[]> WalkSound { get; private set; } = [];

    /// <summary>Per-block break sound filenames. Indexed as [blockId][soundVariant].</summary>
    public Dictionary<int, string[]> BreakSound { get; private set; } = [];

    /// <summary>Per-block build/place sound filenames. Indexed as [blockId][soundVariant].</summary>
    public Dictionary<int, string[]> BuildSound { get; private set; } = [];

    /// <summary>Per-block clone sound filenames. Indexed as [blockId][soundVariant].</summary>
    public Dictionary<int, string[]> CloneSound { get; private set; } = [];

    /// <summary>Emitted light radius for each block type (0 = no light).</summary>
    public Dictionary<int, int> LightRadius { get; private set; } = [];

    /// <summary>Default starting inventory amount for each block type, keyed by block type id.</summary>
    public Dictionary<int, int> StartInventoryAmount { get; private set; } = [];

    /// <summary>Mining strength (time-to-break) for each block type.</summary>
    public Dictionary<int, float> Strength { get; private set; } = [];

    /// <summary>Contact damage dealt to the player per tick for each block type.</summary>
    public Dictionary<int, int> DamageToPlayer { get; private set; } = [];

    /// <summary>Walkability classification for each block type.</summary>
    public Dictionary<int, WalkableType> WalkableType { get; private set; } = [];

    /// <summary>Default hotbar material slot assignments.</summary>
    public int[] DefaultMaterialSlots { get; private set; } = new int[10];

    /// <summary>Maximum number of sound variants stored per block per sound category.</summary>
    public const int SoundCount = 8;

    // ── Special block IDs ─────────────────────────────────────────────────────

    public int BlockIdEmpty { get; private set; } = 0;
    public int BlockIdDirt { get; private set; } = -1;
    public int BlockIdSponge { get; private set; } = -1;
    public int BlockIdTrampoline { get; private set; } = -1;
    public int BlockIdAdminium { get; private set; } = -1;
    public int BlockIdCompass { get; private set; } = -1;
    public int BlockIdLadder { get; private set; } = -1;
    public int BlockIdEmptyHand { get; private set; } = -1;
    public int BlockIdCraftingTable { get; private set; } = -1;
    public int BlockIdLava { get; private set; } = -1;
    public int BlockIdStationaryLava { get; private set; } = -1;
    public int BlockIdFillStart { get; private set; } = -1;
    public int BlockIdCuboid { get; private set; } = -1;
    public int BlockIdFillArea { get; private set; } = -1;
    public int BlockIdMinecart { get; private set; } = -1;
    public int BlockIdRailStart { get; private set; } = -128;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <summary>Clears all dictionaries, ready for a fresh block type load.</summary>
    public void Start() => ClearAll();

    private void ClearAll()
    {
        WhenPlayerPlacesGetsConvertedTo.Clear();
        IsFlower.Clear();
        Rail.Clear();
        WalkSpeed.Clear();
        IsSlipperyWalk.Clear();
        WalkSound.Clear();
        BreakSound.Clear();
        BuildSound.Clear();
        CloneSound.Clear();
        LightRadius.Clear();
        StartInventoryAmount.Clear();
        Strength.Clear();
        DamageToPlayer.Clear();
        WalkableType.Clear();
        DefaultMaterialSlots = new int[10];
    }

    // ── Block type loading ────────────────────────────────────────────────────

    /// <inheritdoc cref="RegisterBlockType"/>
    public void UseBlockTypes(Dictionary<int, BlockType> blocktypes)
    {
        foreach ((int id, BlockType? blockType) in blocktypes)
        {
            RegisterBlockType(id, blockType);
        }
    }

    /// <summary>
    /// Populates all per-block-type dictionaries for block ID <paramref name="id"/>
    /// from the server descriptor <paramref name="b"/> and resolves any special
    /// block ID this block may represent.
    /// </summary>
    public void RegisterBlockType(int id, BlockType b)
    {
        if (b.Name == null)
        {
            return;
        }

        WhenPlayerPlacesGetsConvertedTo[id] = b.WhenPlayerPlacesGetsConvertedTo != 0
            ? b.WhenPlayerPlacesGetsConvertedTo
            : id;

        IsFlower[id] = b.DrawType == DrawType.Plant;
        Rail[id] = b.Rail;
        WalkSpeed[id] = b.WalkSpeed != 0 ? b.WalkSpeed : 1f;
        IsSlipperyWalk[id] = b.IsSlipperyWalk;
        LightRadius[id] = b.LightRadius;
        Strength[id] = b.Strength;
        DamageToPlayer[id] = b.DamageToPlayer;
        WalkableType[id] = b.WalkableType;

        // ── Sounds ────────────────────────────────────────────────────────────
        string[] walk = new string[SoundCount];
        string[] brk = new string[SoundCount];
        string[] build = new string[SoundCount];
        string[] clone = new string[SoundCount];

        if (b.Sounds != null)
        {
            for (int i = 0; i < b.Sounds.Walk.Length; i++)
            {
                walk[i] = b.Sounds.Walk[i] + ".wav";
            }

            for (int i = 0; i < b.Sounds.Break.Length; i++)
            {
                brk[i] = b.Sounds.Break[i] + ".wav";
            }

            for (int i = 0; i < b.Sounds.Build.Length; i++)
            {
                build[i] = b.Sounds.Build[i] + ".wav";
            }

            for (int i = 0; i < b.Sounds.Clone.Length; i++)
            {
                clone[i] = b.Sounds.Clone[i] + ".wav";
            }
        }

        WalkSound[id] = walk;
        BreakSound[id] = brk;
        BuildSound[id] = build;
        CloneSound[id] = clone;

        _ = SetSpecialBlockId(b, id);
    }

    // ── Special block ID registration ─────────────────────────────────────────

    private bool SetSpecialBlockId(BlockType b, int id)
    {
        switch (b.Name)
        {
            case "Empty": BlockIdEmpty = id; return true;
            case "Dirt": BlockIdDirt = id; return true;
            case "Sponge": BlockIdSponge = id; return true;
            case "Trampoline": BlockIdTrampoline = id; return true;
            case "Adminium": BlockIdAdminium = id; return true;
            case "Compass": BlockIdCompass = id; return true;
            case "Ladder": BlockIdLadder = id; return true;
            case "EmptyHand": BlockIdEmptyHand = id; return true;
            case "CraftingTable": BlockIdCraftingTable = id; return true;
            case "Lava": BlockIdLava = id; return true;
            case "StationaryLava": BlockIdStationaryLava = id; return true;
            case "FillStart": BlockIdFillStart = id; return true;
            case "Cuboid": BlockIdCuboid = id; return true;
            case "FillArea": BlockIdFillArea = id; return true;
            case "Minecart": BlockIdMinecart = id; return true;
            case "Rail0": BlockIdRailStart = id; return true;
            default: return false;
        }
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="id"/> falls within the
    /// 64-tile rail sequence starting at <see cref="BlockIdRailStart"/>.
    /// </summary>
    public bool IsRailTile(int id)
        => id >= BlockIdRailStart && id < BlockIdRailStart + 64;

    /// <summary>
    /// Returns <see langword="true"/> when the block at this ID has a name assigned.
    /// </summary>
    public bool IsValid(int blocktype) => BlockTypes[blocktype].Name != null;

    /// <summary>
    /// Fix #1: use registry ID instead of name-based string check.
    /// Returns <see langword="true"/> when the block is the registered water block.
    /// </summary>
    public bool IsWater(int blockType)
    {
        string name = BlockTypes[blockType].Name;
        return name != null && name.Contains("Water");
    }

    /// <summary>
    /// Fix #1: use registry ID instead of name-based string check.
    /// Returns <see langword="true"/> when the block is the registered lava block.
    /// </summary>
    public bool IsLava(int blockType)
        => BlockIdLava >= 0 && blockType == BlockIdLava;

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="blocktype"/> is one of
    /// the fill/cuboid tool blocks that should not be treated as real terrain.
    /// </summary>
    public bool IsFillBlock(int blocktype)
        => blocktype == BlockIdFillArea
        || blocktype == BlockIdFillStart
        || blocktype == BlockIdCuboid;

    /// <summary>
    /// Returns <see langword="true"/> when the block can be interacted with
    /// (rail tiles or blocks with the <c>IsUsable</c> flag).
    /// </summary>
    public bool IsUsableBlock(int blocktype)
        => IsRailTile(blocktype) || BlockTypes[blocktype].IsUsable;
}