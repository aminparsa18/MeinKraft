using OpenTK.Windowing.Common;

// ─────────────────────────────────────────────────────────────────────────────
// Composite — the full platform contract used throughout the game.
// GamePlatformNative implements this; everything else depends on it.
// ─────────────────────────────────────────────────────────────────────────────

public interface IGameWindowService
{
    INetworkService NetworkService { get; set; }
    void AddOnKeyEvent(Action<KeyEventArgs> onKeyDown,
        Action<KeyEventArgs> onKeyUp,
        Action<KeyPressEventArgs> onKeyPress);
    void AddOnMouseEvent(
        Action<MouseEventArgs> onMouseDown,
        Action<MouseEventArgs> onMouseUp,
        Action<MouseEventArgs> onMouseMove,
        Action<float> onMouseWheel);
    void AddOnTouchEvent(Action<TouchEventArgs> onTouchStart,
        Action<TouchEventArgs> onTouchMove,
        Action<TouchEventArgs> onTouchEnd);
    int CanvasWidth { get; }
    int CanvasHeight { get; }
    int TimeMillisecondsFromStart { get; }
    Stream? OpenIconStream();
    void SaveScreenshot();
    Bitmap GrabScreenshot();
    string StoragePath { get; }
    string GameSavePath { get; }
    void SetVSync(bool enabled);
    string GetGameVersion();
    bool ChatLog(string servername, string p);
    void WindowExit();
    void MessageBoxShowError(string text, string caption);
    void SetTitle(string applicationname);
    bool Focused();
    void AddOnCrash(OnCrashHandler handler);
    string KeyName(int key);
    List<DisplayResolution> GetDisplayResolutions();
    WindowState GetWindowState();
    void SetWindowState(WindowState value);
    void ChangeResolution(int width, int height, int bitsPerPixel, float refreshRate);
    DisplayResolution GetDisplayResolutionDefault();

    void MouseCursorSetVisible(bool value);
    bool MouseCursorIsVisible();
    void ApplicationDoEvents();
    void ShowKeyboard(bool show);
    bool IsFastSystem();

    bool IsMousePointerLocked();
    void RequestMousePointerLock();
    void ExitMousePointerLock();
    bool MultithreadingAvailable();
    void QueueUserWorkItem(Action action);
    bool IsDebuggerAttached();
    bool IsSmallScreen();
    void OpenLinkInBrowser(string url);
    void SaveAssetToCache(Asset tosave);
    Asset LoadAssetFromCache(string md5);
    bool IsCached(string md5);
    string QueryStringValue(string key);
    void SetWindowCursor(int hotx, int hoty, int sizex, int sizey, byte[] imgdata, int imgdataLength);

  //  void ReleaseCursor();
    void RestoreWindowCursor();

   // GameWindow Window { get; set; }
    void Start();
}

public class OnCrashHandler
{
    public virtual void OnCrash() { }
}

