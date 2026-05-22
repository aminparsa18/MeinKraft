using MeinKraft.ClientNative;
using OpenTK.Graphics.ES30;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;

public partial class GameWindowService : IGameWindowService
{
    #region Misc

    public readonly IGameExitService _gameExit;
    private readonly CrashReporter _crashReporter;
    private readonly IDisplayService _displayService;

    public GameWindowService(IGameExitService gameExit, IDisplayService displayService, GameWindowNative gameWindowNative, CrashReporter crashReporter)
    {
        _gameExit = gameExit;
        _displayService = displayService;
        Window = gameWindowNative;
        ThreadPool.SetMinThreads(32, 32);
        ThreadPool.SetMaxThreads(128, 128);
        start.Start();
        _crashReporter = crashReporter;
        _crashReporter.SetCursorVisible = MouseCursorSetVisible;
        _crashReporter.ShowErrorDialog = MessageBoxShowError;
    }

    public INetworkService NetworkService { get; set; }

    public bool TouchTest = false;

    public string GameSavePath { get; } = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

    private readonly Stopwatch start = new();

    public int TimeMillisecondsFromStart => (int)start.ElapsedMilliseconds;

    public bool IsMono = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    public string StoragePath { get; } = GameStorePath.GetStorePath();

    public string GetGameVersion() => GameVersion.Version;

    public string GameLogsPath => Path.Combine(StoragePath, "Logs");

    private static string MakeValidFileName(string name)
    {
        string invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
        string invalidReStr = string.Format(@"[{0}]", invalidChars);
        return Regex.Replace(name, invalidReStr, "_");
    }

    public bool ChatLog(string servername, string p)
    {
        if (!Directory.Exists(GameLogsPath))
        {
            Directory.CreateDirectory(GameLogsPath);
        }

        string filename = Path.Combine(GameLogsPath, MakeValidFileName(servername) + ".txt");
        File.AppendAllText(filename, string.Format("{0} {1}\n", DateTime.Now, p));
        return true;
    }

    public void MessageBoxShowError(string text, string caption)
    {
        // On Windows only, show the native box without WinForms
        if (OperatingSystem.IsWindows())
        {
            MessageBoxW(text, caption);
        }
    }

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int MessageBoxW(string text, string caption);

    public void ApplicationDoEvents()
    {
        if (IsMono)
        {
            Thread.Sleep(0);
        }
    }

    public Stream? OpenIconStream()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "md.ico");
        return File.Exists(path) ? File.OpenRead(path) : null;
    }

    public void ShowKeyboard(bool show)
    {
    }

    public bool IsFastSystem() => true;

    public bool IsMac = Environment.OSVersion.Platform == PlatformID.MacOSX;

    public bool MultithreadingAvailable() => true;

    public void QueueUserWorkItem(Action action) => ThreadPool.QueueUserWorkItem((a) => { action(); });

    public bool IsSmallScreen() => TouchTest;

    public void OpenLinkInBrowser(string url)
    {
        if (!(url.StartsWith("http://") || url.StartsWith("https://")))
        {
            //Check if string is an URL - if not, abort
            return;
        }

        Process.Start(url);
    }

    public string Cachepath() => Path.Combine(StoragePath, "Cache");

    public void Checkcachedir()
    {
        if (!Directory.Exists(Cachepath()))
        {
            Directory.CreateDirectory(Cachepath());
        }
    }

    public void SaveAssetToCache(Asset tosave)
    {
        //Check if cache directory exists
        Checkcachedir();
        BinaryWriter bw = new(File.Create(Path.Combine(Cachepath(), tosave.md5)));
        bw.Write(tosave.name);
        bw.Write(tosave.dataLength);
        bw.Write(tosave.data);
        bw.Close();
    }

    public Asset LoadAssetFromCache(string md5)
    {
        //Check if cache directory exists
        Checkcachedir();
        BinaryReader br = new(File.OpenRead(Path.Combine(Cachepath(), md5)));
        string contentName = br.ReadString();
        int contentLength = br.ReadInt32();
        byte[] content = br.ReadBytes(contentLength);
        br.Close();
        Asset a = new()
        {
            data = content,
            dataLength = contentLength,
            md5 = md5,
            name = contentName
        };
        return a;
    }

    public bool IsCached(string md5)
    {
        if (!Directory.Exists(Cachepath()))
        {
            return false;
        }

        return File.Exists(Path.Combine(Cachepath(), md5));
    }

    public bool IsDebuggerAttached() => Debugger.IsAttached;

    public string QueryStringValue(string key) => null;

    #endregion

    #region Game

    public GameWindow Window { get; set; }

    public int CanvasWidth => Window.ClientSize.X;

    public int CanvasHeight => Window.ClientSize.Y;

    public void Start()
    {
        Window.KeyDown += GameKeyDown;
        Window.KeyUp += GameKeyUp;
        Window.TextInput += GameTextInput;
        Window.MouseDown += Mouse_ButtonDown;
        Window.MouseUp += Mouse_ButtonUp;
        Window.MouseMove += Mouse_Move;
        Window.MouseWheel += Mouse_WheelChanged;
        Window.RenderFrame += WindowRenderFrame;
        Window.Closing += WindowClosed;
        Window.Title = "Manic Digger";

        GL.DebugMessageCallback((source, type, id, severity, length, message, param) =>
        {
            if (severity == DebugSeverity.DebugSeverityNotification)
            {
                return; // ignore info messages like this one
            }

            string msg = Marshal.PtrToStringAnsi(message, length);
            Console.WriteLine($"[OpenGL] [{severity}] [{type}] {msg}");

        }, IntPtr.Zero);

        Window.Run();
    }

    private void WindowClosed(CancelEventArgs e) => _gameExit.Exit = e.Cancel;

    public void SetVSync(bool enabled) => Window.VSync = enabled ? VSyncMode.On : VSyncMode.Off;

    private readonly Screenshot _screenshot = new();

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

    public void WindowExit()
    {
        _gameExit?.Exit = true;
        Window.Close();
    }

    public void SetTitle(string applicationname) => Window.Title = applicationname;

    public string KeyName(int key)
    {
        return Enum.IsDefined(typeof(Keys), key) ? Enum.GetName(typeof(Keys), key)! : key.ToString();
    }

    private List<DisplayResolution> resolutions;

    public List<DisplayResolution> GetDisplayResolutions()
    {
        if (resolutions == null)
        {
            resolutions = [];
            foreach (var screen in _displayService.GetDisplayResolutions())
            {
                DisplayResolution resolution = new()
                {
                    Width = screen.Width,
                    Height = screen.Height,
                    BitsPerPixel = screen.BitsPerPixel,
                    RefreshRate = screen.RefreshRate
                };

                if (resolution.Width < 800 || resolution.Height < 600 || resolution.BitsPerPixel < 16)
                {
                    continue;
                }

                resolutions.Add(resolution);
            }
        }

        return resolutions;
    }

    public WindowState GetWindowState() => Window.WindowState;

    public void SetWindowState(WindowState value) => Window.WindowState = value;

    public void ChangeResolution(int width, int height, int bitsPerPixel, float refreshRate) => Window.Size = new Vector2i(width, height);

    public DisplayResolution GetDisplayResolutionDefault() => _displayService.GetDisplayResolutionDefault();

    #endregion

    #region Event handlers

    public List<Action<float>> newFrameHandlers = new();
    public void AddOnNewFrame(Action<float> handler) => newFrameHandlers.Add(handler);

    public List<Action<KeyEventArgs>> KeyDownHandlers { get; set; } = [];

    public List<Action<KeyEventArgs>> KeyUpHandlers = [];
    public List<Action<KeyPressEventArgs>> KeyPressHandlers = [];

    public event Action<TouchEventArgs> OnTouchStart;
    public event Action<TouchEventArgs> OnTouchMove;
    public event Action<TouchEventArgs> OnTouchEnd;

    public event Action<MouseEventArgs> OnMouseDown;
    public event Action<MouseEventArgs> OnMouseUp;
    public event Action<MouseEventArgs> OnMouseMove;
    public event Action<float> OnMouseWheel;

    public void AddOnKeyEvent(
        Action<KeyEventArgs> onKeyDown,
        Action<KeyEventArgs> onKeyUp,
        Action<KeyPressEventArgs> onKeyPress)
    {
        KeyDownHandlers.Add(onKeyDown);
        KeyUpHandlers.Add(onKeyUp);
        KeyPressHandlers.Add(onKeyPress);
    }

    public delegate void GameKeyEventHandler(KeyEventArgs e);

    public List<GameKeyEventHandler> keyEventHandlers { get; set; } = new();
    public void AddOnKeyEvent(GameKeyEventHandler handler) => keyEventHandlers.Add(handler);

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

    public void AddOnTouchEvent(Action<TouchEventArgs> onTouchStart,
        Action<TouchEventArgs> onTouchMove,
        Action<TouchEventArgs> onTouchEnd)
    {
        OnTouchStart += onTouchStart;
        OnTouchMove += onTouchMove;
        OnTouchEnd += onTouchEnd;
    }

    public void AddOnCrash(OnCrashHandler handler) => _crashReporter.OnCrash += handler.OnCrash;

    #endregion

    #region Input

    private bool mousePointerLocked;
    private bool mouseCursorVisible = true;

    public bool IsMousePointerLocked() => mousePointerLocked;

    public bool MouseCursorIsVisible() => mouseCursorVisible;

    public void SetWindowCursor(int hotx, int hoty, int sizex, int sizey, byte[] imgdata, int imgdataLength)
    {
        try
        {
            Bitmap bmp = new(new MemoryStream(imgdata, 0, imgdataLength)); //new Bitmap("data/local/gui/mousecursor.png");
            if (bmp.Width > 32 || bmp.Height > 32)
            {
                // Limit cursor size to 32x32
                return;
            }
            // Convert to required 0xBBGGRRAA format - see https://github.com/opentk/opentk/pull/107#issuecomment-41771702
            int i = 0;
            byte[] data = new byte[4 * bmp.Width * bmp.Height];
            for (int y = 0; y < bmp.Width; y++)
            {
                for (int x = 0; x < bmp.Height; x++)
                {
                    Color color = bmp.GetPixel(x, y);
                    data[i] = color.B;
                    data[i + 1] = color.G;
                    data[i + 2] = color.R;
                    data[i + 3] = color.A;
                    i += 4;
                }
            }

            bmp.Dispose();
            Window.Cursor = new MouseCursor(hotx, hoty, sizex, sizey, data);
        }
        catch
        {
            RestoreWindowCursor();
        }
    }

    public void RestoreWindowCursor() => Window.Cursor = MouseCursor.Default;

    public void MouseCursorSetVisible(bool value)
    {
        if (!value)
        {
            if (TouchTest)
            {
                return;
            }

            if (!mouseCursorVisible)
            {
                //Cursor already hidden. Do nothing.
                return;
            }

            Window.CursorState = CursorState.Grabbed;
            mouseCursorVisible = false;
        }
        else
        {
            if (mouseCursorVisible)
            {
                //Cursor already visible. Do nothing.
                return;
            }

            Window.CursorState = CursorState.Normal;
            mouseCursorVisible = true;
        }
    }

    public void RequestMousePointerLock()
    {
        MouseCursorSetVisible(false);
        mousePointerLocked = true;
    }

    public void ExitMousePointerLock()
    {
        MouseCursorSetVisible(true);
        mousePointerLocked = false;
    }

    public bool Focused() => Window.IsFocused;

    private void WindowRenderFrame(FrameEventArgs e)
    {
        UpdateMousePosition();
        foreach (Action<float> h in newFrameHandlers)
        {
            h((float)e.Time);
        }

        Window.SwapBuffers();
    }

    private void UpdateMousePosition()
    {
        if (!Window.IsFocused)
        {
            return;
        }

        MouseState mouse = Window.MouseState;
        float xdelta = mouse.Delta.X;
        float ydelta = mouse.Delta.Y;

        if (xdelta != 0 || ydelta != 0)
        {
            MouseEventArgs args = new();
            args.X = (int)mouse.Position.X;
            args.Y = (int)mouse.Position.Y;
            args.MovementX = (int)xdelta;
            args.MovementY = (int)ydelta;
            args.Emulated = true;
            OnMouseMove?.Invoke(args);
        }
    }

    private void Mouse_WheelChanged(MouseWheelEventArgs e) => OnMouseWheel?.Invoke(e.OffsetY);

    private void Mouse_ButtonDown(MouseButtonEventArgs e)
    {
        Vector2 pos = Window.MousePosition;
        if (TouchTest)
        {
            TouchEventArgs args = new();
            args.SetX((int)pos.X);
            args.SetY((int)pos.Y);
            args.SetId(0);
            OnTouchStart?.Invoke(args);
        }
        else
        {
            MouseEventArgs args = new();
            args.X = (int)pos.X;
            args.Y = (int)pos.Y;
            args.Button = (int)e.Button;
            OnMouseDown?.Invoke(args);
        }
    }

    private void Mouse_ButtonUp(MouseButtonEventArgs e)
    {
        Vector2 pos = Window.MousePosition;
        if (TouchTest)
        {
            TouchEventArgs args = new();
            args.SetX((int)pos.X);
            args.SetY((int)pos.Y);
            args.SetId(0);
            OnTouchEnd?.Invoke(args);
        }
        else
        {
            MouseEventArgs args = new();
            args.X = (int)pos.X;
            args.Y = (int)pos.Y;
            args.Button = (int)e.Button;
            OnMouseUp?.Invoke(args);
        }
    }

    private void Mouse_Move(MouseMoveEventArgs e)
    {
        if (TouchTest)
        {
            Console.WriteLine("TouchTest path");
            TouchEventArgs args = new();
            args.SetX((int)e.X);
            args.SetY((int)e.Y);
            args.SetId(0);
            OnTouchMove?.Invoke(args);
        }
        else
        {
            Console.WriteLine("Mouse path");
            MouseEventArgs args = new()
            {
                X = (int)e.X,
                Y = (int)e.Y,
                MovementX = (int)e.DeltaX,
                MovementY = (int)e.DeltaY,
                Emulated = false
            };
            OnMouseMove?.Invoke(args);
        }
    }

    private void GameTextInput(TextInputEventArgs e)
    {
        KeyPressEventArgs args = new() { KeyChar = (char)e.Unicode };
        foreach (Action<KeyPressEventArgs> h in KeyPressHandlers)
        {
            h(args);
            if (args.Handled)
            {
                break;
            }
        }
    }

    private void GameKeyDown(KeyboardKeyEventArgs e)
    {
        KeyEventArgs args = new()
        {
            KeyChar = (int)e.Key,
            CtrlPressed = e.Modifiers == KeyModifiers.Control,
            ShiftPressed = e.Modifiers == KeyModifiers.Shift,
            AltPressed = e.Modifiers == KeyModifiers.Alt
        };
        foreach (Action<KeyEventArgs> h in KeyDownHandlers)
        {
            h(args);
            if (args.Handled)
            {
                break;
            }
        }
    }

    private void GameKeyUp(KeyboardKeyEventArgs e)
    {
        KeyEventArgs args = new() { KeyChar = (int)e.Key };
        foreach (Action<KeyEventArgs> h in KeyUpHandlers)
        {
            h(args);
            if (args.Handled)
            {
                break;
            }
        }
    }

    public void ReleaseCursor() => throw new NotImplementedException();

    #endregion
}