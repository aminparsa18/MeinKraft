using OpenTK.Windowing.Common;
using TextCopy;

/// <summary>
/// Base class for all in-game menu screens. Manages a fixed-size pool of
/// <see cref="MenuWidget"/> objects and routes input events (keyboard, mouse,
/// touch) to them. Derive from this class and override <see cref="OnButton"/>
/// to respond to widget interactions.
/// </summary>
public class ModScreen : ModBase
{
    /// <summary>Reference to the current game instance.</summary>
    private readonly IGameWindowService platform;

    /// <summary>Maximum number of widgets this screen can hold.</summary>
    internal int WidgetCount;

    /// <summary>Widget pool. Entries are <see langword="null"/> when unused.</summary>
    internal MenuWidget[] widgets;

    /// <summary>Screen-space X origin added to all widget positions during drawing and hit-testing.</summary>
    internal int screenx;

    /// <summary>Screen-space Y origin added to all widget positions during drawing and hit-testing.</summary>
    internal int screeny;

    /// <summary>Initialises the widget pool with a capacity of 64.</summary>
    public ModScreen(IGameWindowService platform, IGame game) : base(game)
    {
        this.platform = platform;
        WidgetCount = 64;
        widgets = new MenuWidget[WidgetCount];
    }

    public void SetGame(IGame game)
    {
    }

    /// <inheritdoc/>
    public override void OnKeyPress(KeyPressEventArgs args) => KeyPress(args);

    /// <inheritdoc/>
    public override void OnTouchStart(TouchEventArgs e)
        => e.SetHandled(MouseDown(e.GetX(), e.GetY()));

    /// <inheritdoc/>
    public override void OnTouchEnd(TouchEventArgs e) => MouseUp(e.GetX(), e.GetY());

    /// <inheritdoc/>
    public override void OnMouseDown(MouseEventArgs args) => MouseDown(args.X, args.Y);

    /// <inheritdoc/>
    public override void OnMouseUp(MouseEventArgs args) => MouseUp(args.X, args.Y);

    /// <inheritdoc/>
    public override void OnMouseMove(MouseEventArgs args) => MouseMove(args);

    /// <summary>Called when the hardware back button is pressed. Override to handle navigation.</summary>
    public virtual void OnBackPressed() { }

    /// <summary>Called when the mouse wheel is scrolled. Override to handle scrolling.</summary>
    public virtual void OnMouseWheel(MouseWheelEventArgs e) { }

    /// <summary>
    /// Called when a button widget is clicked (mouse-up inside its bounds).
    /// Override to respond to button presses.
    /// </summary>
    /// <param name="w">The widget that was clicked.</param>
    public virtual void OnButton(MenuWidget w) { }

    /// <summary>
    /// Handles keyboard character input, routing it to whichever text-box widget
    /// is currently in editing mode. Supports backspace, tab/enter suppression,
    /// paste (Ctrl+V / key code 22), and normal printable characters.
    /// </summary>
    private void KeyPress(KeyPressEventArgs e)
    {
        for (int i = 0; i < WidgetCount; i++)
        {
            MenuWidget w = widgets[i];
            if (w == null || w.Type != UIWidgetType.Textbox || !w.Editing)
            {
                continue;
            }

            int key = e.KeyChar;

            if (key == 8) // backspace
            {
                if (w.Text.Length > 0)
                {
                    w.Text = w.Text[..^1];
                }

                return;
            }

            if (key is 9 or 13) // tab, enter
            {
                return;
            }

            if (key == 22) // paste (Ctrl+V)
            {
                var clipboard = new Clipboard();
                if (!string.IsNullOrEmpty(clipboard.GetText()))
                {
                    w.Text = string.Concat(w.Text, clipboard.GetText());
                }

                return;
            }

            if (EncodingHelper.IsValidTypingChar(key))
            {
                w.Text = string.Concat(w.Text, ((char)key).ToString());
            }
        }
    }

    /// <summary>
    /// Handles a mouse or touch press at (<paramref name="x"/>, <paramref name="y"/>).
    /// Updates the <c>pressed</c> and <c>editing</c> state of all widgets and
    /// shows or hides the soft keyboard as needed.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if any widget consumed the event.
    /// </returns>
    private bool MouseDown(int x, int y)
    {
        bool handled = false;
        bool editingChange = false;

        for (int i = 0; i < WidgetCount; i++)
        {
            MenuWidget w = widgets[i];
            if (w == null)
            {
                continue;
            }

            bool hit = VectorUtils.PointInRect(x, y, screenx + w.X, screeny + w.Y, w.Sizex, w.Sizey);

            if (w.Type == UIWidgetType.Button)
            {
                w.Pressed = hit;
                if (hit)
                {
                    handled = true;
                }
            }

            if (w.Type == UIWidgetType.Textbox)
            {
                w.Pressed = hit;
                if (hit)
                {
                    handled = true;
                }

                bool wasEditing = w.Editing;
                w.Editing = hit;

                if (w.Editing && !wasEditing)
                {
                    platform.ShowKeyboard(true);
                    editingChange = true;
                }

                if (!w.Editing && wasEditing && !editingChange)
                {
                    platform.ShowKeyboard(false);
                }
            }
        }

        return handled;
    }

    /// <summary>
    /// Handles a mouse or touch release at (<paramref name="x"/>, <paramref name="y"/>).
    /// Clears all pressed states and fires <see cref="OnButton"/> for any button
    /// whose bounds contain the release point.
    /// </summary>
    private void MouseUp(int x, int y)
    {
        for (int i = 0; i < WidgetCount; i++)
        {
            MenuWidget w = widgets[i];
            if (w != null)
            {
                w.Pressed = false;
            }
        }

        for (int i = 0; i < WidgetCount; i++)
        {
            MenuWidget w = widgets[i];
            if (w == null || w.Type != UIWidgetType.Button)
            {
                continue;
            }

            if (VectorUtils.PointInRect(x, y, screenx + w.X, screeny + w.Y, w.Sizex, w.Sizey))
            {
                OnButton(w);
            }
        }
    }

    /// <summary>
    /// Updates the <c>hover</c> state of all widgets based on the current mouse position.
    /// Skips emulated move events unless <c>ForceUsage</c> is set.
    /// </summary>
    private void MouseMove(MouseEventArgs e)
    {
        if (e.Emulated && !e.ForceUsage)
        {
            return;
        }

        for (int i = 0; i < WidgetCount; i++)
        {
            MenuWidget w = widgets[i];
            w?.Hover = VectorUtils.PointInRect(e.X, e.Y, screenx + w.X, screeny + w.Y, w.Sizex, w.Sizey);
        }
    }

    /// <summary>
    /// Draws all visible widgets. Buttons render as images or coloured rectangles
    /// with centred text; text boxes render their text (masked with asterisks for
    /// password fields) with a cursor appended while editing; labels render plain text.
    /// </summary>
    public void DrawWidgets(IGame game)
    {
        for (int i = 0; i < WidgetCount; i++)
        {
            MenuWidget w = widgets[i];
            if (w == null || !w.Visible)
            {
                continue;
            }

            string text = w.Text;
            if (w.Selected)
            {
                text = string.Concat(platform, "&2", text);
            }

            if (w.Type == UIWidgetType.Button)
            {
                if (w.ButtonStyle != ButtonStyle.Text)
                {
                    if (w.Image != null)
                    {
                        game.Draw2dBitmapFile(w.Image, screenx + w.X, screeny + w.Y, w.Sizex, w.Sizey);
                    }
                    else
                    {
                        game.Draw2dTexture(game.GetOrCreateWhiteTexture(), screenx + w.X, screeny + w.Y, w.Sizex, w.Sizey, null, 0, w.Color, false);
                    }

                    game.Draw2dText1(text, screenx + (int)w.X, screeny + (int)(w.Y + (w.Sizey / 2)), (int)w.FontSize, null, false);
                }
                // ButtonStyle.Text rendering is not yet implemented.
            }

            if (w.Type == UIWidgetType.Textbox)
            {
                if (w.Password)
                {
                    text = new string('*', w.Text.Length);
                }

                if (w.Editing)
                {
                    text = string.Concat(platform, text, "_");
                }

                game.Draw2dText(text, w.Font, screenx + w.X, screeny + w.Y, null, false);
            }

            if (w.Type == UIWidgetType.Label)
            {
                game.Draw2dText(text, w.Font, screenx + w.X, screeny + w.Y, ColorUtils.ColorFromArgb(255, 0, 0, 0), false);
            }
        }
    }
}