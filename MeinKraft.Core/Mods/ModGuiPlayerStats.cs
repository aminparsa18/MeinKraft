/// <summary>
/// Renders the player's health and oxygen bars on the HUD.
/// </summary>
public class ModGuiPlayerStats : ModBase
{
    private const int BarWidth = 220;
    private const int BarHeight = 32;
    private const int CenterOffset = 20;

    private static readonly int White = ColorUtils.ColorFromArgb(255, 255, 255, 255);
    private static readonly int Red = ColorUtils.ColorFromArgb(255, 255, 0, 0);
    private static readonly int Blue = ColorUtils.ColorFromArgb(255, 0, 0, 255);

    private readonly IGameWindowService _gameService;

    public ModGuiPlayerStats(IGameWindowService platform, IGame game) : base(game)
    {
        this._gameService = platform;
    }

    public override void OnRender2d(float deltaTime)
    {
        if (Game.GuiState == GameState.MapLoading || Game.PlayerStats == null)
        {
            return;
        }

        int barY = _gameService.CanvasHeight - 122;
        int healthX = (_gameService.CanvasWidth / 2) - BarWidth - CenterOffset;
        int oxygenX = (_gameService.CanvasWidth / 2) + CenterOffset;

        DrawBar(healthX, barY, (float)Game.PlayerStats.CurrentHealth / Game.PlayerStats.MaxHealth, Red);

        if (Game.PlayerStats.CurrentOxygen < Game.PlayerStats.MaxOxygen)
        {
            DrawBar(oxygenX, barY, (float)Game.PlayerStats.CurrentOxygen / Game.PlayerStats.MaxOxygen, Blue);
        }
    }

    /// <summary>Draws a background + filled progress bar at the given position.</summary>
    private void DrawBar(int x, int y, float progress, int color)
    {
        int bgTex = Game.GetTexture("ui_bar_background.png");
        int barTex = Game.GetTexture("ui_bar_inner.png");

        Game.Draw2dTexture(bgTex, x, y, BarWidth, BarHeight, null, 0, White, false);
        Game.Draw2dTexturePart(barTex, progress, 1, x, y, progress * BarWidth, BarHeight, color, false);
    }
}