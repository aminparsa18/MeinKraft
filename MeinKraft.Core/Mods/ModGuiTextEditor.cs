using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;

/// <summary>
/// A simple in-game text editor overlay. Toggle visibility with F9, close with Escape.
/// </summary>
public class ModGuiTextEditor : ModScreen
{
    private const int MaxLines = 128;
    private const int MaxColumns = 80;
    private const int StartX = 100;
    private const int StartY = 100;
    private const int CharSize = 12;
    private const int CharCursor = 95; // '_'
    private const int CharSpace = 32;

    private static readonly int BackgroundColor = ColorUtils.ColorFromArgb(255, 100, 100, 100);

    private readonly int[][] buffer = new int[MaxLines][];
    private int cursorColumn;
    private int cursorLine;
    private bool visible;

    public ModGuiTextEditor(IGameWindowService gameService, IGame game) : base(gameService, game)
    {
        for (int i = 0; i < MaxLines; i++)
        {
            buffer[i] = new int[MaxColumns];
        }
    }

    public override void OnRender2d(float deltaTime)
    {
        if (!visible)
        {
            return;
        }

        Game.Draw2dTexture(Game.GetOrCreateWhiteTexture(), StartX, StartY, MaxColumns * CharSize, MaxLines * CharSize, null, 0, BackgroundColor, false);

        for (int i = 0; i < MaxLines; i++)
        {
            Game.Draw2dText(LineToString(buffer[i]), GameFonts.Default, StartX, StartY + (CharSize * i), null, false);
        }

        // Draw cursor on current line
        int[] spaces = new int[MaxColumns];
        Array.Fill(spaces, CharSpace);
        spaces[cursorColumn] = CharCursor;
        string cursorRow = EncodingHelper.CharArrayToString(spaces, cursorColumn + 1);
        Game.Draw2dText(cursorRow, GameFonts.Default, StartX, StartY + (cursorLine * CharSize), null, false);
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        int key = e.KeyChar;

        if (key == Game.GetKey(Keys.F9))
        {
            visible = !visible;
            return;
        }

        if (!visible)
        {
            return;
        }

        switch (key)
        {
            case (int)Keys.Escape: visible = false; break;
            case (int)Keys.Left: cursorColumn--; break;
            case (int)Keys.Right: cursorColumn++; break;
            case (int)Keys.Up: cursorLine--; break;
            case (int)Keys.Down: cursorLine++; break;
            case (int)Keys.Backspace: cursorColumn--; break;
        }

        cursorColumn = Math.Clamp(cursorColumn, 0, Math.Min(MaxColumns - 1, LineLength(buffer[cursorLine])));
        cursorLine = Math.Clamp(cursorLine, 0, MaxLines - 1);

        if (key == (int)Keys.Delete)
        {
            // Shift characters left from cursor position
            for (int i = cursorColumn; i < MaxColumns - 1; i++)
            {
                buffer[cursorLine][i] = buffer[cursorLine][i + 1];
            }

            buffer[cursorLine][MaxColumns - 1] = 0;
        }

        e.Handled = true;
    }

    public override void OnKeyPress(KeyPressEventArgs e)
    {
        if (!visible)
        {
            return;
        }

        if (e.KeyChar == 8)
        {
            return; // backspace handled in OnKeyDown
        }

        // Shift characters right to make room
        for (int i = MaxColumns - 1; i > cursorColumn; i--)
        {
            buffer[cursorLine][i] = buffer[cursorLine][i - 1];
        }

        buffer[cursorLine][cursorColumn] = e.KeyChar;
        cursorColumn++;
        e.Handled = true;
    }

    private static string LineToString(int[] line)
        => line == null ? "" : EncodingHelper.CharArrayToString(line, LineLength(line));

    private static int LineLength(int[] line)
    {
        for (int i = 0; i < MaxColumns; i++)
        {
            if (line[i] == 0)
            {
                return i;
            }
        }

        return MaxColumns;
    }
}