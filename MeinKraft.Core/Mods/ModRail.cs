using OpenTK.Mathematics;
using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;

/// <summary>
/// Handles minecart rail riding: entering and exiting a minecart, movement along
/// rail tracks (including slopes and corners), speed control, direction reversal,
/// and rail sound effects.
/// </summary>
public class ModRail : ModBase
{
    /// <summary>
    /// Height above the rail block at which the player sits inside the minecart
    /// (0.5 blocks by default, adjusted by <see cref="MinecartHeight"/>).
    /// </summary>
    private readonly float _railHeight;

    /// <summary>The local minecart entity added to the world when rail riding begins.</summary>
    internal Entity localMinecart;

    /// <summary>Rail map utility used to query slope and direction data.</summary>
    internal RailMapUtil d_RailMapUtil;

    /// <summary>Player eye height saved when entering a minecart, restored on exit.</summary>
    internal float originalmodelheight;

    /// <summary><see langword="true"/> while the player is riding a rail.</summary>
    internal bool railriding;

    /// <summary>Current forward speed of the minecart in blocks per second.</summary>
    internal float currentvehiclespeed;

    /// <summary>Chunk-grid X coordinate of the rail block the minecart currently occupies.</summary>
    internal int currentrailblockX;

    /// <summary>Chunk-grid Y coordinate of the rail block the minecart currently occupies.</summary>
    internal int currentrailblockY;

    /// <summary>Chunk-grid Z coordinate of the rail block the minecart currently occupies.</summary>
    internal int currentrailblockZ;

    /// <summary>
    /// Progress through the current rail block in the range [0, 1).
    /// Incremented each frame by <c>speed × dt</c>.
    /// </summary>
    internal float currentrailblockprogress;

    /// <summary>The direction in which the minecart is currently travelling.</summary>
    internal VehicleDirection12 currentdirection;

    /// <summary>The direction the minecart was travelling in the previous block.</summary>
    internal VehicleDirection12 lastdirection;

    /// <summary>Whether Q was held on the previous frame (used for edge-trigger detection).</summary>
    internal bool wasqpressed;

    /// <summary>Whether E was held on the previous frame (used for edge-trigger detection).</summary>
    internal bool wasepressed;

    /// <summary>Timestamp (ms) of the last clack rail sound effect.</summary>
    private int _lastRailSoundTimeMs;

    /// <summary>Index (0–3) of the last rail clack sound played, cycled round-robin.</summary>
    private int _lastRailSoundIndex;

    private readonly IGameWindowService platform;

    /// <summary>Returns the height of the minecart seat above the rail block origin.</summary>
    internal float MinecartHeight() => 1f / 2;

    private readonly IVoxelMap _voxelMap;
    private readonly IBlockRegistry _blockTypeRegistry;

    public ModRail(IGameWindowService platform, IVoxelMap voxelMap, IBlockRegistry blockTypeRegistry, IGame game) : base(game)
    {
        this.platform = platform;
        _voxelMap = voxelMap;
        _blockTypeRegistry = blockTypeRegistry;
        _railHeight = 0.3f;
    }

    /// <inheritdoc/>
    public override void OnUpdate(float args)
    {
        d_RailMapUtil ??= new RailMapUtil(_voxelMap, _blockTypeRegistry) { game = Game };
        RailOnNewFrame(args);
    }

    /// <summary>
    /// Main per-fixed-frame update: syncs the minecart entity, handles player
    /// input, advances rail progress, and manages enter/exit logic.
    /// </summary>
    internal void RailOnNewFrame(float dt)
    {
        EnsureMinecartEntity();
        SyncMinecartEntity();

        Game.LocalPlayerAnimationHint.InVehicle = railriding;
        Game.LocalPlayerAnimationHint.DrawFixX = 0;
        Game.LocalPlayerAnimationHint.DrawFixY = railriding ? -0.07f : 0;
        Game.LocalPlayerAnimationHint.DrawFixZ = 0;

        bool turnRight = Game.KeyboardState[Game.GetKey(Keys.D)];
        bool turnLeft = Game.KeyboardState[Game.GetKey(Keys.A)];

        RailSound();

        if (railriding)
        {
            AdvanceRailRiding(dt, turnLeft, turnRight);
        }

        HandleSpeedInput(dt);
        HandleReverseInput();
        HandleEnterExitInput(turnLeft, turnRight);

        wasqpressed = Game.KeyboardState[Game.GetKey(Keys.Q)] && Game.GuiTyping != TypingState.Typing;
        wasepressed = Game.KeyboardState[Game.GetKey(Keys.E)] && Game.GuiTyping != TypingState.Typing;
    }

    /// <summary>
    /// Creates the local minecart entity on the first frame if it does not yet exist.
    /// </summary>
    private void EnsureMinecartEntity()
    {
        if (localMinecart != null)
        {
            return;
        }

        localMinecart = new Entity { Minecart = new Minecart() };
        Game.EntityAddLocal(localMinecart);
    }

    /// <summary>Copies current rail state onto the minecart entity for rendering.</summary>
    private void SyncMinecartEntity()
    {
        localMinecart.Minecart.Enabled = railriding;
        if (!railriding)
        {
            return;
        }

        Minecart m = localMinecart.Minecart;
        m.PositionX = localMinecart.Position?.X ?? 0;
        m.PositionY = localMinecart.Position?.Y ?? 0;
        m.PositionZ = localMinecart.Position?.Z ?? 0;
        m.Direction = currentdirection;
        m.LastDirection = lastdirection;
        m.Progress = currentrailblockprogress;
    }

    /// <summary>
    /// Moves the player along the rail, advances block progress, and transitions
    /// to the next block when progress reaches 1.
    /// </summary>
    private void AdvanceRailRiding(float dt, bool turnLeft, bool turnRight)
    {
        Game.Controls.FreeMove = true;
        Game.EnableMove = false;

        Vector3 railPos = CurrentRailPos();
        Game.Player.Position.X = railPos.X;
        Game.Player.Position.Y = railPos.Y;
        Game.Player.Position.Z = railPos.Z;

        currentrailblockprogress += currentvehiclespeed * dt;

        if (currentrailblockprogress >= 1)
        {
            AdvanceToNextBlock(turnLeft, turnRight);
        }
    }

    /// <summary>
    /// Moves the minecart into the next rail block, resolving slopes and turn
    /// direction. Reverses direction if no valid next block exists.
    /// </summary>
    private void AdvanceToNextBlock(bool turnLeft, bool turnRight)
    {
        lastdirection = currentdirection;
        currentrailblockprogress = 0;

        TileEnterData newenter = new();
        Vector3i? nexttile = NextTile(currentdirection, currentrailblockX, currentrailblockY, currentrailblockZ);
        newenter.BlockPositionX = nexttile.Value.X;
        newenter.BlockPositionY = nexttile.Value.Y;
        newenter.BlockPositionZ = nexttile.Value.Z;

        TileEnterDirection enterDir = DirectionUtils.ResultEnter(DirectionUtils.ResultExit(currentdirection));

        // Slope: ascend if the current block ramps up in the exit direction.
        if (GetUpDownMove(currentrailblockX, currentrailblockY, currentrailblockZ, enterDir) == (int)RailPosition.Up)
        {
            newenter.BlockPositionZ++;
        }
        // Slope: descend if the next block ramps down in the enter direction.
        if (GetUpDownMove(newenter.BlockPositionX, newenter.BlockPositionY, newenter.BlockPositionZ - 1, enterDir) == (int)RailPosition.Down)
        {
            newenter.BlockPositionZ--;
        }

        newenter.EnterDirection = enterDir;

        VehicleDirection12 newDir = BestNewDirection(PossibleRails(newenter), turnLeft, turnRight, out bool found);
        if (!found)
        {
            currentdirection = DirectionUtils.Reverse(currentdirection);
        }
        else
        {
            currentdirection = newDir;
            currentrailblockX = newenter.BlockPositionX;
            currentrailblockY = newenter.BlockPositionY;
            currentrailblockZ = newenter.BlockPositionZ;
        }
    }

    /// <summary>
    /// Reads W/S keys to accelerate or brake the minecart.
    /// Speed is clamped to a minimum of zero.
    /// </summary>
    private void HandleSpeedInput(float dt)
    {
        if (Game.GuiTyping == TypingState.Typing)
        {
            return;
        }

        if (Game.KeyboardState[Game.GetKey(Keys.W)])
        {
            currentvehiclespeed += 1 * dt;
        }

        if (Game.KeyboardState[Game.GetKey(Keys.S)])
        {
            currentvehiclespeed -= 5 * dt;
        }

        if (currentvehiclespeed < 0)
        {
            currentvehiclespeed = 0;
        }
    }

    /// <summary>
    /// Edge-triggers the Q key to reverse the minecart's direction while riding.
    /// </summary>
    private void HandleReverseInput()
    {
        bool qPressed = Game.KeyboardState[Game.GetKey(Keys.Q)] && Game.GuiTyping != TypingState.Typing;
        if (!wasqpressed && qPressed)
        {
            Reverse();
        }
    }

    /// <summary>
    /// Edge-triggers the E key to enter an idle minecart or exit the current one.
    /// </summary>
    private void HandleEnterExitInput(bool turnLeft, bool turnRight)
    {
        bool ePressed = Game.KeyboardState[Game.GetKey(Keys.E)] && Game.GuiTyping != TypingState.Typing;
        if (wasepressed || !ePressed)
        {
            return;
        }

        if (!railriding && !Game.Controls.FreeMove)
        {
            TryEnterMinecart();
        }
        else if (railriding)
        {
            ExitVehicle();
            Game.Player.Position.Y += 0.7f;
        }
    }

    /// <summary>
    /// Attempts to mount the minecart on the rail block directly below the player.
    /// Sets the initial direction based on the rail type at that position.
    /// Exits immediately if no rail is found or the position is invalid.
    /// </summary>
    private void TryEnterMinecart()
    {
        currentrailblockX = (int)Game.Player.Position.X;
        currentrailblockY = (int)Game.Player.Position.Z;
        currentrailblockZ = (int)Game.Player.Position.Y - 1;
        if (!_voxelMap.IsValidPos(currentrailblockX, currentrailblockY, currentrailblockZ))
        {
            ExitVehicle();
            return;
        }

        int railUnder = _blockTypeRegistry.Rail[_voxelMap.GetBlock(currentrailblockX, currentrailblockY, currentrailblockZ)];
        railriding = true;
        originalmodelheight = Game.GetCharacterEyesHeight();
        Game.SetCharacterEyesHeight(MinecartHeight());
        currentvehiclespeed = 0;

        if ((railUnder & (int)RailDirectionFlags.Horizontal) != 0)
        {
            currentdirection = VehicleDirection12.HorizontalRight;
        }
        else if ((railUnder & (int)RailDirectionFlags.Vertical) != 0)
        {
            currentdirection = VehicleDirection12.VerticalUp;
        }
        else if ((railUnder & (int)RailDirectionFlags.UpLeft) != 0)
        {
            currentdirection = VehicleDirection12.UpLeftUp;
        }
        else if ((railUnder & (int)RailDirectionFlags.UpRight) != 0)
        {
            currentdirection = VehicleDirection12.UpRightUp;
        }
        else if ((railUnder & (int)RailDirectionFlags.DownLeft) != 0)
        {
            currentdirection = VehicleDirection12.DownLeftLeft;
        }
        else if ((railUnder & (int)RailDirectionFlags.DownRight) != 0)
        {
            currentdirection = VehicleDirection12.DownRightRight;
        }
        else
        {
            ExitVehicle();
            return;
        }

        lastdirection = currentdirection;
    }

    /// <summary>
    /// Reverses the minecart's travel direction and mirrors progress within the
    /// current block so the cart appears to turn around smoothly.
    /// </summary>
    internal void Reverse()
    {
        currentdirection = DirectionUtils.Reverse(currentdirection);
        currentrailblockprogress = 1 - currentrailblockprogress;
        lastdirection = currentdirection;
    }

    /// <summary>
    /// Exits the minecart: restores the player's original eye height, re-enables
    /// standard movement, and clears the rail-riding flag.
    /// </summary>
    internal void ExitVehicle()
    {
        Game.SetCharacterEyesHeight(originalmodelheight);
        railriding = false;
        Game.Controls.FreeMove = false;
        Game.EnableMove = true;
    }

    /// <summary>
    /// Computes the world-space position the player should occupy given the current
    /// rail block, direction, progress, and slope.
    /// The result is offset upward by <see cref="_railHeight"/> + 1 so the player
    /// sits above the rail block without clipping into it.
    /// </summary>
    internal Vector3 CurrentRailPos()
    {
        RailSlope slope = d_RailMapUtil.GetRailSlope(currentrailblockX, currentrailblockY, currentrailblockZ);
        float aX = currentrailblockX;
        float aY = currentrailblockY;
        float aZ = currentrailblockZ;
        float half = 0.5f;
        float p = currentrailblockprogress;
        float xc = 0, yc = 0, zc = 0;

        switch (currentdirection)
        {
            case VehicleDirection12.HorizontalRight:
                xc += p; yc += half;
                if (slope == RailSlope.TwoRightRaised)
                {
                    zc += p;
                }

                if (slope == RailSlope.TwoLeftRaised)
                {
                    zc += 1 - p;
                }

                break;
            case VehicleDirection12.HorizontalLeft:
                xc += 1 - p; yc += half;
                if (slope == RailSlope.TwoRightRaised)
                {
                    zc += 1 - p;
                }

                if (slope == RailSlope.TwoLeftRaised)
                {
                    zc += p;
                }

                break;
            case VehicleDirection12.VerticalDown:
                xc += half; yc += p;
                if (slope == RailSlope.TwoDownRaised)
                {
                    zc += p;
                }

                if (slope == RailSlope.TwoUpRaised)
                {
                    zc += 1 - p;
                }

                break;
            case VehicleDirection12.VerticalUp:
                xc += half; yc += 1 - p;
                if (slope == RailSlope.TwoDownRaised)
                {
                    zc += 1 - p;
                }

                if (slope == RailSlope.TwoUpRaised)
                {
                    zc += p;
                }

                break;
            case VehicleDirection12.UpLeftLeft: xc += half * (1 - p); yc += half * p; break;
            case VehicleDirection12.UpLeftUp: xc += half * p; yc += half - (half * p); break;
            case VehicleDirection12.UpRightRight: xc += half + (half * p); yc += half * p; break;
            case VehicleDirection12.UpRightUp: xc += 1 - (half * p); yc += half - (half * p); break;
            case VehicleDirection12.DownLeftLeft: xc += half * (1 - p); yc += 1 - (half * p); break;
            case VehicleDirection12.DownLeftDown: xc += half * p; yc += half + (half * p); break;
            case VehicleDirection12.DownRightRight: xc += half + (half * p); yc += 1 - (half * p); break;
            case VehicleDirection12.DownRightDown: xc += 1 - (half * p); yc += half + (half * p); break;
        }

        // +1 so the player sits above the rail block and picking still works.
        return new Vector3(aX + xc, aZ + _railHeight + 1 + zc, aY + yc);
    }

    /// <summary>
    /// Returns whether the minecart will ascend, descend, or travel flat when
    /// entering the given rail block from the specified direction.
    /// </summary>
    /// <returns>One of <see cref="RailDirection.Up"/>, <see cref="RailDirection.Down"/>, or <see cref="RailDirection.None"/>.</returns>
    internal int GetUpDownMove(int railblockX, int railblockY, int railblockZ, TileEnterDirection dir)
    {
        if (!_voxelMap.IsValidPos(railblockX, railblockY, railblockZ))
        {
            return (int)RailPosition.None;
        }

        RailSlope slope = d_RailMapUtil.GetRailSlope(railblockX, railblockY, railblockZ);

        if (slope == RailSlope.TwoDownRaised && dir == TileEnterDirection.Up)
        {
            return (int)RailPosition.Up;
        }

        if (slope == RailSlope.TwoUpRaised && dir == TileEnterDirection.Down)
        {
            return (int)RailPosition.Up;
        }

        if (slope == RailSlope.TwoLeftRaised && dir == TileEnterDirection.Right)
        {
            return (int)RailPosition.Up;
        }

        if (slope == RailSlope.TwoRightRaised && dir == TileEnterDirection.Left)
        {
            return (int)RailPosition.Up;
        }

        if (slope == RailSlope.TwoDownRaised && dir == TileEnterDirection.Down)
        {
            return (int)RailPosition.Down;
        }

        if (slope == RailSlope.TwoUpRaised && dir == TileEnterDirection.Up)
        {
            return (int)RailPosition.Down;
        }

        if (slope == RailSlope.TwoLeftRaised && dir == TileEnterDirection.Left)
        {
            return (int)RailPosition.Down;
        }

        if (slope == RailSlope.TwoRightRaised && dir == TileEnterDirection.Right)
        {
            return (int)RailPosition.Down;
        }

        return (int)RailPosition.None;
    }

    /// <summary>
    /// Plays looping rail noise and periodic clack sounds scaled to the current speed.
    /// </summary>
    internal void RailSound()
    {
        float soundRate = Math.Min(currentvehiclespeed, 10f);
        Game.AudioPlayLoop("railnoise.ogg", railriding && soundRate > 0.1f, false);

        if (!railriding || soundRate <= 0)
        {
            return;
        }

        if ((platform.TimeMillisecondsFromStart - _lastRailSoundTimeMs) > 1000 / soundRate)
        {
            _lastRailSoundIndex = (_lastRailSoundIndex + 1) % 4;
            Game.PlayAudio(string.Format("rail{0}.ogg", _lastRailSoundIndex.ToString()));
            _lastRailSoundTimeMs = platform.TimeMillisecondsFromStart;
        }
    }

    /// <summary>
    /// Returns the world-block coordinates of the next rail block in the given
    /// <paramref name="direction"/> from the current block.
    /// </summary>
    public static Vector3i? NextTile(VehicleDirection12 direction, int currentTileX, int currentTileY, int currentTileZ)
        => NextTileByExit(DirectionUtils.ResultExit(direction), currentTileX, currentTileY, currentTileZ);

    /// <summary>
    /// Returns the neighbour block in the given exit direction.
    /// Returns <see langword="null"/> for unrecognised directions.
    /// </summary>
    public static Vector3i? NextTileByExit(TileExitDirection direction, int x, int y, int z)
        => direction switch
        {
            TileExitDirection.Left => new Vector3i(x - 1, y, z),
            TileExitDirection.Right => new Vector3i(x + 1, y, z),
            TileExitDirection.Up => new Vector3i(x, y - 1, z),
            TileExitDirection.Down => new Vector3i(x, y + 1, z),
            _ => null,
        };

    /// <summary>
    /// Returns a bitmask of the <see cref="VehicleDirection12"/> values that are
    /// valid exits from the block described by <paramref name="enter"/>.
    /// </summary>
    internal int PossibleRails(TileEnterData enter)
    {
        if (!_voxelMap.IsValidPos(enter.BlockPositionX, enter.BlockPositionY, enter.BlockPositionZ))
        {
            return 0;
        }

        int railFlags = _blockTypeRegistry.Rail[
            _voxelMap.GetBlock(enter.BlockPositionX, enter.BlockPositionY, enter.BlockPositionZ)];

        VehicleDirection12[] candidates = new VehicleDirection12[3];
        int candidateCount = 0;
        VehicleDirection12[] possible3 = DirectionUtils.PossibleNewRails3(enter.EnterDirection);

        for (int i = 0; i < 3; i++)
        {
            VehicleDirection12 dir = possible3[i];
            if ((railFlags & DirectionUtils.ToRailDirectionFlags(DirectionUtils.ToRailDirection(dir))) != 0)
            {
                candidates[candidateCount++] = dir;
            }
        }

        return DirectionUtils.ToVehicleDirection12Flags_(candidates, candidateCount);
    }

    /// <summary>
    /// Selects the best exit direction from a bitmask of possible directions,
    /// honouring the player's turn input if it matches a corner.
    /// Straight rails take priority over corners when no turn input is given.
    /// </summary>
    /// <param name="dirFlags">Bitmask of available <see cref="VehicleDirection12"/> exits.</param>
    /// <param name="turnLeft"><see langword="true"/> when the player is steering left.</param>
    /// <param name="turnRight"><see langword="true"/> when the player is steering right.</param>
    /// <param name="retFound"><see langword="false"/> when no direction is available (end of rail).</param>
    internal static VehicleDirection12 BestNewDirection(int dirFlags, bool turnLeft, bool turnRight, out bool retFound)
    {
        retFound = true;

        if (turnRight)
        {
            if ((dirFlags & VehicleDirection12Flags.DownRightRight) != 0)
            {
                return VehicleDirection12.DownRightRight;
            }

            if ((dirFlags & VehicleDirection12Flags.UpRightUp) != 0)
            {
                return VehicleDirection12.UpRightUp;
            }

            if ((dirFlags & VehicleDirection12Flags.UpLeftLeft) != 0)
            {
                return VehicleDirection12.UpLeftLeft;
            }

            if ((dirFlags & VehicleDirection12Flags.DownLeftDown) != 0)
            {
                return VehicleDirection12.DownLeftDown;
            }
        }

        if (turnLeft)
        {
            if ((dirFlags & VehicleDirection12Flags.DownRightDown) != 0)
            {
                return VehicleDirection12.DownRightDown;
            }

            if ((dirFlags & VehicleDirection12Flags.UpRightRight) != 0)
            {
                return VehicleDirection12.UpRightRight;
            }

            if ((dirFlags & VehicleDirection12Flags.UpLeftUp) != 0)
            {
                return VehicleDirection12.UpLeftUp;
            }

            if ((dirFlags & VehicleDirection12Flags.DownLeftLeft) != 0)
            {
                return VehicleDirection12.DownLeftLeft;
            }
        }

        // Straight directions take priority.
        if ((dirFlags & VehicleDirection12Flags.VerticalDown) != 0)
        {
            return VehicleDirection12.VerticalDown;
        }

        if ((dirFlags & VehicleDirection12Flags.VerticalUp) != 0)
        {
            return VehicleDirection12.VerticalUp;
        }

        if ((dirFlags & VehicleDirection12Flags.HorizontalLeft) != 0)
        {
            return VehicleDirection12.HorizontalLeft;
        }

        if ((dirFlags & VehicleDirection12Flags.HorizontalRight) != 0)
        {
            return VehicleDirection12.HorizontalRight;
        }

        if ((dirFlags & VehicleDirection12Flags.DownLeftDown) != 0)
        {
            return VehicleDirection12.DownLeftDown;
        }

        if ((dirFlags & VehicleDirection12Flags.DownLeftLeft) != 0)
        {
            return VehicleDirection12.DownLeftLeft;
        }

        if ((dirFlags & VehicleDirection12Flags.DownRightDown) != 0)
        {
            return VehicleDirection12.DownRightDown;
        }

        if ((dirFlags & VehicleDirection12Flags.DownRightRight) != 0)
        {
            return VehicleDirection12.DownRightRight;
        }

        if ((dirFlags & VehicleDirection12Flags.UpLeftLeft) != 0)
        {
            return VehicleDirection12.UpLeftLeft;
        }

        if ((dirFlags & VehicleDirection12Flags.UpLeftUp) != 0)
        {
            return VehicleDirection12.UpLeftUp;
        }

        if ((dirFlags & VehicleDirection12Flags.UpRightRight) != 0)
        {
            return VehicleDirection12.UpRightRight;
        }

        if ((dirFlags & VehicleDirection12Flags.UpRightUp) != 0)
        {
            return VehicleDirection12.UpRightUp;
        }

        retFound = false;
        return VehicleDirection12.DownLeftDown; // sentinel — caller checks retFound
    }
}

public class TileEnterData
{
    public int BlockPositionX { get; set; }
    public int BlockPositionY { get; set; }
    public int BlockPositionZ { get; set; }
    public TileEnterDirection EnterDirection { get; set; }
}

public class RailMapUtil
{
    internal IGame game;

    private readonly IVoxelMap voxelMap;
    private readonly IBlockRegistry blockTypeRegistry;

    public RailMapUtil(IVoxelMap voxelMap, IBlockRegistry blockTypeRegistry)
    {
        this.voxelMap = voxelMap;
        this.blockTypeRegistry = blockTypeRegistry;
    }

    public RailSlope GetRailSlope(int x, int y, int z)
    {
        int tiletype = voxelMap.GetBlock(x, y, z);
        int railDirectionFlags = blockTypeRegistry.BlockTypes[tiletype].Rail;
        int blocknear;
        if (x < voxelMap.MapSizeX - 1)
        {
            blocknear = voxelMap.GetBlock(x + 1, y, z);
            if (railDirectionFlags == (int)RailDirectionFlags.Horizontal &&
                 blocknear != 0 && blockTypeRegistry.BlockTypes[blocknear].Rail == 0)
            {
                return RailSlope.TwoRightRaised;
            }
        }

        if (x > 0)
        {
            blocknear = voxelMap.GetBlock(x - 1, y, z);
            if (railDirectionFlags == (int)RailDirectionFlags.Horizontal &&
                 blocknear != 0 && blockTypeRegistry.BlockTypes[blocknear].Rail == 0)
            {
                return RailSlope.TwoLeftRaised;

            }
        }

        if (y > 0)
        {
            blocknear = voxelMap.GetBlock(x, y - 1, z);
            if (railDirectionFlags == (int)RailDirectionFlags.Vertical &&
                  blocknear != 0 && blockTypeRegistry.BlockTypes[blocknear].Rail == 0)
            {
                return RailSlope.TwoUpRaised;
            }
        }

        if (y >= voxelMap.MapSizeY - 1)
        {
            return RailSlope.Flat;
        }

        blocknear = voxelMap.GetBlock(x, y + 1, z);
        if (railDirectionFlags == (int)RailDirectionFlags.Vertical &&
            blocknear != 0 && blockTypeRegistry.BlockTypes[blocknear].Rail == 0)
        {
            return RailSlope.TwoDownRaised;
        }

        return RailSlope.Flat;
    }
}

public enum RailPosition
{
    None = 0,
    Up = 1,
    Down = 2
}