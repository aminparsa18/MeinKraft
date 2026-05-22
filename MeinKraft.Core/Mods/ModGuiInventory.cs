using MeinKraft;
using OpenTK.Windowing.GraphicsLibraryFramework;
using SkiaSharp;
using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;

/// <summary>
/// Renders and handles interaction for the inventory screen, including the main
/// item grid, wear-place slots, material selector bar, drag-drop feedback, and
/// item tooltips.
/// </summary>
public class ModGuiInventory : ModBase
{
    /// <summary>Reference to the current game instance.</summary>
    private readonly IGameWindowService platform;

    /// <summary>Item data helpers (texture IDs, sizes, display info).</summary>
    private IInventoryService inventoryService;
    
    private IBlockRegistry _blockTypeRegistry;

    /// <summary>Client-side inventory query utilities (cell lookups, area checks).</summary>
    internal InventoryUtilClient inventoryUtil;

    /// <summary>Controller that translates UI clicks into inventory packets.</summary>
    internal IInventoryController controller;

    /// <summary>Pixel size of one inventory cell in both axes.</summary>
    internal int CellDrawSize;

    /// <summary>Number of cells visible per page horizontally.</summary>
    private readonly int _cellCountInPageX;

    /// <summary>Number of cells visible per page vertically.</summary>
    private readonly int _cellCountInPageY;

    /// <summary>Total cell rows across all pages of the inventory grid.</summary>
    private readonly int _cellCountTotalY;

    /// <summary>
    /// Screen-space X origins of each wear-place slot, indexed by <see cref="WearPlace"/>.
    /// Relative to <see cref="InventoryStartX"/>.
    /// </summary>
    private readonly Point[] _wearPlaceStart;

    /// <summary>
    /// Cell dimensions (columns × rows) of each wear-place slot, indexed by <see cref="WearPlace"/>.
    /// </summary>
    private readonly Point[] _wearPlaceCells;

    /// <summary>Number of wear-place entries (length of <see cref="_wearPlaceStart"/>).</summary>
    private const int WearPlaceCount = 5;

    /// <summary>First row of the inventory currently scrolled into view.</summary>
    internal int ScrollLine;

    /// <summary>
    /// Timestamp (ms) when the scroll-up button was last pressed for auto-scroll.
    /// <c>0</c> means not scrolling.
    /// </summary>
    private int _scrollingUpTimeMs;

    /// <summary>
    /// Timestamp (ms) when the scroll-down button was last pressed for auto-scroll.
    /// <c>0</c> means not scrolling.
    /// </summary>
    private int _scrollingDownTimeMs;

    /// <summary>Initialises cell dimensions and wear-place layout data.</summary>
    public ModGuiInventory(IGameWindowService platform, IGame game, IBlockRegistry blockRegistry) : base(game)
    {
        this.platform = platform;
        _blockTypeRegistry = blockRegistry;
        // Wear-place slot origins (relative to inventory background image origin).
        _wearPlaceStart = new Point[WearPlaceCount];
        _wearPlaceStart[(int)WearPlace.RightHand] = new Point(34, 100);
        _wearPlaceStart[(int)WearPlace.MainArmor] = new Point(74, 100);
        _wearPlaceStart[(int)WearPlace.Boots] = new Point(194, 100);
        _wearPlaceStart[(int)WearPlace.Helmet] = new Point(114, 100);
        _wearPlaceStart[(int)WearPlace.Gauntlet] = new Point(154, 100);

        // All wear-place slots are 1×1 cells.
        _wearPlaceCells = new Point[WearPlaceCount];
        for (int i = 0; i < WearPlaceCount; i++)
        {
            _wearPlaceCells[i] = new Point(1, 1);
        }

        _cellCountInPageX = 12;
        _cellCountInPageY = 7;
        _cellCountTotalY = 7 * 6;
        CellDrawSize = 40;
    }

    /// <summary>Returns the screen X coordinate of the inventory background's left edge.</summary>
    public int InventoryStartX() => (platform.CanvasWidth / 2) - (560 / 2);

    /// <summary>Returns the screen Y coordinate of the inventory background's top edge.</summary>
    public int InventoryStartY() => (platform.CanvasHeight / 2) - (600 / 2);

    /// <summary>Returns the screen X coordinate of the top-left inventory cell.</summary>
    public int CellsStartX() => 33 + InventoryStartX();

    /// <summary>Returns the screen Y coordinate of the top-left inventory cell.</summary>
    public int CellsStartY() => 180 + InventoryStartY();

    /// <summary>Returns the pixel size of one material-selector cell at the current UI scale.</summary>
    public int ActiveMaterialCellSize() => (int)(48 * Game.Scale());

    private int MaterialSelectorStartX() => (int)(MaterialSelectorBgStartX() + (17 * Game.Scale()));
    private int MaterialSelectorStartY() => (int)(MaterialSelectorBgStartY() + (17 * Game.Scale()));
    private int MaterialSelectorBgStartX() => (int)((platform.CanvasWidth / 2) - (512 / 2 * Game.Scale()));
    private int MaterialSelectorBgStartY() => (int)(platform.CanvasHeight - (90 * Game.Scale()));

    private int ScrollButtonSize() => CellDrawSize;
    private int ScrollUpButtonX() => CellsStartX() + (_cellCountInPageX * CellDrawSize);
    private int ScrollUpButtonY() => CellsStartY();
    private int ScrollDownButtonX() => CellsStartX() + (_cellCountInPageX * CellDrawSize);
    private int ScrollDownButtonY() => CellsStartY() + ((_cellCountInPageY - 1) * CellDrawSize);

    /// <inheritdoc/>
    public override void OnKeyPress(KeyPressEventArgs args)
    {
        if (Game.GuiState != GameState.Inventory)
        {
            return;
        }

        // Key codes 49–57 = '1'–'9', 48 = '0' → material slots 0–9.
        int keyChar = args.KeyChar;
        if (keyChar is >= 49 and <= 57)
        {
            Game.ActiveMaterial = keyChar - 49;
        }

        if (keyChar == 48)
        {
            Game.ActiveMaterial = 9;
        }
    }

    /// <inheritdoc/>
    public override void OnMouseDown(MouseEventArgs args)
    {
        if (Game.GuiState != GameState.Inventory)
        {
            return;
        }

        Point mouse = new(args.X, args.Y);

        // Material selector bar click.
        int? materialSlot = SelectedMaterialSelectorSlot(mouse);
        if (materialSlot != null)
        {
            Game.ActiveMaterial = materialSlot.Value;
            controller.InventoryClick(new Packet_InventoryPosition
            {
                Type = PacketInventoryPositionType.MaterialSelector,
                MaterialId = Game.ActiveMaterial
            });
            args.            Handled = true;
            return;
        }

        // Main grid click.
        Point? cell = SelectedCell(mouse);
        if (cell != null)
        {
            Packet_InventoryPosition mainClick = new()
            {
                Type = PacketInventoryPositionType.MainArea,
                AreaX = cell.Value.X,
                AreaY = cell.Value.Y + ScrollLine
            };

            if (args.Button == (int)MouseButton.Left)
            {
                controller.InventoryClick(mainClick);
            }
            else
            {
                // Right-click: pick up → equip to right hand → put back.
                controller.InventoryClick(mainClick);
                controller.InventoryClick(new Packet_InventoryPosition
                {
                    Type = PacketInventoryPositionType.WearPlace,
                    WearPlace = (int)WearPlace.RightHand,
                    ActiveMaterial = Game.ActiveMaterial
                });
                controller.InventoryClick(mainClick);
            }

            if (Game.GuiState == GameState.Inventory)
            {
                args.                Handled = true;
            }

            return;
        }

        // Wear-place slot click.
        int? wearPlace = SelectedWearPlace(mouse);
        if (wearPlace != null)
        {
            controller.InventoryClick(new Packet_InventoryPosition
            {
                Type = PacketInventoryPositionType.WearPlace,
                WearPlace = wearPlace.Value,
                ActiveMaterial = Game.ActiveMaterial
            });
            args.            Handled = true;
            return;
        }

        // Scroll-up button.
        if (HitTest(mouse, ScrollUpButtonX(), ScrollUpButtonY(), ScrollButtonSize(), ScrollButtonSize()))
        {
            ScrollUp();
            _scrollingUpTimeMs = platform.TimeMillisecondsFromStart;
            args.            Handled = true;
            return;
        }

        // Scroll-down button.
        if (HitTest(mouse, ScrollDownButtonX(), ScrollDownButtonY(), ScrollButtonSize(), ScrollButtonSize()))
        {
            ScrollDown();
            _scrollingDownTimeMs = platform.TimeMillisecondsFromStart;
            args.            Handled = true;
            return;
        }

        Game.GuiStateBackToGame();
    }

    /// <inheritdoc/>
    public override void OnTouchStart(TouchEventArgs e)
    {
        MouseEventArgs args = new();
        args.X = (e.GetX());
        args.        Y = e.GetY();
        OnMouseDown(args);
        e.SetHandled(args.Handled);
    }

    /// <inheritdoc/>
    public override void OnMouseUp(MouseEventArgs args)
    {
        if (Game != null && Game.GuiState != GameState.Inventory)
        {
            return;
        }

        _scrollingUpTimeMs = 0;
        _scrollingDownTimeMs = 0;
    }

    /// <inheritdoc/>
    public override void OnMouseWheelChanged(float args)
    {
        float delta = args;
        bool shiftHeld = Game.KeyboardState[Game.GetKey(Keys.LeftShift)];

        bool inNormalOrOutsideCells = Game.GuiState == GameState.Normal
            || (Game.GuiState == GameState.Inventory && !IsMouseOverCells());

        if (inNormalOrOutsideCells && !shiftHeld)
        {
            Game.ActiveMaterial = (((Game.ActiveMaterial - (int)delta) % 10) + 10) % 10;
        }

        if (IsMouseOverCells() && Game.GuiState == GameState.Inventory)
        {
            if (delta > 0)
            {
                ScrollUp();
            }

            if (delta < 0)
            {
                ScrollDown();
            }
        }
    }

    /// <inheritdoc/>
    public override void OnRender2d(float deltaTime)
    {
        if (inventoryService == null)
        {
            inventoryService = new InventoryService(Game, _blockTypeRegistry);
            controller = ClientInventoryController.Create(Game);
            inventoryUtil = Game.InventoryUtil;
        }

        if (Game.GuiState == GameState.MapLoading)
        {
            return;
        }

        DrawMaterialSelector();

        if (Game.GuiState != GameState.Inventory)
        {
            return;
        }

        AdvanceAutoScroll();

        Point mouse = new(Game.MouseCurrentX, Game.MouseCurrentY);
        Game.Draw2dBitmapFile("inventory.png", InventoryStartX(), InventoryStartY(), 1024, 1024);

        DrawInventoryItems();
        DrawDragDropFeedback(mouse);
        DrawMaterialSelector();
        DrawWearPlaceItems();
        DrawTooltips(mouse);

        if (Game.Inventory.DragDropItem != null)
        {
            DrawItem(mouse.X, mouse.Y, Game.Inventory.DragDropItem, 0, 0);
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> when the mouse cursor is over the inventory
    /// cell grid or its adjacent scroll buttons.
    /// </summary>
    public bool IsMouseOverCells()
        => SelectedCellOrScrollbar(Game.MouseCurrentX, Game.MouseCurrentY);

    /// <summary>Scrolls the inventory grid up by one row, clamped to row 0.</summary>
    public void ScrollUp()
    {
        if (ScrollLine > 0)
        {
            ScrollLine--;
        }
    }

    /// <summary>Scrolls the inventory grid down by one row, clamped to the last page.</summary>
    public void ScrollDown()
    {
        int max = _cellCountTotalY - _cellCountInPageY;
        if (ScrollLine < max)
        {
            ScrollLine++;
        }
    }

    /// <summary>
    /// Draws the material selector bar at the bottom of the screen and highlights
    /// the active material slot.
    /// </summary>
    public void DrawMaterialSelector()
    {
        Game.Draw2dBitmapFile("materials.png",
            MaterialSelectorBgStartX(), MaterialSelectorBgStartY(),
            (int)(1024 * Game.Scale()), (int)(128 * Game.Scale()));

        int startX = MaterialSelectorStartX();
        int startY = MaterialSelectorStartY();
        int cellSize = ActiveMaterialCellSize();

        for (int i = 0; i < 10; i++)
        {
            InventoryItem item = Game.Inventory.RightHand[i];
            if (item != null)
            {
                DrawItem(startX + (i * cellSize), startY, item, cellSize, cellSize);
            }
        }

        Game.Draw2dBitmapFile("activematerial.png",
            startX + (cellSize * Game.ActiveMaterial),
            startY,
            cellSize * 64 / 48,
            cellSize * 64 / 48);
    }

    /// <summary>
    /// Draws a single inventory item at the given screen position.
    /// Block items render as a terrain texture tile with an optional count label.
    /// Other item classes render as a bitmap graphic.
    /// </summary>
    /// <param name="screenposX">Left edge of the drawing area.</param>
    /// <param name="screenposY">Top edge of the drawing area.</param>
    /// <param name="item">Item to draw. No-op when <see langword="null"/>.</param>
    /// <param name="drawsizeX">Override draw width in pixels, or 0 to use cell-sized default.</param>
    /// <param name="drawsizeY">Override draw height in pixels, or 0 to use cell-sized default.</param>
    private void DrawItem(int screenposX, int screenposY, InventoryItem item, int drawsizeX, int drawsizeY)
    {
        if (item == null)
        {
            return;
        }

        int sizex = InventoryService.ItemSizeX(item);
        int sizey = InventoryService.ItemSizeY(item);

        if (drawsizeX is 0 or -1)
        {
            drawsizeX = CellDrawSize * sizex;
            drawsizeY = CellDrawSize * sizey;
        }

        if (item.InventoryItemType == InventoryItemType.Block)
        {
            if (item.BlockId == 0)
            {
                return;
            }

            Game.Draw2dTexture(Game.TerrainTexture, screenposX, screenposY,
                drawsizeX, drawsizeY,
                inventoryService.TextureIdForInventory()[item.BlockId],
                GameConstants.MAX_BLOCKTYPES_SQRT, ColorUtils.ColorFromArgb(255, 255, 255, 255), false);

            if (item.BlockCount > 1)
            {
                Game.Draw2dText(item.BlockCount.ToString(),
                    GameFonts.Default,
                    screenposX, screenposY, null, false);
            }
        }
        else
        {
            Game.Draw2dBitmapFile(InventoryService.ItemGraphics(item), screenposX, screenposY, drawsizeX, drawsizeY);
        }
    }

    /// <summary>
    /// Draws a tooltip popup for <paramref name="item"/> near the given screen position,
    /// repositioning it if it would overflow the screen edges.
    /// </summary>
    public void DrawItemInfo(int screenposX, int screenposY, InventoryItem item)
    {
        int sizex = InventoryService.ItemSizeX(item);
        int sizey = InventoryService.ItemSizeY(item);

        using var paint = new SKPaint
        {
            TextSize = 11.5f,
            Typeface = GameTypeface.Instance,
        };
        SKRect bounds = default;
        paint.MeasureText(inventoryService.ItemInfo(item), ref bounds);
        int tw = (int)MathF.Ceiling(bounds.Width) + 6;
        int th = (int)MathF.Ceiling(bounds.Height) + 4;

        int w = tw + (CellDrawSize * sizex);
        int h = th < (CellDrawSize * sizey) + 4 ? (CellDrawSize * sizey) + 4 : th;

        screenposX = Math.Clamp(screenposX, w + 20, platform.CanvasWidth - (w + 20));
        screenposY = Math.Clamp(screenposY, h + 20, platform.CanvasHeight - (h + 20));

        // Black border, grey fill.
        Game.Draw2dTexture(Game.GetOrCreateWhiteTexture(), screenposX - w, screenposY - h, w, h, null, 0, ColorUtils.ColorFromArgb(255, 0, 0, 0), false);
        Game.Draw2dTexture(Game.GetOrCreateWhiteTexture(), screenposX - w + 2, screenposY - h + 2, w - 4, h - 4, null, 0, ColorUtils.ColorFromArgb(255, 105, 105, 105), false);

        Game.Draw2dText(inventoryService.ItemInfo(item),
            GameFonts.Default,
            screenposX - tw + 4, screenposY - h + 2, null, false);

        DrawItem(screenposX - w + 2, screenposY - h + 2, new InventoryItem { BlockId = item.BlockId }, 0, 0);
    }

    // Private helpers — drawing sub-sections

    /// <summary>Draws all items currently visible in the scrolled inventory grid.</summary>
    private void DrawInventoryItems()
    {
        var ss = string.Join(" - ", Game.Inventory.Items.Select(x => x.Value_.BlockId));

        for (int i = 0; i < Game.Inventory.Items.Length; i++)
        {
            Packet_PositionItem k = Game.Inventory.Items[i];
            if (k == null)
            {
                continue;
            }

            int screenRow = k.Y - ScrollLine;
            if (screenRow >= 0 && screenRow < _cellCountInPageY)
            {
                DrawItem(CellsStartX() + (k.X * CellDrawSize), CellsStartY() + (screenRow * CellDrawSize), k.Value_, 0, 0);
            }
        }
    }

    /// <summary>
    /// Draws a coloured overlay on the cell or wear-place slot under the cursor
    /// while an item is being dragged, indicating whether the drop is valid
    /// (green) or blocked (red).
    /// </summary>
    private void DrawDragDropFeedback(Point mouse)
    {
        if (Game.Inventory.DragDropItem == null)
        {
            return;
        }

        Point? cellInPage = SelectedCell(mouse);
        if (cellInPage != null)
        {
            int sizex = InventoryService.ItemSizeX(Game.Inventory.DragDropItem);
            int sizey = InventoryService.ItemSizeY(Game.Inventory.DragDropItem);

            if (cellInPage.Value.X + sizex <= _cellCountInPageX
             && cellInPage.Value.Y + sizey <= _cellCountInPageY)
            {
                HashSet<Point>? itemsAtArea = inventoryUtil.ItemsAtArea(
                    cellInPage.Value.X, cellInPage.Value.Y + ScrollLine, sizex, sizey);

                int color = (itemsAtArea == null || itemsAtArea.Count > 1)
                    ? ColorUtils.ColorFromArgb(100, 255, 0, 0)   // red — blocked
                    : ColorUtils.ColorFromArgb(100, 0, 255, 0);  // green — free

                Game.Draw2dTexture(Game.GetOrCreateWhiteTexture(),
                    (cellInPage.Value.X * CellDrawSize) + CellsStartX(),
                    (cellInPage.Value.Y * CellDrawSize) + CellsStartY(),
                    CellDrawSize * sizex, CellDrawSize * sizey,
                    null, 0, color, false);
            }
        }

        int? wearSlot = SelectedWearPlace(mouse);
        if (wearSlot != null)
        {
            Point origin = new(_wearPlaceStart[wearSlot.Value].X + InventoryStartX(),
                               _wearPlaceStart[wearSlot.Value].Y + InventoryStartY());
            Point cells = _wearPlaceCells[wearSlot.Value];

            int color = InventoryService.CanWear((WearPlace)wearSlot.Value, Game.Inventory.DragDropItem)
                ? ColorUtils.ColorFromArgb(100, 0, 255, 0)   // green — can equip
                : ColorUtils.ColorFromArgb(100, 255, 0, 0);  // red — cannot equip

            Game.Draw2dTexture(Game.GetOrCreateWhiteTexture(),
                origin.X, origin.Y,
                CellDrawSize * cells.X, CellDrawSize * cells.Y,
                null, 0, color, false);
        }
    }

    /// <summary>Draws the item currently equipped in each wear-place slot.</summary>
    private void DrawWearPlaceItems()
    {
        DrawWearItem(WearPlace.RightHand, Game.Inventory.RightHand[Game.ActiveMaterial]);
        DrawWearItem(WearPlace.MainArmor, Game.Inventory.MainArmor);
        DrawWearItem(WearPlace.Boots, Game.Inventory.Boots);
        DrawWearItem(WearPlace.Helmet, Game.Inventory.Helmet);
        DrawWearItem(WearPlace.Gauntlet, Game.Inventory.Gauntlet);
    }

    /// <summary>Draws a single wear-place item at its configured slot origin.</summary>
    private void DrawWearItem(WearPlace place, InventoryItem item)
    {
        int idx = (int)place;
        DrawItem(_wearPlaceStart[idx].X + InventoryStartX(),
                 _wearPlaceStart[idx].Y + InventoryStartY(),
                 item, 0, 0);
    }

    /// <summary>
    /// Draws item tooltips for whichever cell, wear-place slot, or material-selector
    /// slot is currently under the mouse cursor.
    /// </summary>
    private void DrawTooltips(Point mouse)
    {
        Point? cell = SelectedCell(mouse);
        if (cell != null)
        {
            Point scrolledCell = new(cell.Value.X, cell.Value.Y + ScrollLine);
            Point? itemOrigin = inventoryUtil.ItemAtCell(scrolledCell);
            if (itemOrigin != null)
            {
                InventoryItem item = GetItem(Game.Inventory, itemOrigin.Value.X, itemOrigin.Value.Y);
                if (item != null)
                {
                    DrawItemInfo(mouse.X, mouse.Y, item);
                }
            }
        }

        int? wearSlot = SelectedWearPlace(mouse);
        if (wearSlot != null)
        {
            InventoryItem item = inventoryUtil.ItemAtWearPlace((WearPlace)wearSlot.Value, Game.ActiveMaterial);
            if (item != null)
            {
                DrawItemInfo(mouse.X, mouse.Y, item);
            }
        }

        int? matSlot = SelectedMaterialSelectorSlot(mouse);
        if (matSlot != null)
        {
            InventoryItem item = Game.Inventory.RightHand[matSlot.Value];
            if (item != null)
            {
                DrawItemInfo(mouse.X, mouse.Y, item);
            }
        }
    }

    /// <summary>
    /// Advances the auto-scroll that triggers when the user holds down a scroll button.
    /// Fires every 250 ms while the button is held.
    /// </summary>
    private void AdvanceAutoScroll()
    {
        int now = platform.TimeMillisecondsFromStart;
        if (_scrollingUpTimeMs != 0 && now - _scrollingUpTimeMs > 250)
        {
            _scrollingUpTimeMs = now;
            ScrollUp();
        }

        if (_scrollingDownTimeMs != 0 && now - _scrollingDownTimeMs > 250)
        {
            _scrollingDownTimeMs = now;
            ScrollDown();
        }
    }

    // Private helpers — hit testing

    /// <summary>
    /// Returns the inventory cell under <paramref name="mouse"/>, or
    /// <see langword="null"/> when the cursor is outside the grid.
    /// </summary>
    private Point? SelectedCell(Point mouse)
    {
        if (mouse.X < CellsStartX() || mouse.Y < CellsStartY()
         || mouse.X > CellsStartX() + (_cellCountInPageX * CellDrawSize)
         || mouse.Y > CellsStartY() + (_cellCountInPageY * CellDrawSize))
        {
            return null;
        }

        return new Point((mouse.X - CellsStartX()) / CellDrawSize,
                         (mouse.Y - CellsStartY()) / CellDrawSize);
    }

    /// <summary>
    /// Returns <see langword="true"/> when (<paramref name="mx"/>, <paramref name="my"/>)
    /// is over the cell grid or the adjacent scroll buttons.
    /// </summary>
    private bool SelectedCellOrScrollbar(int mx, int my)
        => mx >= CellsStartX()
        && my >= CellsStartY()
        && mx <= CellsStartX() + ((_cellCountInPageX + 1) * CellDrawSize)
        && my <= CellsStartY() + (_cellCountInPageY * CellDrawSize);

    /// <summary>
    /// Returns the wear-place index under <paramref name="mouse"/>, or
    /// <see langword="null"/> when the cursor is outside all wear-place slots.
    /// </summary>
    private int? SelectedWearPlace(Point mouse)
    {
        for (int i = 0; i < WearPlaceCount; i++)
        {
            int px = _wearPlaceStart[i].X + InventoryStartX();
            int py = _wearPlaceStart[i].Y + InventoryStartY();
            int pw = _wearPlaceCells[i].X * CellDrawSize;
            int ph = _wearPlaceCells[i].Y * CellDrawSize;
            if (HitTest(mouse, px, py, pw, ph))
            {
                return i;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns the material-selector slot index under <paramref name="mouse"/>,
    /// or <see langword="null"/> when the cursor is outside the bar.
    /// </summary>
    private int? SelectedMaterialSelectorSlot(Point mouse)
    {
        int cellSize = ActiveMaterialCellSize();
        int startX = MaterialSelectorStartX();
        int startY = MaterialSelectorStartY();

        if (mouse.X >= startX && mouse.Y >= startY
         && mouse.X < startX + (10 * cellSize)
         && mouse.Y < startY + cellSize)
        {
            return (mouse.X - startX) / cellSize;
        }

        return null;
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="mouse"/> lies within the
    /// rectangle defined by (<paramref name="x"/>, <paramref name="y"/>, <paramref name="w"/>, <paramref name="h"/>).
    /// </summary>
    private static bool HitTest(Point mouse, int x, int y, int w, int h)
        => mouse.X >= x && mouse.Y >= y && mouse.X < x + w && mouse.Y < y + h;

    /// <summary>
    /// Returns the item at the given grid coordinates, or <see langword="null"/>
    /// when no item occupies that position.
    /// </summary>
    private static InventoryItem GetItem(Packet_Inventory inventory, int x, int y)
    {
        for (int i = 0; i < inventory.Items.Length; i++)
        {
            if (inventory.Items[i].X == x && inventory.Items[i].Y == y)
            {
                return inventory.Items[i].Value_;
            }
        }

        return null;
    }
}