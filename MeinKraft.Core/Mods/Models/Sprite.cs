public class Sprite
{
    public Sprite()
    {
        Size = 40;
    }

    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public float PositionZ { get; set; }
    public string Image { get; set; }
    public int Size { get; set; }
    public int AnimationCount { get; set; }
}
