using OpenTK.Mathematics;

/// <summary>
/// Updates grenade physics including gravity, movement, and collision bounce each fixed frame.
/// </summary>
public class ModGrenade : ModBase
{
    private const float ProjectileGravity = 20f;
    private const float BounceSpeedMultiply = 0.5f;
    private const float WallDistance = 0.3f;

    public ModGrenade(IGame game) : base(game)
    {
    }

    public override void OnUpdate(float args)
    {
        for (int i = 0; i < Game.Entities.Count; i++)
        {
            Entity entity = Game.Entities[i];
            if (entity?.Grenade == null)
            {
                continue;
            }

            UpdateGrenade(i, args);
        }
    }

    private void UpdateGrenade(int grenadeEntityId, float dt)
    {
        Entity grenadeEntity = Game.Entities[grenadeEntityId];
        Sprite grenadeSprite = grenadeEntity.Sprite;
        Grenade grenade = grenadeEntity.Grenade;

        if (grenade.Settled) return;

        // Fix gravity ordering — update velocity first, then integrate
        grenade.VelocityY -= ProjectileGravity * dt;

        Vector3 velocity = new(grenade.VelocityX, grenade.VelocityY, grenade.VelocityZ);

        // Substep to prevent tunneling — cap movement to half a block per step
        const float maxStepSize = 0.4f;
        float totalDist = velocity.Length * dt;
        int steps = Math.Max(1, (int)MathF.Ceiling(totalDist / maxStepSize));
        float subDt = dt / steps;

        Vector3 pos = new(grenadeSprite.PositionX, grenadeSprite.PositionY, grenadeSprite.PositionZ);

        for (int s = 0; s < steps; s++)
        {
            Vector3 newPos = pos + velocity * subDt;
            pos = GrenadeBounce(pos, newPos, ref velocity, subDt);
        }

        // Resting state — stop simulating if barely moving
        if (velocity.LengthSquared < 0.05f)
        {
            velocity = Vector3.Zero;
            grenade.Settled = true;
        }

        grenade.VelocityX = velocity.X;
        grenade.VelocityY = velocity.Y;
        grenade.VelocityZ = velocity.Z;
        grenadeSprite.PositionX = pos.X;
        grenadeSprite.PositionY = pos.Y;
        grenadeSprite.PositionZ = pos.Z;
    }

    internal Vector3 GrenadeBounce(Vector3 oldPos, Vector3 newPos, ref Vector3 velocity, float dt)
    {
        bool isMoving = velocity.Length > 100 * dt;

        oldPos.Y += WallDistance;
        newPos.Y += WallDistance;

        Vector3 pos = newPos;

        // Left (+Z)
        if (newPos.Z > oldPos.Z)
        {
            TryBounceAxis(newPos, new Vector3(0, 0, WallDistance), ref velocity, ref pos, isMoving, axis: 2);
        }

        // Right (-Z)
        if (newPos.Z < oldPos.Z)
        {
            TryBounceAxis(newPos, new Vector3(0, 0, -WallDistance), ref velocity, ref pos, isMoving, axis: 2);
        }
        // Front (+X)
        if (newPos.X > oldPos.X)
        {
            TryBounceAxis(newPos, new Vector3(WallDistance, 0, 0), ref velocity, ref pos, isMoving, axis: 0);
        }

        // Back (-X)
        if (newPos.X < oldPos.X)
        {
            TryBounceAxis(newPos, new Vector3(-WallDistance, 0, 0), ref velocity, ref pos, isMoving, axis: 0);
        }
        // Bottom (falling down)
        if (newPos.Y < oldPos.Y)
        {
            TryBounceFloor(newPos, oldPos, ref velocity, ref pos, isMoving);
        }

        // Top (moving up)
        if (newPos.Y > oldPos.Y)
        {
            TryBounceCeiling(newPos, ref velocity, ref pos, isMoving);
        }

        pos.Y -= WallDistance;
        return pos;
    }

    /// <summary>Checks and applies a bounce for X or Z axis wall collisions.</summary>
    private void TryBounceAxis(Vector3 newPos, Vector3 offset, ref Vector3 velocity, ref Vector3 pos, bool isMoving, int axis)
    {
        Vector3 probe = newPos + offset;
        int px = (int)MathF.Floor(probe.X);
        int py = (int)MathF.Floor(probe.Z);
        int pz = (int)MathF.Floor(probe.Y);

        bool empty = Game.IsTileEmptyForPhysics(px, py, pz)
                  && Game.IsTileEmptyForPhysics(px, py, pz + 1);
        if (empty) return;

        velocity[axis] = -velocity[axis];
        ApplyBounce(ref velocity, newPos, isMoving);

        // Push out — move back to the face of the block we hit
        pos[axis] = offset[axis] > 0
            ? MathF.Floor(probe[axis == 0 ? 0 : 2]) - WallDistance
            : MathF.Ceiling(probe[axis == 0 ? 0 : 2]) + WallDistance;
    }

    /// <summary>Checks and applies a bounce when the grenade hits a floor (moving down).</summary>
    private void TryBounceFloor(Vector3 newPos, Vector3 oldPos, ref Vector3 velocity, ref Vector3 pos, bool isMoving)
    {
        float a = WallDistance;
        Vector3 probe = new(newPos.X, newPos.Y - WallDistance, newPos.Z);
        int x = (int)MathF.Floor(probe.X);
        int y = (int)MathF.Floor(probe.Z);
        int z = (int)MathF.Floor(probe.Y);

        float fracX = probe.X - x;
        float fracZ = probe.Z - y;

        bool full = !Game.IsTileEmptyForPhysics(x, y, z)
            || (fracX <= a && !Game.IsTileEmptyForPhysics(x - 1, y, z) && Game.IsTileEmptyForPhysics(x - 1, y, z + 1))
            || (fracX >= 1 - a && !Game.IsTileEmptyForPhysics(x + 1, y, z) && Game.IsTileEmptyForPhysics(x + 1, y, z + 1))
            || (fracZ <= a && !Game.IsTileEmptyForPhysics(x, y - 1, z) && Game.IsTileEmptyForPhysics(x, y - 1, z + 1))
            || (fracZ >= 1 - a && !Game.IsTileEmptyForPhysics(x, y + 1, z) && Game.IsTileEmptyForPhysics(x, y + 1, z + 1));

        if (!full)
        {
            return;
        }

        velocity.Y = -velocity.Y;
        ApplyBounce(ref velocity, newPos, isMoving);
    }

    /// <summary>Checks and applies a bounce when the grenade hits a ceiling (moving up).</summary>
    private void TryBounceCeiling(Vector3 newPos, ref Vector3 velocity, ref Vector3 pos, bool isMoving)
    {
        Vector3 probe = new(newPos.X, newPos.Y + WallDistance, newPos.Z);
        bool empty = Game.IsTileEmptyForPhysics(
            (int)MathF.Floor(probe.X),
            (int)MathF.Floor(probe.Z),
            (int)MathF.Floor(probe.Y));

        if (empty)
        {
            return;
        }

        velocity.Y = -velocity.Y;
        ApplyBounce(ref velocity, newPos, isMoving);
    }

    /// <summary>Applies bounce speed damping and plays bounce sound if the grenade is moving.</summary>
    private void ApplyBounce(ref Vector3 velocity, Vector3 pos, bool isMoving)
    {
        velocity *= BounceSpeedMultiply;
        if (isMoving)
        {
            Game.PlayAudioAt("grenadebounce.ogg", pos.X, pos.Y, pos.Z);
        }
    }
}