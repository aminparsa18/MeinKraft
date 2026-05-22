using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.Desktop;

public class GameWindowNative : GameWindow
{
    public GameWindowNative()
        : base(
            new GameWindowSettings
            {
                UpdateFrequency = 60.0 // Cap at 60 updates/sec
            },
            new NativeWindowSettings
            {
                ClientSize = new Vector2i(1280, 720),
                Title = "",
                WindowState = WindowState.Maximized,
                Profile = ContextProfile.Core,
                Icon = LoadWindowIcon(Path.Combine(AppContext.BaseDirectory, "md.ico"))
            })
    {
    }

    private static WindowIcon? LoadWindowIcon(string path)
    {
        if (!File.Exists(path)) return null;

        // OpenTK wants raw RGBA pixels. Use System.Drawing (available via UseWindowsForms).
        using var bmp = new Icon(path).ToBitmap();
        var pixels = new byte[bmp.Width * bmp.Height * 4];
        int i = 0;
        for (int y = 0; y < bmp.Height; y++)
            for (int x = 0; x < bmp.Width; x++)
            {
                var c = bmp.GetPixel(x, y);
                pixels[i++] = c.R;
                pixels[i++] = c.G;
                pixels[i++] = c.B;
                pixels[i++] = c.A;
            }
        var image = new OpenTK.Windowing.Common.Input.Image(bmp.Width, bmp.Height, pixels);
        return new WindowIcon(image);
    }
}