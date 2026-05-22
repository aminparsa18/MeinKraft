using OpenTK.Mathematics;

public class PlayerDrawInfo
{
    public PlayerDrawInfo()
    {
    }

    internal NetworkInterpolation Interpolation { get; set; }
    internal float LastNetworkPosX { get; set; }
    internal float LastNetworkPosY { get; set; }
    internal float LastNetworkPosZ { get; set; }
    internal float LastCurPosX { get; set; }
    internal float LastCurPosY { get; set; }
    internal float LastCurPosZ { get; set; }
    internal float LastNetworkRotX { get; set; }
    internal float LastNetworkRotY { get; set; }
    internal float LastNetworkRotZ { get; set; }
    internal Vector3 Velocity { get; set; } = Vector3.Zero;
    internal bool Moves { get; set; }
}