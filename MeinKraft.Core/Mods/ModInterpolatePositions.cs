/// <summary>
/// Interpolates network entity positions each frame for smooth remote player movement.
/// </summary>
public class ModInterpolatePositions : ModBase
{
    private const int ExtrapolationTimeMs = 300;
    private const int MinDelayMs = 100;


    public ModInterpolatePositions(IGame game) : base(game)
    {
    }

    public override void OnFrame(float dt) => InterpolatePositions();

    internal void InterpolatePositions()
    {
        for (int i = 0; i < Game.Entities.Count; i++)
        {
            Entity e = Game.Entities[i];
            if (e?.NetworkPosition == null)
            {
                continue;
            }

            if (i == Game.LocalPlayerId)
            {
                continue;
            }

            if (!e.NetworkPosition.PositionLoaded)
            {
                continue;
            }

            e.PlayerDrawInfo ??= new PlayerDrawInfo();
            EnsureInterpolation(e);

            e.PlayerDrawInfo.Interpolation.DelayMilliseconds =
                Math.Max(MinDelayMs, Game.ServerInfo.ServerPing.RoundtripMilliseconds);

            UpdateInterpolation(e);
        }
    }

    /// <summary>
    /// Initialises the network interpolation state for an entity if not already set up.
    /// </summary>
    private static void EnsureInterpolation(Entity e)
    {
        if (e.PlayerDrawInfo.Interpolation != null)
        {
            return;
        }

        e.PlayerDrawInfo.Interpolation = new NetworkInterpolation
        {
            Interpolator = new PlayerInterpolate(),
            DelayMilliseconds = 500,
            Extrapolate = false,
            ExtrapolationTimeMilliseconds = ExtrapolationTimeMs
        };
    }

    private void UpdateInterpolation(Entity e)
    {
        PlayerDrawInfo info = e.PlayerDrawInfo;
        EntityPosition net = e.NetworkPosition;

        float netX = net.X, netY = net.Y, netZ = net.Z;

        // Feed a new state packet when network position or rotation has changed.
        bool posChanged = !(netX == info.LastNetworkPosX
                         && netY == info.LastNetworkPosY
                         && netZ == info.LastNetworkPosZ);
        bool rotChanged = net.RotX != info.LastNetworkRotX
                       || net.RotY != info.LastNetworkRotY
                       || net.RotZ != info.LastNetworkRotZ;

        if (posChanged || rotChanged)
        {
            // Store rotations in degrees to avoid per-frame radian conversion in Interpolate().
            info.Interpolation.AddNetworkPacket(new PlayerInterpolationState
            {
                PositionX = netX,
                PositionY = netY,
                PositionZ = netZ,
                RotX = float.RadiansToDegrees(net.RotX),
                RotY = float.RadiansToDegrees(net.RotY),
                RotZ = float.RadiansToDegrees(net.RotZ),
            }, Game.TotalTimeMilliseconds);
        }

        PlayerInterpolationState cur =
          (PlayerInterpolationState)info.Interpolation.InterpolatedState(Game.TotalTimeMilliseconds)
            ?? new PlayerInterpolationState();

        info.Velocity = new(
            cur.PositionX - info.LastCurPosX,
            cur.PositionY - info.LastCurPosY,
            cur.PositionZ - info.LastCurPosZ);

        info.Moves = !(cur.PositionX == info.LastCurPosX
                    && cur.PositionY == info.LastCurPosY
                    && cur.PositionZ == info.LastCurPosZ);

        info.LastCurPosX = cur.PositionX;
        info.LastCurPosY = cur.PositionY;
        info.LastCurPosZ = cur.PositionZ;
        info.LastNetworkPosX = netX;
        info.LastNetworkPosY = netY;
        info.LastNetworkPosZ = netZ;
        info.LastNetworkRotX = net.RotX;
        info.LastNetworkRotY = net.RotY;
        info.LastNetworkRotZ = net.RotZ;

        e.Position.X = cur.PositionX;
        e.Position.Y = cur.PositionY;
        e.Position.Z = cur.PositionZ;
        // Convert degrees back to radians only once, here at the apply site.
        e.Position.RotX = float.DegreesToRadians(cur.RotX);
        e.Position.RotY = float.DegreesToRadians(cur.RotY);
        e.Position.RotZ = float.DegreesToRadians(cur.RotZ);
    }
}

/// <summary>
/// Interpolates <see cref="PlayerInterpolationState"/> snapshots.
/// Rotations are stored and interpolated in degrees; conversion to/from
/// radians happens only at the network-receive and position-apply sites,
/// not on every interpolation call.
/// </summary>
public class PlayerInterpolate : IInterpolation
{
    public IInterpolatedObject Interpolate(IInterpolatedObject a, IInterpolatedObject b, float progress)
    {
        PlayerInterpolationState aa = (PlayerInterpolationState)a;
        PlayerInterpolationState bb = (PlayerInterpolationState)b;

        return new PlayerInterpolationState
        {
            PositionX = aa.PositionX + ((bb.PositionX - aa.PositionX) * progress),
            PositionY = aa.PositionY + ((bb.PositionY - aa.PositionY) * progress),
            PositionZ = aa.PositionZ + ((bb.PositionZ - aa.PositionZ) * progress),
            // Rotations are already in degrees — no conversion needed here.
            RotX = AngleInterpolation.InterpolateAngle360(aa.RotX, bb.RotX, progress),
            RotY = AngleInterpolation.InterpolateAngle360(aa.RotY, bb.RotY, progress),
            RotZ = AngleInterpolation.InterpolateAngle360(aa.RotZ, bb.RotZ, progress),
        };
    }
}

public class PlayerInterpolationState : IInterpolatedObject
{
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public float PositionZ { get; set; }
    public float RotX { get; set; }
    public float RotY { get; set; }
    public float RotZ { get; set; }
}