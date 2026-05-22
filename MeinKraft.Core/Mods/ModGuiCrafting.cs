using OpenTK.Mathematics;
using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;

/// <summary>
/// Handles the crafting table UI — drawing available recipes, mouse selection, and sending craft packets.
/// </summary>
public class ModGuiCrafting : ModBase
{
    private const int RecipeRowHeight = 80;
    private const int MenuWidth = 600;
    private const int OutputColumnOffset = 400;
    private const int FontSize = 12;

    private readonly PacketHandlerCraftingRecipes handler;

    // ── Per-frame scratch (pre-allocated) ─────────────────────────────────────
    /// <summary>
    /// Indices into <see cref="craftingRecipes2"/> of recipes the player currently
    /// has materials for. Populated every frame by <see cref="DrawCraftingRecipes"/>.
    /// Pre-allocated to avoid a <c>new int[1024]</c> allocation on every draw call.
    /// </summary>
    private readonly int[] currentRecipes = new int[1024];
    private int currentRecipesCount;

    // ── Crafting session state ────────────────────────────────────────────────
    private int craftingTablePosX, craftingTablePosY, craftingTablePosZ;
    private CraftingRecipe[] craftingRecipes2;
    private int craftingRecipes2Count;
    private int craftingSelectedRecipe;

    /// <summary>
    /// Copy of the on-table block list for the current session.
    /// This is a private owned array — NOT a reference to CraftingTableTool's
    /// internal buffer — so that re-opening the crafting table cannot corrupt
    /// an active session's ingredient counts.
    /// </summary>
    private readonly int[] _craftingBlocksCopy = new int[2048];

    /// <summary>
    /// Per-block-type count of what is currently on the table.
    /// Built once in <see cref="CraftingRecipesStart"/> and read in
    /// <see cref="DrawCraftingRecipes"/>. Replaces the O(n) <c>CountBlock</c>
    /// scan that ran for every ingredient of every recipe, every frame.
    /// Indexed by block type ID; size must be at least <see cref="GameConstants.MAX_BLOCKTYPES"/>.
    /// </summary>
    private readonly int[] _blockTypeCounts = new int[GameConstants.MAX_BLOCKTYPES];

    // ── Injected dependencies ─────────────────────────────────────────────────
    internal CraftingRecipe[] d_CraftingRecipes;
    internal int d_CraftingRecipesCount;
    internal CraftingTableTool d_CraftingTableTool;

    // ── Once-flags ────────────────────────────────────────────────────────────
    /// <summary>
    /// True after the packet handler has been registered.
    /// Prevents re-registering the same handler reference on every frame.
    /// </summary>
    private bool _handlerRegistered;

    private readonly IVoxelMap voxelMap;
    private readonly IBlockRegistry blockTypeRegistry;

    public ModGuiCrafting(IGameWindowService gameService, IVoxelMap voxelMap, IBlockRegistry blockTypeRegistry, IGame game) : base(game)
    {
        this.voxelMap = voxelMap;
        this.blockTypeRegistry = blockTypeRegistry;
        handler = new PacketHandlerCraftingRecipes(gameService, game) { mod = this };
    }

    // ── ModBase overrides ─────────────────────────────────────────────────────

    public override void OnRender2d(float deltaTime)
    {
        // Lazy-initialise the tool once.
        d_CraftingTableTool ??= new CraftingTableTool
        {
            d_Map = new MapStorage(voxelMap),
            d_Data = blockTypeRegistry
        };

        // Register the packet handler once, not every frame.
        if (!_handlerRegistered)
        {
            Game.PacketHandlers[(int)Packet_ServerIdEnum.CraftingRecipes] = handler;
            _handlerRegistered = true;
        }

        if (Game.GuiState == GameState.CraftingRecipes)
        {
            DrawCraftingRecipes();
        }
    }

    public override void OnUpdate(float args)
    {
        if (Game.GuiState == GameState.CraftingRecipes)
        {
            CraftingMouse();
        }
    }

    public override void OnKeyDown(KeyEventArgs args)
    {
        if (args.KeyChar != Game.GetKey(Keys.E) || Game.GuiTyping != TypingState.None)
        {
            return;
        }

        if (Game.SelectedBlockPositionX == -1
         && Game.SelectedBlockPositionY == -1
         && Game.SelectedBlockPositionZ == -1)
        {
            return;
        }

        int posX = Game.SelectedBlockPositionX;
        int posY = Game.SelectedBlockPositionZ;
        int posZ = Game.SelectedBlockPositionY;

        if (voxelMap.GetBlock(posX, posY, posZ) != blockTypeRegistry.BlockIdCraftingTable)
        {
            return;
        }

        // GetTable / GetOnTable return references to CraftingTableTool's internal
        // reusable buffers. CraftingRecipesStart copies the on-table data into
        // _craftingBlocksCopy before storing it, so a subsequent table-open
        // cannot corrupt the active session.
        Vector3i[] table = d_CraftingTableTool.GetTable(posX, posY, posZ, out int tableCount);
        int[] onTable = d_CraftingTableTool.GetOnTable(table, tableCount, out int onTableCount);

        CraftingRecipesStart(d_CraftingRecipes, d_CraftingRecipesCount,
            onTable, onTableCount,
            posX, posY, posZ);

        args.Handled = true;
    }

    // ── Drawing ───────────────────────────────────────────────────────────────

    internal void DrawCraftingRecipes()
    {
        // ── Filter recipes for which the player has all materials ─────────────
        // Uses _blockTypeCounts (built once in CraftingRecipesStart) for O(1)
        // ingredient lookup. The old CountBlock was an O(n) linear scan called
        // for every ingredient of every recipe, every frame.
        currentRecipesCount = 0;
        for (int i = 0; i < craftingRecipes2Count; i++)
        {
            CraftingRecipe r = craftingRecipes2[i];
            if (r == null)
            {
                continue;
            }

            bool canCraft = true;
            for (int k = 0; k < r.Ingredients.Length; k++)
            {
                Ingredient ing = r.Ingredients[k];
                if (ing == null)
                {
                    continue;
                }

                if (_blockTypeCounts[ing.Type] < ing.Amount)
                {
                    canCraft = false;
                    break;
                }
            }

            if (canCraft)
            {
                currentRecipes[currentRecipesCount++] = i;
            }
        }

        if (currentRecipesCount == 0)
        {
            Game.Draw2dText1(Game.Language.NoMaterialsForCrafting(),
                Game.Xcenter(200), Game.Ycenter(20), FontSize, null, false);
            return;
        }

        int menuX = Game.Xcenter(MenuWidth);
        int menuY = Game.Ycenter(currentRecipesCount * RecipeRowHeight);

        for (int i = 0; i < currentRecipesCount; i++)
        {
            CraftingRecipe r = craftingRecipes2[currentRecipes[i]];
            int rowY = menuY + (i * RecipeRowHeight);
            int color = i == craftingSelectedRecipe
                ? ColorUtils.ColorFromArgb(255, 255, 0, 0)
                : ColorUtils.ColorFromArgb(255, 255, 255, 255);
            int white = ColorUtils.ColorFromArgb(255, 255, 255, 255);

            for (int ii = 0; ii < r.Ingredients.Length; ii++)
            {
                Ingredient ing = r.Ingredients[ii];
                int colX = menuX + 20 + (ii * 130);
                Game.Draw2dTexture(Game.TerrainTexture,
                    colX, rowY, 32, 32,
                    Game.TextureIdForInventory[ing.Type], GameConstants.MAX_BLOCKTYPES_SQRT, white, false);
                Game.Draw2dText1($"{ing.Amount} {blockTypeRegistry.BlockTypes[ing.Type].Name}",
                    colX + 50, rowY, FontSize, color, false);
            }

            int outX = menuX + 20 + OutputColumnOffset;
            Game.Draw2dTexture(Game.TerrainTexture,
                outX, rowY, 32, 32,
                Game.TextureIdForInventory[r.Output.Type], GameConstants.MAX_BLOCKTYPES_SQRT, white, false);
            Game.Draw2dText1($"{r.Output.Amount} {blockTypeRegistry.BlockTypes[r.Output.Type].Name}",
                outX + 50, rowY, FontSize, color, false);
        }
    }

    // ── Input ─────────────────────────────────────────────────────────────────

    internal void CraftingMouse()
    {
        if (currentRecipesCount == 0)
        {
            return;
        }

        int menuY = Game.Ycenter(currentRecipesCount * RecipeRowHeight);

        if (Game.MouseCurrentY >= menuY
         && Game.MouseCurrentY < menuY + (currentRecipesCount * RecipeRowHeight))
        {
            craftingSelectedRecipe = (Game.MouseCurrentY - menuY) / RecipeRowHeight;
        }

        if (!Game.MouseLeftClick)
        {
            return;
        }

        Game.SendPacketClient(ClientPackets.Craft(
            craftingTablePosX, craftingTablePosY, craftingTablePosZ,
            currentRecipes[craftingSelectedRecipe]));

        Game.MouseLeftClick = false;
        Game.GuiStateBackToGame();
    }

    // ── Session management ────────────────────────────────────────────────────

    internal void CraftingRecipesStart(CraftingRecipe[] recipes, int recipesCount,
        int[] blocks, int blocksCount,
        int posX, int posY, int posZ)
    {
        craftingRecipes2 = recipes;
        craftingRecipes2Count = recipesCount;
        craftingTablePosX = posX;
        craftingTablePosY = posY;
        craftingTablePosZ = posZ;

        // ── Copy the on-table block list into our own buffer ──────────────────
        // 'blocks' is a reference to CraftingTableTool._onTableBuffer — a shared
        // reusable array. Storing the reference directly (the old code) meant
        // the next GetOnTable call would silently overwrite craftingBlocks while
        // DrawCraftingRecipes was still reading it.
        Array.Copy(blocks, _craftingBlocksCopy, blocksCount);

        // ── Build O(1) lookup table from the copied block list ────────────────
        // Clear only the range that was previously populated to avoid a full
        // MAX_BLOCKTYPES clear on every open (most block IDs will be zero anyway).
        Array.Clear(_blockTypeCounts, 0, GameConstants.MAX_BLOCKTYPES);
        for (int i = 0; i < blocksCount; i++)
        {
            int blockId = _craftingBlocksCopy[i];
            if ((uint)blockId < GameConstants.MAX_BLOCKTYPES)
            {
                _blockTypeCounts[blockId]++;
            }
        }

        Game.GuiState = GameState.CraftingRecipes;
        Game.MenuState = new MenuState();
        Game.SetFreeMouse(true);
    }
}