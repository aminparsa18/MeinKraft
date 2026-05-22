using MeinKraft.Maui.Services;
using MeinKraft.Worker;
using OpenTK.Graphics.ES30;
using SkiaSharp.Views.Maui;
using MessagePipe;

namespace MeinKraft.Maui.Views;

public partial class GameView : ContentPage, IDisposable
{
    private bool _glInitialized = false;
    private IDispatcherTimer _gameLoopTimer;
    private DateTime _lastFrame = DateTime.UtcNow;

    private readonly IGame _game;
    private readonly IGameLogger _gameLogger;
    private readonly IOpenGlService _openGlService;
    private readonly IGameWindowService _gameWindowService;
    private readonly IAssetManager _assetManager;
    private readonly IDisposable _subscription;

    private readonly ClientWorkerHost _workerHost;

    public GameView(IOpenGlService openGlService, IGameWindowService gameWindowService, IAssetManager assetManager,
        IGameLogger gameLogger, IGame game, ITerrainChunkTesselator terrainChunkTesselator, ClientWorkerHost workerHost,
        ISubscriber<SetupProgressEventArgs> subscriber)
    {
        InitializeComponent();
        _openGlService = openGlService;
        _gameWindowService = gameWindowService;
        _assetManager = assetManager;
        _game = game;
        _gameLogger = gameLogger;
        _workerHost = workerHost;

        var bag = DisposableBag.CreateBuilder();
        subscriber.Subscribe(SetupProgressUpdated).AddTo(bag);
        _subscription = bag.Build();

        // Inject game services into the overlay so it can apply options directly.
        // Must happen after InitializeComponent() so OverlayMenu is already created.
        OverlayMenu.Initialize(_game, terrainChunkTesselator);

        // Wire up overlay exit events. OverlayMenuView owns its internal navigation
        // (Pause ↔ Options); GameView only handles the two exit points that
        // require cursor and game-state changes, plus the platform fullscreen call.
        OverlayMenu.ReturnToGameRequested += (_, _) => HideOverlay();
        OverlayMenu.ExitToMenuRequested += OnExitToMenuRequested;
    }

    private void SetupProgressUpdated(SetupProgressEventArgs e)
    {
        if (e.Progress == 100)
        {
            ProgressView.IsVisible = false;
            return;
        }
        ProgressView.UpdateProgress(e);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        _gameLoopTimer = Dispatcher.CreateTimer();
        _gameLoopTimer.Interval = TimeSpan.FromMilliseconds(8); // ~60 fps
        _gameLoopTimer.Tick += (_, _) =>
        {
            GlView.InvalidateSurface();
#if WINDOWS
            if (_gameWindowService.Focused() && _game.GuiState == GameState.Normal)
            {
                ((MauiGameWindowService)_gameWindowService).TrapCursorInCenter();
            }
#endif
        };
        _gameLoopTimer.Start();
        ProgressView.UpdateProgress(new() { Title = "Loading Assets...", Progress = 0 });
        await _assetManager.LoadAssetsAsync();
        try
        {
            await Connect();
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(() => throw ex);
            return;
        }

        ProgressView.UpdateProgress(new() { Title = "Attaching OpenGl Surface...", Progress = 0 });
        ((MauiGameWindowService)_gameWindowService).Attach(GlView);

        GlView.PaintSurface += GlView_PaintSurface;

#if WINDOWS
        _gameWindowService.RequestMousePointerLock();

        IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(
            Application.Current.Windows[0].Handler.PlatformView
            as Microsoft.UI.Xaml.Window);

        MauiGameWindowService svc = (MauiGameWindowService)_gameWindowService;
        svc.StartRawInput(hwnd);
        svc.RawMouseDelta += OnRawMouseDelta;
#endif
        _game.IsSinglePlayer = true;
    }

    private async Task Connect()
    {
        // Start simulation loop + chunk workers + periodic tasks.
        // WorkerHost sets SinglePlayerServerLoaded = true once everything is live.
        // Fire-and-forget is fine — startup is fast, socket is already wired above.
        ProgressView.UpdateProgress(new() { Title = "Starting Game Engine...", Progress = 0 });
        _ = _workerHost.StartAsync();

        int port = Microsoft.Maui.Storage.Preferences.Get("session_port", 0);
        string username = Microsoft.Maui.Storage.Preferences.Get("username", "Player");
        string apiKey = Microsoft.Maui.Storage.Preferences.Get("api_key", string.Empty);
        string serverIp = Microsoft.Maui.Storage.Preferences.Get("server_ip", "127.0.0.1");

        _game.NetClient = new EnetNetClient(new NetworkService(_gameLogger));
        _game.ConnectData = new ConnectionData
        {
            Ip = serverIp,
            Port = port,
            Username = username,
            Auth = apiKey,
            ServerPassword = string.Empty,
            IsServerPasswordProtected = false,
        };
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _gameLoopTimer?.Stop();
        _gameLoopTimer = null;
#if WINDOWS
        ((MauiGameWindowService)_gameWindowService).ReleaseCursor();

        _gameWindowService.ExitMousePointerLock();

        IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(
            Application.Current.Windows[0].Handler.PlatformView
            as Microsoft.UI.Xaml.Window);

        MauiGameWindowService svc = (MauiGameWindowService)_gameWindowService;
        // Lines 228-245 from original (StopRawInput + RawMouseDelta unsubscribe) unchanged:
        svc.StopRawInput(hwnd);
        svc.RawMouseDelta -= OnRawMouseDelta;
#endif
    }

    private void GlView_PaintSurface(object? sender, SKPaintGLSurfaceEventArgs e)
    {
        try
        {
            if (!_glInitialized)
            {
#if WINDOWS
                GL.LoadBindings(new AngleBindingsContext());
#elif ANDROID
            GL.LoadBindings(new AndroidBindingsContext());
#endif
                ProgressView.UpdateProgress(new() { Title = "Initialising Shader...", Progress = 0 });
                InitGL();
                _glInitialized = true;
                _game.Start();
            }

            // Compute delta time
            DateTime now = DateTime.UtcNow;
            float dt = (float)(now - _lastFrame).TotalSeconds;
            _lastFrame = now;

            Draw(dt);
        }
        catch (Exception ex)
        {
            // Replace with your actual logger
            System.Diagnostics.Debug.WriteLine($"[FATAL] PaintSurface crashed: {ex}");
            File.AppendAllText(
                Path.Combine(FileSystem.CacheDirectory, "crash.txt"),
                $"{DateTime.UtcNow}: {ex}\n");
            throw; // re-throw so you still see it's fatal
        }
    }

    private void InitGL()
    {
        _openGlService.InitShaders();
        _openGlService.GlClearColorRgbaf(0, 0, 0, 1);
        _openGlService.GlEnableDepthTest();
    }

    private void Draw(float dt)
    {
        _openGlService.GlViewport(0, 0, (int)GlView.CanvasSize.Width, (int)GlView.CanvasSize.Height);
        _openGlService.GlClearColorBufferAndDepthBuffer();
        _openGlService.GlDisableDepthTest();
        _openGlService.GlDisableCullFace();

        Render(dt);
    }

    public void Render(float dt)
    {
        if (_game.IsReconnecting)
        {
            _game.Dispose();
            // restart game
            return;
        }

        if (_game.IsExitingToMainMenu)
        {
            _game.Dispose();
            // need to handle exit
            return;
        }

        _game.OnRenderFrame(dt);
    }

    // =========================================================================
    // Overlay — public API
    // =========================================================================

    /// <summary>
    /// Shows the pause menu overlay and releases the mouse lock so the cursor
    /// is usable in the overlay. Call when the player presses ESC.
    /// </summary>
    public void ShowPauseMenu()
    {
        OverlayMenu.ShowPauseMenu();   // reset to Pause panel (not Options)
        OverlayMenu.IsVisible = true;
        _gameWindowService.ExitMousePointerLock();
    }

    /// <summary>
    /// Hides the overlay entirely and restores input to the game.
    /// Called by the ReturnToGameRequested event handler.
    /// </summary>
    private void HideOverlay()
    {
        OverlayMenu.IsVisible = false;
#if WINDOWS
        ((MauiGameWindowService)_gameWindowService).CaptureCursor();
#endif
        _game.GuiState = GameState.Normal;
    }

    /// <summary>
    /// Handler for OverlayMenuView.ExitToMenuRequested.
    /// Hides the overlay, releases the cursor, and navigates to the main menu.
    /// </summary>
    private async void OnExitToMenuRequested(object? sender, EventArgs e)
    {
        HideOverlay();
#if WINDOWS
        ((MauiGameWindowService)_gameWindowService).ReleaseCursor();
#endif
        await Shell.Current.GoToAsync("//MainMenuView");
    }

    public void Dispose()
    {
        _subscription.Dispose();
    }
}