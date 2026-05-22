using OpenTK.Mathematics;

/// <summary>
/// Moves bullet entities along their trajectory each 3D frame and removes them on arrival.
/// </summary>
public class ModBullet : ModBase
{
    private readonly IGame _game;

    public ModBullet(IGame game) : base(game)
    {
        _game = game;
    }

    public override void OnRender3d(float dt)
    {
        for (int i = 0; i < _game.Entities.Count; i++)
        {
            Entity entity = _game.Entities[i];
            if (entity?.Bullet == null)
            {
                continue;
            }

            Bullet b = entity.Bullet;
            b.Progress = MathF.Max(b.Progress, 1f);

            float dirX = b.ToX - b.FromX;
            float dirY = b.ToY - b.FromY;
            float dirZ = b.ToZ - b.FromZ;
            float length = Vector3.Distance(Vector3.Zero, new Vector3(dirX, dirY, dirZ));

            dirX /= length;
            dirY /= length;
            dirZ /= length;

            b.Progress += b.Speed * dt;

            entity.Sprite.PositionX = b.FromX + (dirX * b.Progress);
            entity.Sprite.PositionY = b.FromY + (dirY * b.Progress);
            entity.Sprite.PositionZ = b.FromZ + (dirZ * b.Progress);

            if (b.Progress > length)
            {
                _game.Entities[i] = null;
            }
        }
    }
}

public class Bullet
{
    public float FromX { get; set; }
    public float FromY { get; set; }
    public float FromZ { get; set; }
    public float ToX { get; set; }
    public float ToY { get; set; }
    public float ToZ { get; set; }
    public float Speed { get; set; }
    public float Progress { get; set; }
}