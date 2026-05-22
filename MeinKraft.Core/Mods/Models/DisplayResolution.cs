public class DisplayResolution
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int BitsPerPixel { get; set; }
    public float RefreshRate { get; set; }
    public int GetWidth() => Width;
    public void SetWidth(int value) => Width = value;
    public int GetHeight() => Height;
    public void SetHeight(int value) => Height = value;
    public int GetBitsPerPixel() => BitsPerPixel;
    public void SetBitsPerPixel(int value) => BitsPerPixel = value;
    public float GetRefreshRate() => RefreshRate;
    public void SetRefreshRate(float value) => RefreshRate = value;
}
