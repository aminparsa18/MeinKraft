// MAUI-specific implementation of IGameWindowService.
// SkiaSharp's SKGLView owns the EGL/ANGLE context — we never touch EGL directly.
//
// Attach(GameSKGLView) is called from GameView.OnAppearing.
// Detach() is called from GameView.OnDisappearing.

using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using System.Diagnostics;
using System.Drawing;
using System.Text.RegularExpressions;
using SkiaSharp.Views.Maui.Controls;
using OpenTK.Windowing.GraphicsLibraryFramework;

#if WINDOWS
using System.Runtime.InteropServices;
#endif

namespace MeinKraft.Maui.Services;

public sealed partial class MauiGameWindowService : IGameWindowService
{
    // ── Dependencies ──────────────────────────────────────────────────────────

    private readonly IGameExitService _gameExit;
    private readonly CrashReporter _crashReporter;
    private readonly IDisplayService _displayService;
    private readonly IOpenGlService _openGlService;

    // ── Late-bound view ───────────────────────────────────────────────────────

    private SKGLView? _view;

    // ── Timing ────────────────────────────────────────────────────────────────

    private readonly Stopwatch _start = Stopwatch.StartNew();
    public int TimeMillisecondsFromStart => (int)_start.ElapsedMilliseconds;

    // ── Canvas ────────────────────────────────────────────────────────────────

    public int CanvasWidth => (int)(_view?.Width ?? 0);
    public int CanvasHeight => (int)(_view?.Height ?? 0);

    // ── Constructor ───────────────────────────────────────────────────────────

    public MauiGameWindowService(
        IGameExitService gameExit,
        IDisplayService displayService,
        IOpenGlService openGlService,
        CrashReporter crashReporter)
    {
        _gameExit = gameExit;
        _displayService = displayService;
        _crashReporter = crashReporter;
        _openGlService = openGlService;

        _crashReporter.SetCursorVisible = MouseCursorSetVisible;
        _crashReporter.ShowErrorDialog = MessageBoxShowError;

        ThreadPool.SetMinThreads(32, 32);
        ThreadPool.SetMaxThreads(128, 128);
    }

    // ── View lifecycle ────────────────────────────────────────────────────────

    private bool _isFocused = false;
    /// <summary>
    /// Called from GameView.OnAppearing once the SKGLView is in the visual tree.
    /// SkiaSharp has already created the EGL/ANGLE context at this point.
    /// </summary>
    public void Attach(SKGLView view)
    {
        _view = view;
        view.EnableTouchEvents = true;
       // view.Touch += OnSkiaTouch;
        _isFocused = true;
        _initialised = false;
    }

    /// <summary>
    /// Called from GameView.OnDisappearing.
    /// </summary>
    public void Detach()
    {
        if (_view is null) return;
       // _view.Touch -= OnSkiaTouch;
        _view = null;
    }

    // ── Frame dispatch (called by GameSKGLView) ───────────────────────────────

    private bool _initialised;

    // ── IGameWindowService — render loop ──────────────────────────────────────

    /// <summary>No-op — render loop is driven by SKGLView.InvalidateSurface().</summary>
    public void Start()
    {
    }

    // ── IGameWindowService — input ────────────────────────────────────────────

    public List<Action<KeyEventArgs>> KeyDownHandlers { get; set; } = [];
    public List<Action<KeyEventArgs>> KeyUpHandlers { get; set; } = [];
    public List<Action<KeyPressEventArgs>> KeyPressHandlers { get; set; } = [];

    public event Action<MouseEventArgs>? OnMouseDown;
    public event Action<MouseEventArgs>? OnMouseUp;
    public event Action<MouseEventArgs>? OnMouseMove;
    public event Action<float>? OnMouseWheel;
    public event Action<TouchEventArgs>? OnTouchStart;
    public event Action<TouchEventArgs>? OnTouchMove;
    public event Action<TouchEventArgs>? OnTouchEnd;

    public delegate void GameKeyEventHandler(KeyEventArgs e);
    public List<GameKeyEventHandler> keyEventHandlers { get; set; } = [];
    public void AddOnKeyEvent(GameKeyEventHandler handler) => keyEventHandlers.Add(handler);

    public void AddOnKeyEvent(
        Action<KeyEventArgs> onKeyDown,
        Action<KeyEventArgs> onKeyUp,
        Action<KeyPressEventArgs> onKeyPress)
    {
        KeyDownHandlers.Add(onKeyDown);
        KeyUpHandlers.Add(onKeyUp);
        KeyPressHandlers.Add(onKeyPress);
    }

    public void AddOnMouseEvent(
        Action<MouseEventArgs> onMouseDown,
        Action<MouseEventArgs> onMouseUp,
        Action<MouseEventArgs> onMouseMove,
        Action<float> onMouseWheel)
    {
        OnMouseDown += onMouseDown;
        OnMouseUp += onMouseUp;
        OnMouseMove += onMouseMove;
        OnMouseWheel += onMouseWheel;
    }

    public void AddOnTouchEvent(
        Action<TouchEventArgs> onTouchStart,
        Action<TouchEventArgs> onTouchMove,
        Action<TouchEventArgs> onTouchEnd)
    {
        OnTouchStart += onTouchStart;
        OnTouchMove += onTouchMove;
        OnTouchEnd += onTouchEnd;
    }

    public void AddOnCrash(OnCrashHandler handler) => _crashReporter.OnCrash += handler.OnCrash;

    // ── Touch/mouse from SKGLView ─────────────────────────────────────────────

    public bool TouchTest = false;

    // ── Cursor / focus ────────────────────────────────────────────────────────

    private bool _mousePointerLocked;
    private bool _mouseCursorVisible = true;

    public bool IsMousePointerLocked() => _mousePointerLocked;
    public bool MouseCursorIsVisible() => _mouseCursorVisible;
    public bool Focused() => _isFocused;

    public void MouseCursorSetVisible(bool value)
    {
        _mouseCursorVisible = value;
    }

    public void RequestMousePointerLock() { MouseCursorSetVisible(false); _mousePointerLocked = true; }
    public void ExitMousePointerLock() { MouseCursorSetVisible(true); _mousePointerLocked = false; }

    public void SetWindowCursor(int hotx, int hoty, int sizex, int sizey,
        byte[] imgdata, int imgdataLength)
    { }
    public void RestoreWindowCursor() => MouseCursorSetVisible(true);

    // ── Window ────────────────────────────────────────────────────────────────

    public void SetTitle(string title)
    {
        var win = Application.Current?.Windows[0];
#if WINDOWS
        if (win?.Handler?.PlatformView is Microsoft.UI.Xaml.Window w) 
            w.Title = title;
#endif
    }

    public void SetVSync(bool enabled) { }
    public void WindowExit() { _gameExit.Exit = true; Application.Current?.Quit(); }
    public WindowState GetWindowState() => WindowState.Normal;
    public void SetWindowState(WindowState value) { }
    public void ChangeResolution(int w, int h, int bpp, float refresh) { }

    public List<DisplayResolution> GetDisplayResolutions()
        => [.. _displayService.GetDisplayResolutions()
            .Where(r => r.Width >= 800 && r.Height >= 600 && r.BitsPerPixel >= 16)
            .Select(r => new DisplayResolution
            {
                Width = r.Width,
                Height = r.Height,
                BitsPerPixel = r.BitsPerPixel,
                RefreshRate = r.RefreshRate,
            })];

    public DisplayResolution GetDisplayResolutionDefault()
        => _displayService.GetDisplayResolutionDefault();

    public string KeyName(int key)
        => Enum.IsDefined(typeof(Keys), key)
            ? Enum.GetName(typeof(Keys), key)!
            : key.ToString();

    // ── Misc ──────────────────────────────────────────────────────────────────

    public string StoragePath => GameStorePath.GetStorePath();
    public string GameSavePath => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    public string GameLogsPath => Path.Combine(StoragePath, "Logs");

    public INetworkService NetworkService { get; set; }
    public GameWindow Window { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public string GetGameVersion() => GameVersion.Version;
    public bool IsFastSystem() => true;
    public bool MultithreadingAvailable() => true;
    public bool IsSmallScreen() => TouchTest;
    public bool IsDebuggerAttached() => Debugger.IsAttached;
    public string QueryStringValue(string key) => null;
    public void ApplicationDoEvents() { }
    public void ShowKeyboard(bool show) { }
    public void QueueUserWorkItem(Action action) => ThreadPool.QueueUserWorkItem(_ => action());
    public void OpenLinkInBrowser(string url)
    {
        if (url.StartsWith("http://") || url.StartsWith("https://")) Process.Start(url);
    }

    public void MessageBoxShowError(string text, string caption)
    {
        if (OperatingSystem.IsWindows())
            Application.Current.MainPage.DisplayAlert(caption, text, "OK");
    }

    public bool ChatLog(string servername, string p)
    {
        Directory.CreateDirectory(GameLogsPath);
        File.AppendAllText(
            Path.Combine(GameLogsPath, SanitiseFileName(servername) + ".txt"),
            $"{DateTime.Now} {p}\n");
        return true;
    }

    private static string SanitiseFileName(string name)
        => Regex.Replace(name, $"[{Regex.Escape(new string(Path.GetInvalidFileNameChars()))}]", "_");

    private readonly ClientNative.Screenshot _screenshot = new();

    public void SaveScreenshot() 
    {
        _screenshot.d_GameWindow = Window;
        _screenshot.SaveScreenshot();
    }
    public Bitmap GrabScreenshot()
    {
        _screenshot.d_GameWindow = Window;
        Bitmap bmp = _screenshot.GrabScreenshot();
        return bmp;
    }

    public Stream? OpenIconStream()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "md.ico");
        return File.Exists(path) ? File.OpenRead(path) : null;
    }

    public string Cachepath() => Path.Combine(StoragePath, "Cache");
    public void Checkcachedir() => Directory.CreateDirectory(Cachepath());

    public void SaveAssetToCache(Asset tosave)
    {
        Checkcachedir();
        using BinaryWriter bw = new(File.Create(Path.Combine(Cachepath(), tosave.md5)));
        bw.Write(tosave.name); bw.Write(tosave.dataLength); bw.Write(tosave.data);
    }

    public Asset LoadAssetFromCache(string md5)
    {
        using BinaryReader br = new(File.OpenRead(Path.Combine(Cachepath(), md5)));
        string name = br.ReadString(); int len = br.ReadInt32(); byte[] data = br.ReadBytes(len);
        return new Asset { data = data, dataLength = len, md5 = md5, name = name };
    }

    public bool IsCached(string md5)
        => Directory.Exists(Cachepath()) && File.Exists(Path.Combine(Cachepath(), md5));

#if WINDOWS
    // -------------------------------------------------------------------------
    // P/Invoke
    // -------------------------------------------------------------------------

    [DllImport("user32.dll")] private static extern IntPtr CreateCursor(IntPtr hInst, int xHotSpot, int yHotSpot, int nWidth, int nHeight, byte[] andPlane, byte[] xorPlane);
    [DllImport("user32.dll")] private static extern bool SetSystemCursor(IntPtr hCursor, uint id);
    [DllImport("user32.dll")] private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);
    [DllImport("user32.dll")] private static extern bool ClipCursor(IntPtr lpRect);
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] private static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] devices, uint count, uint size);
    [DllImport("user32.dll")] private static extern uint GetRawInputData(IntPtr hRawInput, uint command, IntPtr data, ref uint size, uint headerSize);
    [DllImport("user32.dll")] private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, WndProcDelegate proc);
    [DllImport("user32.dll")] private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr proc);
    [DllImport("user32.dll")] private static extern IntPtr CallWindowProc(IntPtr prev, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    // -------------------------------------------------------------------------
    // Constants
    // -------------------------------------------------------------------------

    private const uint OCR_NORMAL = 32512;
    private const uint SPI_SETCURSORS = 0x0057;
    private const int GWL_WNDPROC = -4;

    // WM_ messages
    private const uint WM_INPUT = 0x00FF;

    // Raw input
    private const uint RID_INPUT = 0x10000003;
    private const uint RIM_TYPEMOUSE = 0;

    // -------------------------------------------------------------------------
    // Structs
    // -------------------------------------------------------------------------

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTDEVICE
    {
        public ushort UsagePage;
        public ushort Usage;
        public uint Flags;
        public IntPtr Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTHEADER
    {
        public uint Type;
        public uint Size;
        public IntPtr Device;
        public IntPtr wParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWMOUSE
    {
        public ushort Flags;
        public ushort ButtonFlags;  // was: uint Buttons (low word is flags)
        public ushort ButtonData;   // high word — wheel delta for scroll
        public uint RawButtons;
        public int LastX;
        public int LastY;
        public uint ExtraInformation;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUT
    {
        public RAWINPUTHEADER Header;
        public RAWMOUSE Mouse;
    }

    // -------------------------------------------------------------------------
    // State
    // -------------------------------------------------------------------------

    private WndProcDelegate? _wndProc;
    private IntPtr _oldWndProc = IntPtr.Zero;

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>Raw mouse movement delta (dx, dy) from WM_INPUT.</summary>
    public event Action<int, int>? RawMouseDelta;

    /// <summary>Mouse button pressed. Args: (button, x, y) in client coords.</summary>
    public event Action<MouseButton, int, int>? RawMouseDown;

    /// <summary>Mouse button released. Args: (button, x, y) in client coords.</summary>
    public event Action<MouseButton, int, int>? RawMouseUp;

    public void StartRawInput(IntPtr hwnd)
    {
        RAWINPUTDEVICE[] rid =
        [
            new RAWINPUTDEVICE
            {
                UsagePage = 0x01, // Generic Desktop
                Usage     = 0x02, // Mouse
                Flags     = 0,
                Target    = hwnd
            }
        ];

        RegisterRawInputDevices(rid, 1, (uint)Marshal.SizeOf<RAWINPUTDEVICE>());

        _wndProc = WndProc;
        _oldWndProc = SetWindowLongPtr(hwnd, GWL_WNDPROC, _wndProc);

        System.Diagnostics.Debug.WriteLine($"[RawInput] registered hwnd={hwnd} oldWndProc={_oldWndProc}");
    }

    public void StopRawInput(IntPtr hwnd)
    {
        if (_oldWndProc == IntPtr.Zero)
        {
            return;
        }

        SetWindowLongPtr(hwnd, GWL_WNDPROC, _oldWndProc);
        _oldWndProc = IntPtr.Zero;
    }

    public void TrapCursorInCenter()
    {
        if (!_mousePointerLocked)
        {
            return;
        }

        IntPtr hwnd = GetMauiHwnd();
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        GetWindowRect(hwnd, out RECT rect);
        SetCursorPos((rect.Left + rect.Right) / 2, (rect.Top + rect.Bottom) / 2);
    }

    public void CaptureCursor()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // Create 1x1 invisible cursor
            IntPtr invisible = CreateCursor(
                IntPtr.Zero, 0, 0, 1, 1,
                [0xFF], // AND mask — fully transparent
                [0x00]);// XOR mask — no pixels

            // Replace the system arrow cursor globally
            SetSystemCursor(invisible, OCR_NORMAL);
        });
    }

    public void ReleaseCursor()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // Restore ALL system cursors to Windows defaults in one call
            SystemParametersInfo(SPI_SETCURSORS, 0, IntPtr.Zero, 0);
            ClipCursor(IntPtr.Zero);
        });
    }

    // -------------------------------------------------------------------------
    // WndProc
    // -------------------------------------------------------------------------

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WM_INPUT:
                HandleRawInput(lParam);
                break;
        }

        return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    // Button flag constants

    private void HandleRawInput(IntPtr lParam)
    {
        uint size = 0;
        GetRawInputData(lParam, RID_INPUT, IntPtr.Zero, ref size, (uint)Marshal.SizeOf<RAWINPUTHEADER>());

        IntPtr buf = Marshal.AllocHGlobal((int)size);
        try
        {
            GetRawInputData(lParam, RID_INPUT, buf, ref size, (uint)Marshal.SizeOf<RAWINPUTHEADER>());
            RAWINPUT raw = Marshal.PtrToStructure<RAWINPUT>(buf);

            if (raw.Header.Type != RIM_TYPEMOUSE)
            {
                return;
            }

            // Mouse movement
            if (_isFocused && (raw.Mouse.LastX != 0 || raw.Mouse.LastY != 0))
            {
                RawMouseDelta?.Invoke(raw.Mouse.LastX, raw.Mouse.LastY);
            }

            // Mouse buttons — always fire regardless of focus
            ushort flags = raw.Mouse.ButtonFlags;
        }
        finally
        {
            Marshal.FreeHGlobal(buf);
        }
    }

    private static IntPtr GetMauiHwnd()
        => Application.Current?.Windows[0].Handler?.PlatformView is not Microsoft.UI.Xaml.Window window ? IntPtr.Zero : WinRT.Interop.WindowNative.GetWindowHandle(window);
#endif
}