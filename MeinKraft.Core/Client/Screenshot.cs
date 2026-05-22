using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Desktop;

namespace MeinKraft.ClientNative;

public interface IScreenshot
{
    void SaveScreenshot();
}

public class Screenshot : IScreenshot
{
    public GameWindow d_GameWindow;
    public string SavePath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

    public void SaveScreenshot()
    {
        using Bitmap bmp = GrabScreenshot();
        string time = string.Format("{0:yyyy-MM-dd_HH-mm-ss}", DateTime.Now);
        string filename = Path.Combine(SavePath, time + ".png");
        bmp.Save(filename);
    }

    // Returns a System.Drawing.Bitmap with the contents of the current framebuffer
    public Bitmap GrabScreenshot()
    {
        int width = d_GameWindow.ClientSize.X;
        int height = d_GameWindow.ClientSize.Y;

        Bitmap bmp = new(width, height);
        System.Drawing.Imaging.BitmapData data = bmp.LockBits(new Rectangle(0, 0, width, height),
        System.Drawing.Imaging.ImageLockMode.WriteOnly,
        System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        GL.ReadPixels(0, 0, width, height, PixelFormat.Bgr, PixelType.UnsignedByte, data.Scan0);
        bmp.UnlockBits(data);

        bmp.RotateFlip(RotateFlipType.RotateNoneFlipY);
        return bmp;
    }
}
