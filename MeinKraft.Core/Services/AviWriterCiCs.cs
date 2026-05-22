public class AviWriterCiCs : IAviWriter
{
    public AviWriterCiCs()
    {
        avi = new AviWriter();
    }

    public AviWriter avi;
    public Bitmap openbmp;

    public void Open(string filename, int framerate, int width, int height) => openbmp = avi.Open(filename, (uint)framerate, width, height);

    public void AddFrame(Bitmap bitmap)
    {
        Bitmap bmp_ = bitmap;

        using (Graphics g = Graphics.FromImage(openbmp))
        {
            g.DrawImage(bmp_, 0, 0);
        }

        openbmp.RotateFlip(RotateFlipType.RotateNoneFlipY);

        avi.AddFrame();
    }

    public void Close() => avi.Close();
}
