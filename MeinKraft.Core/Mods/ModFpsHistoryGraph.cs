using System.Text;

/// <summary>
/// Displays an FPS counter and frame-time history graph.
/// Toggle with F7 or the "fps" command.
/// </summary>
public class ModFpsHistoryGraph : ModBase
{
    private const int MaxCount = 300;
    private const int GraphHeight = 80;
    private const int GraphPosX = 25;
    private const int PerLine = 2;

    private readonly IGameWindowService _platform;
    private readonly IMeshDrawer meshDrawer;

    private readonly float[] dtHistory = new float[MaxCount];
    private readonly Draw2dData[] todraw = new Draw2dData[MaxCount];

    private int lasttitleUpdateMilliseconds;
    private int fpsCount;
    private string fpsText;
    private float longestFrameDt;
    private bool drawFpsText;
    private bool drawFpsGraph;

    public ModFpsHistoryGraph(IGameWindowService platform, IMeshDrawer meshDrawer, IGame game) : base(game)
    {
        _platform = platform;
        this.meshDrawer = meshDrawer;

        for (int i = 0; i < MaxCount; i++)
        {
            todraw[i] = new Draw2dData();
        }
    }

    /// <inheritdoc/>
    public override void OnFrame(float dt)
    {
        UpdateGraph(dt);
        UpdateTitleFps(dt);
        Draw();
    }

    /// <inheritdoc/>
    public override void OnKeyDown(KeyEventArgs args)
    {
        if (args.KeyChar == (int)OpenTK.Windowing.GraphicsLibraryFramework.Keys.F7)
        {
            drawFpsText = !drawFpsGraph;
            drawFpsGraph = !drawFpsGraph;
        }
    }

    /// <inheritdoc/>
    public override bool OnClientCommand(ClientCommandArgs args)
    {
        if (args.Command != "fps")
        {
            return false;
        }

        (drawFpsText, drawFpsGraph) = args.Arguments.Trim() switch
        {
            "" or "1" => (true, false),
            "2" => (true, true),
            _ => (false, false)
        };
        return true;
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>Shifts frame-time history left and appends the latest dt.</summary>
    private void UpdateGraph(float dt)
    {
        Array.Copy(dtHistory, 1, dtHistory, 0, MaxCount - 1);
        dtHistory[MaxCount - 1] = dt;
    }

    /// <summary>Updates the FPS counter and performance info string once per second.</summary>
    private void UpdateTitleFps(float dt)
    {
        fpsCount++;
        longestFrameDt = Math.Max(longestFrameDt, dt);

        int now = _platform.TimeMillisecondsFromStart;
        float elapsed = (now - lasttitleUpdateMilliseconds) / 1000f;
        if (elapsed < 1f)
        {
            return;
        }

        lasttitleUpdateMilliseconds = now;

        int fps = (int)(fpsCount / elapsed);
        int minFps = (int)(1f / longestFrameDt);

        longestFrameDt = 0;
        fpsCount = 0;

        Game.PerformanceInfo["fps"] = $"FPS: {fps} (min: {minFps})";

        StringBuilder sb = new();
        int idx = 0;
        foreach (string value in Game.PerformanceInfo.Values)
        {
            sb.Append(value);
            if (idx % PerLine == 0 && idx != Game.PerformanceInfo.Count - 1)
            {
                sb.Append(", ");
            }
            else if (idx % PerLine != 0)
            {
                sb.Append('\n');
            }

            idx++;
        }

        fpsText = sb.ToString();
    }

    private void Draw()
    {
        if (!drawFpsGraph && !drawFpsText)
        {
            return;
        }

        meshDrawer.OrthoMode(_platform.CanvasWidth, _platform.CanvasHeight);
        if (drawFpsGraph)
        {
            DrawGraph();
        }

        if (drawFpsText)
        {
            Game.Draw2dText(fpsText, GameFonts.Default, 20, 20, null, false);
        }

        meshDrawer.PerspectiveMode();
    }

    private void DrawGraph()
    {
        int posx = GraphPosX;
        int posy = _platform.CanvasHeight - GraphHeight - 20;

        int[] colors =
        [
            ColorUtils.ColorFromArgb(255, 0,   0, 0),
            ColorUtils.ColorFromArgb(255, 255, 0, 0)
        ];

        int whiteTexture = Game.GetOrCreateWhiteTexture();

        for (int i = 0; i < MaxCount; i++)
        {
            float barHeight = dtHistory[i] * 60 * GraphHeight;
            todraw[i].X1 = posx + i;
            todraw[i].Y1 = posy - barHeight;
            todraw[i].Width = 1;
            todraw[i].Height = barHeight;
            todraw[i].InAtlasId = -1;
            todraw[i].Color = ColorUtils.InterpolateColor((float)i / MaxCount, colors, 2);
        }

        Game.Draw2dTextures(todraw, MaxCount, whiteTexture);

        // Reference FPS lines
        int lineColor = ColorUtils.ColorFromArgb(255, 255, 255, 255);
        DrawFpsLine(Game, posy, 30, lineColor);
        DrawFpsLine(Game, posy, 60, lineColor);
        DrawFpsLine(Game, posy, 75, lineColor);
        DrawFpsLine(Game, posy, 150, lineColor);
    }

    /// <summary>Draws a horizontal reference line at the given target FPS level.</summary>
    private void DrawFpsLine(IGame _game, int posy, int fps, int color)
    {
        int whiteTexture = _game.GetOrCreateWhiteTexture();
        float y = posy - (GraphHeight * (60f / fps));

        _game.Draw2dTexture(whiteTexture, GraphPosX, (int)y, MaxCount, 1, -1, GameConstants.MAX_BLOCKTYPES_SQRT, color, false);
        _game.Draw2dText(fps.ToString(), GameFonts.Default, GraphPosX, y, null, false);
    }
}