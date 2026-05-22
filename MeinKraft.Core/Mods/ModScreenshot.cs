/// <summary>
/// Handles screenshot capture on F12 and displays a brief flash overlay after taking one.
/// </summary>
public class ModScreenshot : ModBase
{
    private const int FlashFrames = 5;
    private const int FlashFontSize = 50;
    private const string ScreenshotText = "&0Screenshot";

    private static readonly int White = ColorUtils.ColorFromArgb(255, 255, 255, 255);

    private bool takeScreenshot;
    private int screenshotFlashFramesLeft;

    private readonly IGameWindowService platform;

    public ModScreenshot(IGameWindowService platform, IGame game) : base(game)
    {
        this.platform = platform;
    }

    public override void OnRender2d(float deltaTime)
    {
        if (takeScreenshot)
        {
            takeScreenshot = false;
            platform.SaveScreenshot(); // Must be done after rendering, before SwapBuffers
            screenshotFlashFramesLeft = FlashFrames;
        }

        if (screenshotFlashFramesLeft > 0)
        {
            DrawScreenshotFlash();
            screenshotFlashFramesLeft--;
        }
    }

    public override void OnKeyDown(KeyEventArgs args)
    {
        if (args.KeyChar != Game.GetKey(OpenTK.Windowing.GraphicsLibraryFramework.Keys.F12))
        {
            return;
        }

        takeScreenshot = true;
        args.Handled = true;
    }

    internal void DrawScreenshotFlash()
    {
        Game.Draw2dTexture(Game.GetOrCreateWhiteTexture(), 0, 0, platform.CanvasWidth, platform.CanvasHeight, null, 0, White, false);
        var (textWidth, textHeight) = GameTypeface.Measure(ScreenshotText, FlashFontSize);
        Game.Draw2dText(ScreenshotText, GameFonts.Large, Game.Xcenter(textWidth), Game.Ycenter(textHeight), null, false);
    }
}