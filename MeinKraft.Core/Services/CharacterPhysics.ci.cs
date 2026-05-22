using MeinKraft;
using OpenTK.Mathematics;

/// <summary>
/// Defines the per-frame fixed-timestep update contract for entity behaviour scripts.
/// </summary>
public interface IEntityScript
{
    /// <summary>Called once per fixed physics tick for the given <paramref name="entity"/>.</summary>
    void OnNewFrameFixed(int entity, float dt);
}

/// <summary>
/// Implements walking, jumping, swimming, and collision-response physics for the local player.
/// Runs each fixed timestep via <see cref="IEntityScript.OnNewFrameFixed"/>.
//This is the player movement and physics engine. Every game tick it:
//Reads your keyboard / controller input (WASD, jump, shift)
//Applies gravity, drag, and friction to your velocity
//Converts your input into world-space movement
//Slides you along walls instead of stopping dead on collision
//Detects if you're on the ground, in water, on ice, or on a trampoline

//Special cases it handles: swimming(slower gravity), jumping(including half - jumps), trampoline bouncing, half-blocks you can step up onto, noclip mode, and being pushed by external forces like explosions.
/// </summary>
public class ScriptCharacterPhysics : IEntityScript
{
    // ── Constructor ───────────────────────────────────────────────────────────

    public ScriptCharacterPhysics(IVoxelMap voxelMap, IBlockRegistry blockTypeRegistry, IGame game)
    {
        this.game = game;
        this.voxelMap = voxelMap;
        this.blockTypeRegistry = blockTypeRegistry;
        // Only non-default values need explicit initialisation.
        // (movedz, curspeed, jumpacceleration, isplayeronground, etc. are
        //  already zero/false by C# default for their types.)
        acceleration.SetDefault();
        constGravity = 0.3f;
        constWaterGravityMultiplier = 3;
        constEnableAcceleration = true;
        constJump = 2.1f;
    }

    // ── Dependencies ──────────────────────────────────────────────────────────

    /// <summary>Reference to the active game instance, assigned at the start of each tick.</summary>
    private readonly IGame game;
    private readonly IVoxelMap voxelMap;
    private readonly IBlockRegistry blockTypeRegistry;

    // ── Per-frame physics state ───────────────────────────────────────────────

    /// <summary>Current vertical velocity (positive = upward). Modified by gravity and jumps each tick.</summary>
    private float movedz;

    /// <summary>Current XYZ movement speed vector, attenuated by <see cref="acceleration"/> each tick.</summary>
    private Vector3 curspeed;

    /// <summary>Remaining upward acceleration from an in-progress jump, halved each tick until exhausted.</summary>
    private float jumpacceleration;

    /// <summary>True when the player is standing on solid ground this tick.</summary>
    private bool isplayeronground;

    /// <summary>Tunable acceleration parameters (drag, friction, responsiveness) for the current tick.</summary>
    private Acceleration acceleration;

    /// <summary>Full-height jump initial acceleration, derived from <see cref="constGravity"/> each tick.</summary>
    private float jumpstartacceleration;

    /// <summary>Half-height jump initial acceleration (crouch-jump / swim surfacing).</summary>
    private float jumpstartaccelerationhalf;

    /// <summary>Effective movement speed this tick, sourced from <see cref="Game.MoveSpeedNow"/>.</summary>
    private float movespeednow;

    // ── Tunable constants ─────────────────────────────────────────────────────

    /// <summary>Base gravitational acceleration applied each tick.</summary>
    private readonly float constGravity;

    /// <summary>Factor by which gravity is multiplied while swimming.</summary>
    private readonly float constWaterGravityMultiplier;

    /// <summary>When false, <see cref="curspeed"/> is set directly from input rather than ramped.</summary>
    private readonly bool constEnableAcceleration;

    /// <summary>Multiplier applied to <see cref="jumpacceleration"/> each tick to produce upward displacement.</summary>
    private readonly float constJump;

    // ── IEntityScript ─────────────────────────────────────────────────────────

    /// <summary>
    /// Entry point called by the entity system each fixed timestep.
    /// Reads player input, resolves move speed, and delegates to <see cref="Update"/>.
    /// Does nothing while the map-loading screen is active.
    /// </summary>
    public void OnNewFrameFixed(int entity, float dt)
    {
        if (game.GuiState == GameState.MapLoading)
        {
            return;
        }

        movespeednow = game.MoveSpeedNow();
        game.Controls.MovedX = Math.Clamp(game.Controls.MovedX, -1, 1);
        game.Controls.MovedY = Math.Clamp(game.Controls.MovedY, -1, 1);

        jumpstartacceleration = 13.333f * constGravity;
        jumpstartaccelerationhalf = 9f * constGravity;
        acceleration.SetDefault();
        game.soundnow = false; 

        // ── Follow mode: suppress local input ────────────────────────────────
        // FollowId() does a linear entity scan — call once and cache.
        int? followId = game.FollowId();
        Controls move = game.Controls;
        if (followId != null && followId == game.LocalPlayerId)
        {
            move.MovedX = 0;
            move.MovedY = 0;
            move.MoveUp = false;
            move.WantsJump = false;
        }

        Update(game.Player.Position, move, dt,
            out bool soundNow,
            new Vector3(game.PushX, game.PushY, game.PushZ),
            game.Entities[game.LocalPlayerId].DrawModel.ModelHeight);

        game.soundnow = soundNow;
    }

    // ── Core physics update ───────────────────────────────────────────────────

    /// <summary>
    /// Applies gravity, input forces, acceleration, collision response, and jump logic
    /// to <paramref name="stateplayerposition"/> for a single fixed timestep.
    /// </summary>
    /// <param name="stateplayerposition">Player position mutated in-place.</param>
    /// <param name="move">Input snapshot for this tick.</param>
    /// <param name="dt">Fixed timestep duration in seconds.</param>
    /// <param name="soundnow">Set to true when the player initiates a jump (footstep sound cue).</param>
    /// <param name="push">External impulse vector (e.g. explosions, conveyors).</param>
    /// <param name="modelheight">Entity model height in blocks; values ≥ 2 enable tall-player collision.</param>
    public void Update(EntityPosition stateplayerposition, Controls move, float dt,
        out bool soundnow, Vector3 push, float modelheight)
    {
        if (game.StopPlayerMove)
        {
            movedz = 0;
            game.StopPlayerMove = false;
        }

        // ── Cache per-tick queries called multiple times ───────────────────────
        bool swimmingBody = game.SwimmingBody();
        int blockUnder = game.BlockUnderPlayer();

        // ── Air control: high drag + low ramp-up while airborne ───────────────
        if (!isplayeronground)
        {
            acceleration.acceleration1 = 0.99f;
            acceleration.acceleration2 = 0.2f;
            acceleration.acceleration3 = 70;
        }

        // ── Trampoline: force a super-jump on landing ─────────────────────────
        if (blockUnder != -1
            && blockUnder == blockTypeRegistry.BlockIdTrampoline
            && !isplayeronground
            && !game.Controls.ShiftKeyDown)
        {
            game.Controls.WantsJump = true;
            jumpstartacceleration = 20.666f * constGravity;
        }

        // ── Ice / water: slippery acceleration profile ────────────────────────
        if ((blockUnder != -1 && blockTypeRegistry.IsSlipperyWalk[blockUnder]) || swimmingBody)
        {
            acceleration.acceleration1 = 0.99f;
            acceleration.acceleration2 = 0.2f;
            acceleration.acceleration3 = 70;
        }

        soundnow = false;

        // ── Convert input from player-local to world space ────────────────────
        Vector3 diff1 = new();
        VectorUtils.ToVectorInFixedSystem(
            move.MovedX * movespeednow * dt,
            0,
            move.MovedY * movespeednow * dt,
            stateplayerposition.RotX, stateplayerposition.RotY, ref diff1);

        // ── Apply normalised external push ────────────────────────────────────
        // Use LengthSquared to avoid sqrt for the magnitude check.
        if (push.LengthSquared > 0.0001f) // equivalent to Length > 0.01f
        {
            push = Vector3.Normalize(push);
            push.X *= 5;
            push.Y *= 5;
            push.Z *= 5;
        }

        diff1.X += push.X * dt;
        diff1.Y += push.Y * dt;
        diff1.Z += push.Z * dt;

        // ── Gravity: only after the chunk under the player has arrived ─────────
        bool loaded = false;
        int cx = (int)(game.Player.Position.X / GameConstants.CHUNK_SIZE);
        int cy = (int)(game.Player.Position.Y / GameConstants.CHUNK_SIZE);
        int cz = (int)(game.Player.Position.Z / GameConstants.CHUNK_SIZE);
        if (voxelMap.IsValidChunkPos(cx, cy, cz))
        {
            // Use cached chunk-count properties instead of recomputing / BlockConstants.CHUNK_SIZE.
            if (voxelMap.Chunks[VectorIndexUtil.Index3d(
                    cx, cy, cz,
                    voxelMap.Mapsizexchunks,
                    voxelMap.Mapsizeychunks)] != null)
            {
                loaded = true;
            }
        }
        else
        {
            loaded = true;
        }

        if (!move.FreeMove && loaded)
        {
            movedz += swimmingBody
                ? -constGravity * constWaterGravityMultiplier
                : -constGravity;
        }

        game.MovedZ = movedz;

        if (constEnableAcceleration)
        {
            // Drag
            curspeed.X *= acceleration.acceleration1;
            curspeed.Y *= acceleration.acceleration1;
            curspeed.Z *= acceleration.acceleration1;
            // Friction (frame-rate independent)
            curspeed.X = MakeCloserToZero(curspeed.X, acceleration.acceleration2 * dt);
            curspeed.Y = MakeCloserToZero(curspeed.Y, acceleration.acceleration2 * dt);
            curspeed.Z = MakeCloserToZero(curspeed.Z, acceleration.acceleration2 * dt);
            // Fly / swim vertical input
            diff1.Y += move.MoveUp ? 2 * movespeednow * dt : 0;
            diff1.Y -= move.MoveDown ? 2 * movespeednow * dt : 0;
            // Force ramp-up
            curspeed.X += diff1.X * acceleration.acceleration3 * dt;
            curspeed.Y += diff1.Y * acceleration.acceleration3 * dt;
            curspeed.Z += diff1.Z * acceleration.acceleration3 * dt;
            // Clamp to max speed — LengthSquared avoids sqrt for the comparison.
            if (curspeed.LengthSquared > movespeednow * movespeednow)
            {
                curspeed = Vector3.Normalize(curspeed);
                curspeed.X *= movespeednow;
                curspeed.Y *= movespeednow;
                curspeed.Z *= movespeednow;
            }
        }
        else
        {
            // Instant response: no ramp-up.
            // LengthSquared avoids sqrt for the zero-check.
            if (diff1.LengthSquared > 0)
            {
                diff1 = Vector3.Normalize(diff1);
            }

            curspeed.X = diff1.X * movespeednow;
            curspeed.Y = diff1.Y * movespeednow;
            curspeed.Z = diff1.Z * movespeednow;
        }

        Vector3 newposition = Vector3.Zero;
        if (!move.FreeMove)
        {
            newposition.X = stateplayerposition.X + curspeed.X;
            newposition.Y = stateplayerposition.Y + curspeed.Y;
            newposition.Z = stateplayerposition.Z + curspeed.Z;
            // Horizontal-only movement when not swimming (vertical handled by movedz).
            if (!swimmingBody)
            {
                newposition.Y = stateplayerposition.Y;
            }

            // Re-normalise horizontal displacement then scale by actual speed.
            float diffx = newposition.X - stateplayerposition.X;
            float diffy = newposition.Y - stateplayerposition.Y;
            float diffz = newposition.Z - stateplayerposition.Z;
            float difflength = MathF.Sqrt((diffx * diffx) + (diffy * diffy) + (diffz * diffz));
            if (difflength > 0)
            {
                float inv = curspeed.Length / difflength;
                diffx *= inv;
                diffy *= inv;
                diffz *= inv;
            }

            newposition.X = stateplayerposition.X + (diffx * dt);
            newposition.Y = stateplayerposition.Y + (diffy * dt);
            newposition.Z = stateplayerposition.Z + (diffz * dt);
        }
        else
        {
            newposition.X = stateplayerposition.X + (curspeed.X * dt);
            newposition.Y = stateplayerposition.Y + (curspeed.Y * dt);
            newposition.Z = stateplayerposition.Z + (curspeed.Z * dt);
        }

        // Apply accumulated vertical velocity (gravity + jump).
        newposition.Y += movedz * dt;

        if (!move.NoClip)
        {
            Vector3 v = WallSlide(
                new Vector3(stateplayerposition.X, stateplayerposition.Y, stateplayerposition.Z),
                newposition,
                modelheight);
            stateplayerposition.X = v.X;
            stateplayerposition.Y = v.Y;
            stateplayerposition.Z = v.Z;
        }
        else
        {
            stateplayerposition.X = newposition.X;
            stateplayerposition.Y = newposition.Y;
            stateplayerposition.Z = newposition.Z;
        }

        if (!move.FreeMove)
        {
            if (isplayeronground || swimmingBody)
            {
                jumpacceleration = 0;
                movedz = 0;
            }

            if ((move.WantsJump || move.WantsJumpHalf)
                && (jumpacceleration == 0 && isplayeronground || swimmingBody)
                && loaded
                && !game.SwimmingEyes())
            {
                jumpacceleration = move.WantsJumpHalf
                    ? jumpstartaccelerationhalf
                    : jumpstartacceleration;
                soundnow = true;
            }

            if (jumpacceleration > 0)
            {
                isplayeronground = false;
                jumpacceleration /= 2;
            }

            movedz += jumpacceleration * constJump;
        }
        else
        {
            isplayeronground = true;
        }

        game.IsPlayerOnGround = isplayeronground;
    }

    // ── Collision helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns true when the block at the given block-space coordinates does not
    /// impede player movement (air, fluid, rail, or out-of-bounds above the map).
    /// </summary>
    private bool IsTileEmptyForPhysics(int x, int y, int z)
    {
        if (z >= voxelMap.MapSizeZ)
        {
            return true;
        }

        if (x < 0 || y < 0 || z < 0)
        {
            return false;
        }

        if (x >= voxelMap.MapSizeX || y >= voxelMap.MapSizeY)
        {
            return false;
        }

        int block = voxelMap.GetBlockValid(x, y, z);
        if (block == 0)
        {
            return true;
        }

        BlockType blocktype = blockTypeRegistry.BlockTypes[block];
        return blocktype.WalkableType == WalkableType.Fluid
            || BlockType.IsEmptyForPhysics(blocktype)
            || IsRail(blocktype);
    }

    /// <summary>Reusable scratch vector used inside <see cref="WallSlide"/> to avoid stack allocation per call.</summary>
    private Vector3 tmpPlayerPosition;

    /// <summary>
    /// Moves the player from <paramref name="oldposition"/> toward <paramref name="newposition"/>
    /// one axis at a time, stopping each axis independently on collision.
    /// Also detects ground contact, walls for auto-jump, and half-block step-ups.
    /// </summary>
    private Vector3 WallSlide(Vector3 oldposition, Vector3 newposition, float modelheight)
    {
        bool high = modelheight >= 2;

        oldposition.Y += game.WallDistance;
        newposition.Y += game.WallDistance;

        game.ReachedWall = false;
        game.ReachedWall1BlockHigh = false;
        game.ReachedHalfBlock = false;

        tmpPlayerPosition.X = oldposition.X;
        tmpPlayerPosition.Y = oldposition.Y;
        tmpPlayerPosition.Z = oldposition.Z;

        // X axis
        if (IsEmptySpaceForPlayer(high, newposition.X, tmpPlayerPosition.Y, tmpPlayerPosition.Z, out int tmpBlockingBlockType))
        {
            tmpPlayerPosition.X = newposition.X;
        }
        else
        {
            game.ReachedWall = true;
            if (IsEmptyPoint(newposition.X, tmpPlayerPosition.Y + 0.5f, tmpPlayerPosition.Z, out _))
            {
                game.ReachedWall1BlockHigh = true;
                if (blockTypeRegistry.BlockTypes[tmpBlockingBlockType].DrawType == DrawType.HalfHeight)
                {
                    game.ReachedHalfBlock = true;
                }

                if (StandingOnHalfBlock(newposition.X, tmpPlayerPosition.Y, tmpPlayerPosition.Z))
                {
                    game.ReachedHalfBlock = true;
                }
            }
        }

        // Y axis
        if (IsEmptySpaceForPlayer(high, tmpPlayerPosition.X, newposition.Y, tmpPlayerPosition.Z, out _))
        {
            tmpPlayerPosition.Y = newposition.Y;
        }

        // Z axis
        if (IsEmptySpaceForPlayer(high, tmpPlayerPosition.X, tmpPlayerPosition.Y, newposition.Z, out tmpBlockingBlockType))
        {
            tmpPlayerPosition.Z = newposition.Z;
        }
        else
        {
            game.ReachedWall = true;
            if (IsEmptyPoint(tmpPlayerPosition.X, tmpPlayerPosition.Y + 0.5f, newposition.Z, out _))
            {
                game.ReachedWall1BlockHigh = true;
                if (blockTypeRegistry.BlockTypes[tmpBlockingBlockType].DrawType == DrawType.HalfHeight)
                {
                    game.ReachedHalfBlock = true;
                }

                if (StandingOnHalfBlock(tmpPlayerPosition.X, tmpPlayerPosition.Y, newposition.Z))
                {
                    game.ReachedHalfBlock = true;
                }
            }
        }

        // Ground detection: Y did not advance toward the desired lower position.
        isplayeronground = tmpPlayerPosition.Y == oldposition.Y && newposition.Y < oldposition.Y;

        tmpPlayerPosition.Y -= game.WallDistance;
        return tmpPlayerPosition;
    }

    private bool StandingOnHalfBlock(float x, float y, float z)
    {
        int under = voxelMap.GetBlock((int)x, (int)z, (int)y);
        return blockTypeRegistry.BlockTypes[under].DrawType == DrawType.HalfHeight;
    }

    private bool IsEmptySpaceForPlayer(bool high, float x, float y, float z, out int blockingBlockType)
    {
        return IsEmptyPoint(x, y, z, out blockingBlockType)
            && IsEmptyPoint(x, y + 1, z, out blockingBlockType)
            && (!high || IsEmptyPoint(x, y + 2, z, out blockingBlockType));
    }

    private bool IsEmptyPoint(float x, float y, float z, out int blockingBlocktype)
    {
        float r = game.WallDistance;
        // 8 corners of the AABB centred on (x, y, z)
        float[] xs = { x - r, x + r };
        float[] ys = { y - r, y + r };
        float[] zs = { z - r, z + r };

        foreach (float cx in xs)
        {
            foreach (float cy in ys)
            {
                foreach (float cz in zs)
                {
                    if (!IsTileEmptyForPhysics((int)cx, (int)cz, (int)cy))
                    {
                        blockingBlocktype = voxelMap.GetBlock((int)cx, (int)cz, (int)cy);
                        return false;
                    }
                }
            }
        }

        blockingBlocktype = 0;
        return true;
    }

    // ── Static helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the Chebyshev distance between a point and an AABB.
    /// Returns 0 when the point is inside the box.
    /// </summary>
    public static float BoxPointDistance(
        float minX, float minY, float minZ,
        float maxX, float maxY, float maxZ,
        float pX, float pY, float pZ)
    {
        float dx = Max3(minX - pX, 0, pX - maxX);
        float dy = Max3(minY - pY, 0, pY - maxY);
        float dz = Max3(minZ - pZ, 0, pZ - maxZ);
        return Max3(dx, dy, dz);
    }

    /// <summary>
    /// Moves <paramref name="a"/> toward zero by at most <paramref name="b"/>, never crossing zero.
    /// Used for friction — decelerates a speed component without reversing it.
    /// </summary>
    public static float MakeCloserToZero(float a, float b)
    {
        if (a > 0)
        {
            return Math.Max(a - b, 0);
        }
        else
        {
            return Math.Min(a + b, 0);
        }
    }

    /// <summary>Returns the largest of three float values.</summary>
    private static float Max3(float a, float b, float c) => Math.Max(Math.Max(a, b), c);

    /// <summary>Returns true when <paramref name="block"/> has a non-zero rail value.</summary>
    public static bool IsRail(BlockType block) => block.Rail > 0;
}

/// <summary>
/// Tunable acceleration parameters governing drag, friction, and movement responsiveness.
/// Stored as a struct — embedded directly in <see cref="ScriptCharacterPhysics"/> with
/// no separate heap allocation. Reset to defaults each physics tick before
/// surface/air overrides are applied.
/// </summary>
public struct Acceleration
{
    /// <summary>Per-tick velocity multiplier (drag). Values below 1 bleed off speed over time.</summary>
    internal float acceleration1;

    /// <summary>Flat per-second deceleration toward zero (friction), scaled by dt.</summary>
    internal float acceleration2;

    /// <summary>Force multiplier applied to input direction, scaled by dt (movement responsiveness).</summary>
    internal float acceleration3;

    /// <summary>
    /// Restores normal ground-movement values:
    /// moderate drag (0.9), light friction (2), high responsiveness (700).
    /// </summary>
    public void SetDefault()
    {
        acceleration1 = 0.9f;
        acceleration2 = 2f;
        acceleration3 = 700f;
    }
}

/// <summary>
/// Snapshot of player input for a single physics tick.
/// Stored as a struct so that <c>Controls move = game.controls</c> produces
/// a cheap copy — local modifications (e.g. zeroing movement in follow mode)
/// do not affect the original <c>game.controls</c> state.
/// </summary>
public class Controls
{
    /// <summary>Lateral strafe input in [-1, 1] (negative = left, positive = right).</summary>
    public float MovedX { get; set; }

    /// <summary>Forward/backward input in [-1, 1] (negative = back, positive = forward).</summary>
    public float MovedY { get; set; }

    /// <summary>True when the player pressed the jump key this tick.</summary>
    public bool WantsJump { get; set; }

    /// <summary>True when a reduced-height jump was requested (e.g. crouch-jump).</summary>
    public bool WantsJumpHalf { get; set; }

    /// <summary>True while the fly/swim ascend key is held.</summary>
    public bool MoveUp { get; set; }

    /// <summary>True while the fly/swim descend key is held.</summary>
    public bool MoveDown { get; set; }

    /// <summary>True while the shift (sneak) key is held; suppresses trampoline super-jump.</summary>
    public bool ShiftKeyDown { get; set; }

    /// <summary>When true, gravity is disabled and the player moves freely in all three axes.</summary>
    public bool FreeMove { get; set; }

    /// <summary>When true, collision detection is disabled (no-clip / ghost mode).</summary>
    public bool NoClip { get; set; }
}