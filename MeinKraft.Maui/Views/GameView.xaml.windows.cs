#if WINDOWS

using MeinKraft.Maui.Services;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Application = Microsoft.Maui.Controls.Application;

namespace MeinKraft.Maui.Views;

public partial class GameView : ContentPage
{
    public GameView()
    {
        OverlayMenu.FullscreenChanged += OnFullscreenChanged;
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        AttachWindowKeyEvents();
        ((MauiGameWindowService)_gameWindowService).CaptureCursor();
    }

    private void AttachWindowKeyEvents()
    {
        Microsoft.Maui.Controls.Window? mauiWindow = Application.Current?.Windows.FirstOrDefault();
        Microsoft.UI.Xaml.Window? nativeWindow = mauiWindow?.Handler?.PlatformView
                           as Microsoft.UI.Xaml.Window;

        if (nativeWindow?.Content is UIElement root)
        {
            root.AddHandler(
                UIElement.KeyDownEvent,
                new KeyEventHandler((s, args) =>
                {
                    KeyEventArgs keyEvent = WinKeyMapper.ToKeyEventArgs(args);
                    if (keyEvent.KeyChar == (int)Keys.Escape && _game.GuiState == GameState.Normal && !OverlayMenu.IsVisible)
                    {
                        ((MauiGameWindowService)_gameWindowService).ReleaseCursor();
                        ShowPauseMenu();
                    }
                    else if (keyEvent.KeyChar == (int)Keys.Escape && _game.GuiState == GameState.Inventory)
                    {
                        ((MauiGameWindowService)_gameWindowService).CaptureCursor();
                    }
                    else if (keyEvent.KeyChar == (int)Keys.B && _game.GuiState == GameState.Normal)
                    {
                        ((MauiGameWindowService)_gameWindowService).ReleaseCursor();
                    }
                    else if (keyEvent.KeyChar == (int)Keys.Escape && OverlayMenu.IsVisible)
                    {
                        HideOverlay();
                        ((MauiGameWindowService)_gameWindowService).CaptureCursor();
                    }
                    _game.KeyDown(keyEvent);
                    _game.KeyPress(keyEvent);
                    args.Handled = keyEvent.Handled;
                }),
                handledEventsToo: true
            );

            root.AddHandler(
                UIElement.KeyUpEvent,
                new KeyEventHandler((s, args) =>
                {
                    KeyEventArgs keyEvent = WinKeyMapper.ToKeyEventArgs(args);
                    _game.KeyUp(keyEvent);
                    args.Handled = keyEvent.Handled;
                }),
                handledEventsToo: true
            );

            // Must set these BEFORE trying to focus
            if (root is Microsoft.UI.Xaml.Controls.Control control)
            {
                control.IsTabStop = true;
                control.AllowFocusOnInteraction = true;
            }

            root.Tapped += (s, _) => root.Focus(FocusState.Pointer);  // focus on tap
            root.Focus(FocusState.Programmatic);                       // focus immediately

            root.AddHandler(UIElement.PointerPressedEvent,
                new PointerEventHandler((s, args) =>
                {
                    UIElement? glNative = GlView.Handler?.PlatformView as UIElement;
                    PointerPoint pt = args.GetCurrentPoint(glNative);
                    _game.MouseDown(WinMouseMapper.ToMouseDownEventArgs(pt));
                }),
                handledEventsToo: true);

            root.AddHandler(UIElement.PointerReleasedEvent,
                new PointerEventHandler((s, args) =>
                {
                    UIElement? glNative = GlView.Handler?.PlatformView as UIElement;
                    PointerPoint pt = args.GetCurrentPoint(glNative);
                    _game.MouseUp(WinMouseMapper.ToMouseUpEventArgs(pt));
                }),
                handledEventsToo: true);

            root.AddHandler(UIElement.PointerWheelChangedEvent,
                new PointerEventHandler((s, args) =>
                {
                    UIElement? glNative = GlView.Handler?.PlatformView as UIElement;
                    PointerPoint pt = args.GetCurrentPoint(glNative);
                    _game.MouseWheelChanged(WinMouseMapper.ToMouseWheelEventArgs(pt));
                }),
                handledEventsToo: true);
        }
    }

    /// <summary>
    /// Uses the AppWindow / OverlappedPresenter API — the only reliable way to
    /// toggle borderless fullscreen in a MAUI WinUI3 app.
    /// </summary>
    private void OnFullscreenChanged(object? sender, bool fullscreen)
    {
        MauiWinUIWindow? window = GetParentWindow().Handler.PlatformView as MauiWinUIWindow;
        AppWindow appWindow = GetAppWindow(window);

        if (fullscreen)
        {
            appWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
        }
        else
        {
            appWindow.SetPresenter(AppWindowPresenterKind.Default);
        }
    }

    private static AppWindow GetAppWindow(MauiWinUIWindow? window)
    {
        var handle = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WindowId id = Win32Interop.GetWindowIdFromWindow(handle);
        return AppWindow.GetFromWindowId(id);
    }

    private void OnRawMouseDelta(int dx, int dy)
    {
        MouseEventArgs emulated = new MouseEventArgs
        {
            MovementX = dx,
            MovementY = dy,
            Emulated = true
        };
        _game.MouseMove(emulated);
    }

    public static class WinMouseMapper
    {
        public static MouseEventArgs ToMouseDownEventArgs(PointerPoint point)
        {
            return new MouseEventArgs
            {
                X = (int)point.Position.X,
                Y = (int)point.Position.Y,
                Button = MapPressedButton(point.Properties)
            };
        }

        public static MouseEventArgs ToMouseUpEventArgs(PointerPoint point)
        {
            return new MouseEventArgs
            {
                X = (int)point.Position.X,
                Y = (int)point.Position.Y,
                Button = MapReleasedButton(point.Properties)
            };
        }

        public static float ToMouseWheelEventArgs(PointerPoint point)
        {
            return point.Properties.IsHorizontalMouseWheel
                ? 0f
                : point.Properties.MouseWheelDelta / 120f;
        }

        private static int MapPressedButton(PointerPointProperties props)
        {
            if (props.IsLeftButtonPressed) return (int)MouseButton.Left;
            if (props.IsRightButtonPressed) return (int)MouseButton.Right;
            if (props.IsMiddleButtonPressed) return (int)MouseButton.Middle;
            if (props.IsXButton1Pressed) return (int)MouseButton.Button4;
            if (props.IsXButton2Pressed) return (int)MouseButton.Button5;
            return -1;
        }

        private static int MapReleasedButton(PointerPointProperties props)
        {
            return props.PointerUpdateKind switch
            {
                PointerUpdateKind.LeftButtonReleased => (int)MouseButton.Left,
                PointerUpdateKind.RightButtonReleased => (int)MouseButton.Right,
                PointerUpdateKind.MiddleButtonReleased => (int)MouseButton.Middle,
                PointerUpdateKind.XButton1Released => (int)MouseButton.Button4,
                PointerUpdateKind.XButton2Released => (int)MouseButton.Button5,
                _ => -1
            };
        }
    }
}
#endif