using TextCopy;
using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;

/// <summary>
/// Handles chat display, the typing input buffer, clickable chat links,
/// page scrolling, history navigation, and player-name autocomplete.
/// </summary>
public class ModGuiChat : ModBase
{
    // -------------------------------------------------------------------------
    // Constants
    // -------------------------------------------------------------------------

    /// <summary>Default chat font size in points.</summary>
    private const float DefaultChatFontSize = 11f;

    /// <summary>Number of milliseconds in one second, used for expiry calculations.</summary>
    private const float MsToSeconds = 1f / 1000f;

    /// <summary>
    /// Number of characters stripped from the start of internal player names
    /// before display. Names are stored as "&amp;xNAME" so the first two
    /// characters are the colour-code prefix.
    /// </summary>
    private const int PlayerNamePrefixLength = 2;

    /// <summary>Key character for 't' — opens public chat.</summary>
    private const int CharLowercaseT = 't';

    /// <summary>Key character for 'T' — opens public chat.</summary>
    private const int CharUppercaseT = 'T';

    /// <summary>Key character for 'y' — opens team chat.</summary>
    private const int CharLowercaseY = 'y';

    /// <summary>Key character for 'Y' — opens team chat.</summary>
    private const int CharUppercaseY = 'Y';

    /// <summary>Horizontal left margin of chat lines in unscaled pixels.</summary>
    private const float ChatMarginX = 20f;

    /// <summary>Vertical position of the first chat line in unscaled pixels.</summary>
    private const float ChatBaseY = 90f;

    /// <summary>Vertical spacing between chat lines in unscaled pixels.</summary>
    private const float ChatLineSpacing = 25f;

    /// <summary>Click hit-box width of a chat line in unscaled pixels.</summary>
    private const float ChatLineWidth = 500f;

    /// <summary>Click hit-box height of a chat line in unscaled pixels.</summary>
    private const float ChatLineHeight = 20f;

    // -------------------------------------------------------------------------
    // Public configuration
    // -------------------------------------------------------------------------

    /// <summary>Base font size for chat text, in points (before UI scale is applied).</summary>
    internal float ChatFontSize;

    /// <summary>Number of seconds a chat line remains visible when the chat box is closed.</summary>
    internal int ChatScreenExpireTimeSeconds;

    /// <summary>Maximum number of chat lines drawn at once.</summary>
    internal int ChatLinesMaxToDraw;

    /// <summary>Current page offset for scrolling through chat history (0 = latest).</summary>
    internal int ChatPageScroll;

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------

    private readonly IGameWindowService _platform;
    private readonly Clipboard _clipboard;

    /// <summary>Visible chat lines for the current frame, populated by <see cref="DrawChatLines"/>.</summary>
    private readonly Chatline[] _visibleLines;
    private int _visibleLineCount;

    /// <summary>Scaled font size computed once per frame; used to detect when fonts need rebuilding.</summary>
    private float _currentScaledFontSize;

    /// <summary>Cached bold font used for normal chat lines. Rebuilt only when the scaled size changes.</summary>
    private TextFont _boldFont;

    /// <summary>Cached italic font used for clickable link lines. Rebuilt only when the scaled size changes.</summary>
    private TextFont _italicFont;

    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    /// <summary>
    /// Initialises the chat module with default display settings and pre-allocates
    /// the visible-line buffer.
    /// </summary>
    public ModGuiChat(IGameWindowService platform, IGame game) : base(game)
    {
        _platform = platform;
        ChatFontSize = DefaultChatFontSize;
        ChatScreenExpireTimeSeconds = 20;
        ChatLinesMaxToDraw = 10;
        _visibleLines = new Chatline[1024];
        _clipboard = new Clipboard();
        // Build initial fonts at unscaled size; they will be rebuilt on the
        // first frame once Game.Scale() is known.
        _currentScaledFontSize = DefaultChatFontSize;
        _boldFont = GameFonts.Large;
        _italicFont = GameFonts.Default;
    }

    // -------------------------------------------------------------------------
    // ModBase overrides
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public override void OnRender2d(float deltaTime)
    {
        if (Game.GuiState == GameState.MapLoading)
        {
            return;
        }

        bool typing = Game.GuiTyping == TypingState.Typing;
        DrawChatLines(typing);
        if (typing)
        {
            DrawTypingBuffer(Game);
        }
    }

    /// <inheritdoc/>
    public override void OnMouseDown(MouseEventArgs args)
    {
        float scale = Game.Scale();
        float dx = _platform.IsMousePointerLocked() ? ChatMarginX : ChatMarginX + 100f;
        float startX = dx * scale;
        float sizeX = ChatLineWidth * scale;
        float sizeY = ChatLineHeight * scale;
        float mouseX = args.X;
        float mouseY = args.Y;

        for (int i = 0; i < _visibleLineCount; i++)
        {
            if (!_visibleLines[i].Clickable)
            {
                continue;
            }

            float lineX = startX;
            float lineY = (ChatBaseY + (i * ChatLineSpacing)) * scale;

            if (mouseX > lineX && mouseX < lineX + sizeX
             && mouseY > lineY && mouseY < lineY + sizeY)
            {
                _platform.OpenLinkInBrowser(_visibleLines[i].LinkTarget);
            }
        }
    }

    /// <inheritdoc/>
    public override void OnKeyDown(KeyEventArgs args)
    {
        if (Game.GuiState != GameState.Normal)
        {
            return;
        }

        int eKey = args.KeyChar;

        // Shift+NumPad7 opens chat with a slash prefix (command shortcut).
        if (eKey == Game.GetKey(Keys.KeyPad7)
         && Game.IsShiftPressed
         && Game.GuiTyping == TypingState.None)
        {
            OpenChat(teamChat: false);
            args.Handled = true;
            return;
        }

        // Page up/down scroll through chat history while the chat box is open.
        if (Game.GuiTyping == TypingState.Typing)
        {
            if (eKey == Game.GetKey(Keys.PageUp))
            {
                ChatPageScroll++;
                args.Handled = true;
            }
            else if (eKey == Game.GetKey(Keys.PageDown))
            {
                ChatPageScroll--;
                args.Handled = true;
            }
        }

        ChatPageScroll = Math.Clamp(ChatPageScroll, 0,
            ChatLinesMaxToDraw > 0 ? Game.ChatLinesCount / ChatLinesMaxToDraw : 0);

        // Enter confirms the typed message or opens the chat box if closed.
        if (eKey == Game.GetKey(Keys.Enter) || eKey == Game.GetKey(Keys.KeyPadEnter))
        {
            HandleEnterKey();
            args.Handled = true;
            return;
        }

        if (Game.GuiTyping != TypingState.Typing)
        {
            return;
        }

        // --- Keys active only while typing ---

        if (eKey == Game.GetKey(Keys.Backspace))
        {
            if (Game.GuiTypingBuffer.Length > 0)
            {
                Game.GuiTypingBuffer = Game.GuiTypingBuffer[..^1];
            }

            args.Handled = true;
            return;
        }

        // Ctrl+V — paste from clipboard.
        if (Game.KeyboardStateRaw[Game.GetKey(Keys.LeftControl)]
         || Game.KeyboardStateRaw[Game.GetKey(Keys.RightControl)])
        {
            if (eKey == Game.GetKey(Keys.V) && !string.IsNullOrEmpty(_clipboard.GetText()))
            {
                Game.GuiTypingBuffer += _clipboard.GetText();
            }

            args.Handled = true;
            return;
        }

        // Up/Down — navigate typing history.
        if (eKey == Game.GetKey(Keys.Up))
        {
            Game.TypingLogPos = Math.Max(0, Game.TypingLogPos - 1);
            if (Game.TypingLogPos < Game.TypingLog.Count)
            {
                Game.GuiTypingBuffer = Game.TypingLog[Game.TypingLogPos];
            }

            args.Handled = true;
        }
        else if (eKey == Game.GetKey(Keys.Down))
        {
            Game.TypingLogPos = Math.Min(Game.TypingLog.Count, Game.TypingLogPos + 1);
            Game.GuiTypingBuffer = Game.TypingLogPos < Game.TypingLog.Count
                ? Game.TypingLog[Game.TypingLogPos]
                : "";

            args.Handled = true;
        }

        // Tab — autocomplete the last word with a matching player name.
        if (eKey == Game.GetKey(Keys.Tab)
         && Game.GuiTypingBuffer.Trim() != "")
        {
            HandleTabAutocomplete();
            args.Handled = true;
            return;
        }

        args.Handled = true;
    }

    /// <inheritdoc/>
    public override void OnKeyPress(KeyPressEventArgs args)
    {
        if (Game.GuiState != GameState.Normal)
        {
            return;
        }

        int eKeyChar = args.KeyChar;

        // 't' / 'T' — open public chat.
        if ((eKeyChar == CharLowercaseT || eKeyChar == CharUppercaseT)
         && Game.GuiTyping == TypingState.None)
        {
            OpenChat(teamChat: false);
            return;
        }

        // 'y' / 'Y' — open team chat.
        if ((eKeyChar == CharLowercaseY || eKeyChar == CharUppercaseY)
         && Game.GuiTyping == TypingState.None)
        {
            OpenChat(teamChat: true);
            return;
        }

        // Append printable characters to the typing buffer.
        if (Game.GuiTyping == TypingState.Typing
         && EncodingHelper.IsValidTypingChar(eKeyChar))
        {
            Game.GuiTypingBuffer += (char)eKeyChar;
        }
    }

    // -------------------------------------------------------------------------
    // Drawing
    // -------------------------------------------------------------------------

    /// <summary>
    /// Populates <see cref="_visibleLines"/> from the chat history and draws each
    /// line using bold for normal messages and italic for clickable links.
    /// </summary>
    /// <param name="all">
    /// When <see langword="true"/>, all lines in the current page are shown regardless
    /// of age. When <see langword="false"/>, only lines newer than
    /// <see cref="ChatScreenExpireTimeSeconds"/> are shown.
    /// </param>
    public void DrawChatLines(bool all)
    {
        _visibleLineCount = 0;

        int timeNow = _platform.TimeMillisecondsFromStart;
        int scroll = all ? ChatPageScroll : 0;

        int first = Math.Max(0, Game.ChatLinesCount - (ChatLinesMaxToDraw * (scroll + 1)));
        int count = Math.Min(Game.ChatLinesCount, ChatLinesMaxToDraw);

        for (int i = first; i < first + count; i++)
        {
            Chatline c = Game.ChatLines[i];
            bool fresh = (timeNow - c.TimeMilliseconds) * MsToSeconds < ChatScreenExpireTimeSeconds;
            if (all || fresh)
            {
                _visibleLines[_visibleLineCount++] = c;
            }
        }

        // Rebuild cached fonts only when the scaled size has changed.
        float scale = Game.Scale();
        float scaledFontSize = ChatFontSize * scale;
        if (scaledFontSize != _currentScaledFontSize)
        {
            _currentScaledFontSize = scaledFontSize;
            _boldFont = GameFonts.Large;
            _italicFont = GameFonts.Default;
        }

        float baseX = ChatMarginX * scale;

        for (int i = 0; i < _visibleLineCount; i++)
        {
            var lineFont = _visibleLines[i].Clickable ? _italicFont : _boldFont;
            float y = (ChatBaseY + (i * ChatLineSpacing)) * scale;
            Game.Draw2dText(_visibleLines[i].Text, lineFont, baseX, y, null, false);
        }

        if (ChatPageScroll != 0)
        {
            float pageY = (ChatBaseY - ChatLineSpacing) * scale;
            Game.Draw2dText($"&7Page: {ChatPageScroll}", _boldFont, baseX, pageY, null, false);
        }
    }

    /// <summary>
    /// Draws the current contents of the typing buffer at the bottom of the screen,
    /// appending a blinking-cursor underscore.
    /// </summary>
    public void DrawTypingBuffer(IGame game)
    {
        float scaledSize = ChatFontSize * game.Scale();
        if (scaledSize != _currentScaledFontSize)
        {
            _currentScaledFontSize = scaledSize;
            _boldFont = GameFonts.Default;
            _italicFont = GameFonts.Default;
        }

        string prefix = game.IsTeamchat ? "To team: " : "";
        string display = $"{prefix}{game.GuiTypingBuffer}_";

        float x = 50f * game.Scale();
        float y = _platform.IsSmallScreen()
            ? (_platform.CanvasHeight / 2f) - (100f * game.Scale())
            : _platform.CanvasHeight - (100f * game.Scale());

        game.Draw2dText(display, _boldFont, x, y, null, true);
    }

    // -------------------------------------------------------------------------
    // Autocomplete
    // -------------------------------------------------------------------------

    /// <summary>
    /// Finds the first entity name that starts with <paramref name="text"/>
    /// (case-insensitive) and is marked as auto-completable.
    /// </summary>
    /// <param name="text">The partial name typed by the player.</param>
    /// <returns>
    /// The full player name (without the internal colour-code prefix) if a match
    /// is found; otherwise an empty string.
    /// </returns>
    public string DoAutocomplete(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "";
        }

        for (int i = 0; i < Game.Entities.Count; i++)
        {
            DrawName drawName = Game.Entities[i]?.DrawName;
            if (drawName == null || !drawName.ClientAutoComplete)
            {
                continue;
            }

            // Strip the "&x" colour-code prefix once and reuse the slice.
            string displayName = drawName.Name[PlayerNamePrefixLength..];
            if (displayName.StartsWith(text, StringComparison.InvariantCultureIgnoreCase))
            {
                return displayName;
            }
        }

        return "";
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Opens the chat typing buffer, optionally targeting the team channel.
    /// </summary>
    /// <param name="teamChat">
    /// <see langword="true"/> to open team chat; <see langword="false"/> for public chat.
    /// </param>
    private void OpenChat(bool teamChat)
    {
        Game.GuiTyping = TypingState.Typing;
        Game.IsTyping = true;
        Game.GuiTypingBuffer = "";
        Game.IsTeamchat = teamChat;
    }

    /// <summary>
    /// Handles the Enter key: submits the current buffer when typing,
    /// or opens the chat box when idle.
    /// </summary>
    private void HandleEnterKey()
    {
        if (Game.GuiTyping == TypingState.Typing)
        {
            Game.TypingLog.Add(Game.GuiTypingBuffer);
            Game.TypingLogPos = Game.TypingLog.Count;
            Game.ExecuteChat(Game.GuiTypingBuffer);

            Game.GuiTypingBuffer = "";
            Game.IsTyping = false;
            Game.GuiTyping = TypingState.None;
            _platform.ShowKeyboard(false);
        }
        else if (Game.GuiTyping == TypingState.None)
        {
            Game.StartTyping();
        }
    }

    /// <summary>
    /// Attempts to autocomplete the last word in the typing buffer with a
    /// matching player name. Formats the result as "Name: " when it is the
    /// first word, or appends a trailing space otherwise.
    /// </summary>
    private void HandleTabAutocomplete()
    {
        string[] parts = Game.GuiTypingBuffer.Split(' ');
        string lastWord = parts[^1];
        string completed = DoAutocomplete(lastWord);

        if (completed == "")
        {
            return;
        }

        if (parts.Length == 1)
        {
            Game.GuiTypingBuffer = completed + ": ";
        }
        else
        {
            parts[^1] = completed;
            Game.GuiTypingBuffer = string.Join(' ', parts) + " ";
        }
    }
}

// =============================================================================

/// <summary>
/// Represents a single line in the chat history, optionally carrying a
/// clickable hyperlink.
/// </summary>
public class Chatline
{
    /// <summary>The formatted text displayed in the chat window.</summary>
    internal string Text { get; set; }

    /// <summary>
    /// The platform timestamp (in milliseconds) at which this line was added,
    /// used to expire old messages from the passive display.
    /// </summary>
    internal int TimeMilliseconds { get; set; }

    /// <summary>
    /// When <see langword="true"/>, clicking this line opens <see cref="LinkTarget"/>
    /// in the system browser.
    /// </summary>
    internal bool Clickable { get; set; }

    /// <summary>
    /// The URL opened when the player clicks this line.
    /// Only meaningful when <see cref="Clickable"/> is <see langword="true"/>.
    /// </summary>
    internal string LinkTarget { get; set; }

    /// <summary>Creates a non-clickable chat line with the given text and timestamp.</summary>
    internal static Chatline Create(string text, int timeMilliseconds) => new()
    {
        Text = text,
        TimeMilliseconds = timeMilliseconds,
        Clickable = false,
    };

    /// <summary>
    /// Creates a clickable chat line that opens <paramref name="linkTarget"/>
    /// in the system browser when clicked.
    /// </summary>
    internal static Chatline CreateClickable(string text, int timeMilliseconds, string linkTarget) => new()
    {
        Text = text,
        TimeMilliseconds = timeMilliseconds,
        Clickable = true,
        LinkTarget = linkTarget,
    };
}