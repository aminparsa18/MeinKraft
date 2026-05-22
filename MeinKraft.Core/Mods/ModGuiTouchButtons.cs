using OpenTK.Mathematics;

/// <summary>
/// Renders and handles the on-screen touch buttons (menu, inventory, talk, camera)
/// shown during normal gameplay on touch-screen devices.
/// Also manages two virtual joystick touch tracks: one for movement and one for
/// camera rotation.
/// </summary>
public class ModGuiTouchButtons : ModScreen
{
    /// <summary>
    /// <see langword="true"/> after the first touch event is received.
    /// Buttons are hidden until then so they don't appear on non-touch devices.
    /// </summary>
    private bool _touchButtonsEnabled;

    private readonly MenuWidget _buttonMenu;
    private readonly MenuWidget _buttonInventory;
    private readonly MenuWidget _buttonTalk;
    private readonly MenuWidget _buttonCamera;

    /// <summary>
    /// Touch ID of the active movement finger, or <c>-1</c> when no movement
    /// touch is active.
    /// </summary>
    private int _touchIdMove;

    /// <summary>Screen X position where the movement touch began.</summary>
    private int _touchMoveStartX;

    /// <summary>Screen Y position where the movement touch began.</summary>
    private int _touchMoveStartY;

    /// <summary>
    /// Touch ID of the active camera-rotation finger, or <c>-1</c> when no
    /// rotation touch is active.
    /// </summary>
    private int _touchIdRotate;

    /// <summary>Screen X position where the rotation touch began.</summary>
    private int _touchRotateStartX;

    /// <summary>Screen Y position where the rotation touch began.</summary>
    private int _touchRotateStartY;

    private readonly IGame game;
    private readonly IGameWindowService platform;

    /// <summary>Initialises all four touch buttons and assigns them to widget slots 0–3.</summary>
    public ModGuiTouchButtons(IGameWindowService platform, IGame game) : base(platform, game)
    {
        this.game = game;
        this.platform = platform;
        _touchButtonsEnabled = false;
        _touchIdMove = -1;
        _touchIdRotate = -1;

        _buttonMenu = new MenuWidget { Image = "TouchMenu.png" };
        _buttonInventory = new MenuWidget { Image = "TouchInventory.png" };
        _buttonTalk = new MenuWidget { Image = "TouchTalk.png" };
        _buttonCamera = new MenuWidget { Image = "TouchCamera.png" };

        widgets[0] = _buttonMenu;
        widgets[1] = _buttonInventory;
        widgets[2] = _buttonTalk;
        widgets[3] = _buttonCamera;
    }

    /// <inheritdoc/>
    public override void OnRender2d(float deltaTime)
    {
        if (!_touchButtonsEnabled)
        {
            return;
        }

        if (game.GuiState != GameState.Normal)
        {
            return;
        }

        const int buttonSize = 80;
        float scale = Scale();

        // Position each button in a vertical column on the left side of the screen.
        LayoutButton(_buttonMenu, 0, buttonSize, scale);
        LayoutButton(_buttonInventory, 1, buttonSize, scale);
        LayoutButton(_buttonTalk, 2, buttonSize, scale);
        LayoutButton(_buttonCamera, 3, buttonSize, scale);

        if (!platform.IsMousePointerLocked())
        {
            if (game.CameraType is CameraType.Fpp or CameraType.Tpp)
            {
                game.Draw2dText1("Move", platform.CanvasWidth * 5 / 100, platform.CanvasHeight * 85 / 100, (int)(scale * 50), null, false);
                game.Draw2dText1("Look", platform.CanvasWidth * 80 / 100, platform.CanvasHeight * 85 / 100, (int)(scale * 50), null, false);
            }

            DrawWidgets(game);
        }
    }

    /// <inheritdoc/>
    public override void OnButton(MenuWidget w)
    {
        if (w == _buttonMenu)
        {
            game.ShowEscapeMenu();
        }

        if (w == _buttonInventory)
        {
            game.ShowInventory();
        }

        if (w == _buttonCamera)
        {
            game.CameraChange();
        }

        if (w == _buttonTalk)
        {
            if (game.GuiTyping == TypingState.None)
            {
                game.StartTyping();
                platform.ShowKeyboard(true);
            }
            else
            {
                game.StopTyping();
                platform.ShowKeyboard(false);
            }
        }
    }

    /// <inheritdoc/>
    public override void OnTouchStart(TouchEventArgs e)
    {
        // First touch activates the button overlay.
        _touchButtonsEnabled = true;

        // Let the base class handle widget hit-testing via the overridden OnTouchStart.
        base.OnTouchStart(e);
        if (e.GetHandled())
        {
            return;
        }

        bool isLeftSide = e.GetX() <= platform.CanvasWidth / 2;

        if (isLeftSide && _touchIdMove == -1)
        {
            _touchIdMove = e.GetId();
            _touchMoveStartX = e.GetX();
            _touchMoveStartY = e.GetY();
            game.TouchMoveDx = 0;
            game.TouchMoveDy = e.GetY() < platform.CanvasHeight * 50 / 100 ? 1 : 0;
        }

        bool isSecondFinger = _touchIdMove != -1 && e.GetId() != _touchIdMove;
        if ((isSecondFinger || !isLeftSide) && _touchIdRotate == -1)
        {
            _touchIdRotate = e.GetId();
            _touchRotateStartX = e.GetX();
            _touchRotateStartY = e.GetY();
        }
    }

    /// <inheritdoc/>
    public override void OnTouchMove(TouchEventArgs e)
    {
        if (e.GetId() == _touchIdMove)
        {
            game.TouchMoveDx = e.GetX() - _touchMoveStartX;
            game.TouchMoveDy = -(e.GetY() - 1 - _touchMoveStartY);

            if (e.GetY() < platform.CanvasHeight * 50 / 100)
            {
                // Upper half of screen — lock to forward movement only.
                game.TouchMoveDx = 0;
                game.TouchMoveDy = 1;
            }
            else
            {
                float length = new Vector3(game.TouchMoveDx, game.TouchMoveDy, 0).Length;
                if (length > 0)
                {
                    game.TouchMoveDx /= length;
                    game.TouchMoveDy /= length;
                }
            }
        }

        if (e.GetId() == _touchIdRotate)
        {
            float sensitivity = platform.CanvasWidth / 40f;
            game.TouchOrientationDx += (e.GetX() - _touchRotateStartX) / sensitivity;
            game.TouchOrientationDy += (e.GetY() - _touchRotateStartY) / sensitivity;
            _touchRotateStartX = e.GetX();
            _touchRotateStartY = e.GetY();
        }
    }

    /// <inheritdoc/>
    public override void OnTouchEnd(TouchEventArgs e)
    {
        base.OnTouchEnd(e);
        if (e.GetHandled())
        {
            return;
        }

        if (e.GetId() == _touchIdMove)
        {
            _touchIdMove = -1;
            game.TouchMoveDx = 0;
            game.TouchMoveDy = 0;
        }

        if (e.GetId() == _touchIdRotate)
        {
            _touchIdRotate = -1;
            game.TouchOrientationDx = 0;
            game.TouchOrientationDy = 0;
        }
    }

    /// <summary>
    /// Returns the current UI scale factor from the game platform.
    /// </summary>
    private float Scale() => game.Scale();

    /// <summary>
    /// Positions a button in the left-side vertical stack at the given
    /// <paramref name="slot"/> index (0 = top).
    /// </summary>
    /// <param name="button">The widget to position.</param>
    /// <param name="slot">Zero-based vertical slot index.</param>
    /// <param name="buttonSize">Unscaled button size in pixels.</param>
    /// <param name="scale">Current UI scale factor.</param>
    private static void LayoutButton(MenuWidget button, int slot, int buttonSize, float scale)
    {
        button.X = 16 * scale;
        button.Y = (16 + (96 * slot)) * scale;
        button.Sizex = buttonSize * scale;
        button.Sizey = buttonSize * scale;
    }
}