using OpenTK.Mathematics;
using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;

/// <summary>
/// Handles keyboard input for player movement and camera control each fixed frame.
/// </summary>
public class ModCameraKeys : ModBase
{
    private const float OverheadCameraSpeed = 3f;
    private readonly IGameWindowService platform;
    private readonly ICameraService cameraService;

    public ModCameraKeys(IGameWindowService platform, ICameraService cameraService, IGame game) : base(game)
    {
        this.platform = platform;
        this.cameraService = cameraService;
    }

    public override void OnUpdate(float args)
    {
        if (Game.GuiState == GameState.MapLoading)
        {
            return;
        }

        float dt = args;
        bool isNormal = Game.GuiState == GameState.Normal;
        bool isTyping = Game.GuiTyping != TypingState.None;
        UpdateJumpAndShift(isNormal, isTyping);

        Game.Controls.MovedX = 0;
        Game.Controls.MovedY = 0;
        Game.Controls.MoveUp = false;
        Game.Controls.MoveDown = false;

        if (isNormal)
        {
            if (!isTyping)
            {
                UpdateAutoJump();

                if (Game.OverheadCamera)
                {
                    UpdateOverheadCamera(dt);
                }
                else if (Game.EnableMove)
                {
                    UpdateMovementKeys();
                }
            }

            UpdateVerticalFreemove(isTyping);
        }
    }

    /// <summary>Updates jump and shift control flags based on keyboard state.</summary>
    private void UpdateJumpAndShift(bool isNormal, bool isTyping)
    {
        bool canAct = isNormal && !isTyping;
        Game.Controls.WantsJump = canAct && Game.KeyboardState[Game.GetKey(Keys.Space)];
        Game.Controls.WantsJumpHalf = false;
        Game.Controls.ShiftKeyDown = canAct && Game.KeyboardState[Game.GetKey(Keys.LeftShift)];
    }

    /// <summary>Triggers auto-jump when the player walks into a climbable wall or half-block.</summary>
    private void UpdateAutoJump()
    {
        if (Game.ReachedWall1BlockHigh && (Game.AutoJumpEnabled || !platform.IsMousePointerLocked()))
        {
            Game.Controls.WantsJump = true;
        }

        if (Game.ReachedHalfBlock)
        {
            Game.Controls.WantsJumpHalf = true;
        }
    }

    /// <summary>Handles overhead (RTS-style) camera rotation, angle, and click-to-move.</summary>
    private void UpdateOverheadCamera(float dt)
    {
        if (Game.KeyboardState[Game.GetKey(Keys.A)])
        {
            cameraService.TurnRight(dt * OverheadCameraSpeed);
        }

        if (Game.KeyboardState[Game.GetKey(Keys.D)])
        {
            cameraService.TurnLeft(dt * OverheadCameraSpeed);
        }

        cameraService.Center = new Vector3(Game.Player.Position.X,
            Game.Player.Position.Y, Game.Player.Position.Z);
        CameraMoveArgs m = new()
        {
            Distance = cameraService.OverHeadCameraDistance,
            AngleUp = Game.KeyboardState[Game.GetKey(Keys.W)],
            AngleDown = Game.KeyboardState[Game.GetKey(Keys.S)],
        };
        cameraService.Move(m, dt);

        // Click-to-move: steer player toward destination
        float toDest = Vector3.Distance(
            new Vector3(Game.Player.Position.X, Game.Player.Position.Y, Game.Player.Position.Z),
            new Vector3(Game.PlayerDestination.X + 0.5f, Game.PlayerDestination.Y - 0.5f, Game.PlayerDestination.Z + 0.5f));

        if (toDest >= 1)
        {
            Game.Controls.MovedY += 1;
            if (Game.ReachedWall)
            {
                Game.Controls.WantsJump = true;
            }

            float qX = Game.PlayerDestination.X - Game.Player.Position.X;
            float qZ = Game.PlayerDestination.Z - Game.Player.Position.Z;
            Game.Player.Position.RotY = (MathF.PI / 2) + MathF.Atan2(qX, qZ);
            Game.Player.Position.RotX = MathF.PI;
        }
    }

    /// <summary>Handles WASD movement and leaning animation hints in first/third person.</summary>
    private void UpdateMovementKeys()
    {
        if (Game.KeyboardState[Game.GetKey(Keys.W)])
        {
            Game.Controls.MovedY += 1;
        }

        if (Game.KeyboardState[Game.GetKey(Keys.S)])
        {
            Game.Controls.MovedY -= 1;
        }

        bool leanLeft = Game.KeyboardState[Game.GetKey(Keys.A)];
        bool leanRight = Game.KeyboardState[Game.GetKey(Keys.D)];
        if (leanLeft)
        {
            Game.Controls.MovedX -= 1;
            Game.LocalStance = 1;
        }

        if (leanRight)
        {
            Game.Controls.MovedX += 1;
            Game.LocalStance = 2;
        }

        if (!leanLeft && !leanRight)
        {
            Game.LocalStance = 0;
        }

        Game.LocalPlayerAnimationHint.LeanLeft = leanLeft;
        Game.LocalPlayerAnimationHint.LeanRight = leanRight;

        Game.Controls.MovedX += Game.TouchMoveDx;
        Game.Controls.MovedY += Game.TouchMoveDy;
    }

    /// <summary>Handles vertical movement in freemove mode or while swimming.</summary>
    private void UpdateVerticalFreemove(bool isTyping)
    {
        if (!Game.Controls.FreeMove && !Game.SwimmingEyes())
        {
            return;
        }

        if (isTyping)
        {
            return;
        }

        if (Game.KeyboardState[Game.GetKey(Keys.Space)])
        {
            Game.Controls.MoveUp = true;
        }

        if (Game.KeyboardState[Game.GetKey(Keys.LeftControl)])
        {
            Game.Controls.MoveDown = true;
        }
    }
}