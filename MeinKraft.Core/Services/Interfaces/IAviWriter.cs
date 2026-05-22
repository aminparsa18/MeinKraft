public interface IAviWriter
{
    void Open(string filename, int framerate, int width, int height);
    void AddFrame(Bitmap bitmap);
    void Close();
}
