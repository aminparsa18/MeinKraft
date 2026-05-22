using OpenTK.Mathematics;

/// <inheritdoc/>
public sealed class CameraService : ICameraService
{
    private float _distance;
    private float _angle;
    private float _azimuth;

    private readonly float _minimumDistance;
    private readonly float _minimumAngle;
    private readonly float _maximumAngle;

    public CameraService(
        float initialDistance = 5f,
        float initialAngle = 45f,
        float initialAzimuth = 0f,
        float minimumDistance = 2f,
        float minimumAngle = 0f,
        float maximumAngle = 89f)
    {
        _minimumDistance = minimumDistance;
        _minimumAngle = minimumAngle;
        _maximumAngle = maximumAngle;
        _distance = MathF.Max(initialDistance, minimumDistance);
        _angle = Math.Clamp(initialAngle, minimumAngle, maximumAngle);
        _azimuth = initialAzimuth;
        Center = Vector3.Zero;
    }

    /// <inheritdoc/>
    public Vector3 Center { get; set; }
    public float OverHeadCameraDistance { get; set; } = 10;
    public BlockOctreeSearcher BlockOctreeSearcher { get; } = new();

    /// <inheritdoc/>
    public void GetPosition(ref Vector3 ret)
    {
        float flatDist = FlatDistance();
        ret.X = (MathF.Cos(_azimuth) * flatDist) + Center.X;
        ret.Y = Center.Y + HeightFromCenter();
        ret.Z = (MathF.Sin(_azimuth) * flatDist) + Center.Z;
    }

    /// <inheritdoc/>
    public void Move(CameraMoveArgs args, float dt)
    {
        float turn = dt * 4f;
        float tilt = dt * 40f;

        if (args.TurnLeft)
        {
            TurnLeft(turn);
        }

        if (args.TurnRight)
        {
            TurnRight(turn);
        }

        if (args.DistanceUp)
        {
            _distance += turn;
        }

        if (args.DistanceDown)
        {
            _distance -= turn;
        }

        if (args.AngleUp)
        {
            TurnUp(tilt);
        }

        if (args.AngleDown)
        {
            TurnDown(tilt);
        }

        _distance = MathF.Max(args.Distance, _minimumDistance);
    }

    /// <inheritdoc/>
    public void TurnLeft(float delta) => _azimuth += delta;

    /// <inheritdoc/>
    public void TurnRight(float delta) => _azimuth -= delta;

    /// <inheritdoc/>
    public void TurnUp(float delta) => _angle = Math.Clamp(_angle + delta, _minimumAngle, _maximumAngle);

    /// <inheritdoc/>
    public void TurnDown(float delta) => _angle = Math.Clamp(_angle - delta, _minimumAngle, _maximumAngle);

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>Vertical offset of the camera above <see cref="Center"/>.</summary>
    private float HeightFromCenter()
        => MathF.Sin(_angle * MathF.PI / 180f) * _distance;

    /// <summary>Horizontal (XZ-plane) distance from <see cref="Center"/> to the camera eye.</summary>
    private float FlatDistance()
        => MathF.Cos(_angle * MathF.PI / 180f) * _distance;
}