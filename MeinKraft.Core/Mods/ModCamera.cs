using OpenTK.Mathematics;

/// <summary>
/// Handles first-person, third-person, and overhead camera positioning each frame.
/// </summary>
public class ModCamera : ModBase
{
    private static readonly Vector3 Up = Vector3.UnitY;
    private Vector3 overheadCameraEye;
    private readonly ICameraService cameraService;
    private readonly IMeshDrawer _meshDrawer;
    private readonly IGameWindowService _gameService;

    // ── Camera bob state ──────────────────────────────────────────────────────

    /// <summary>Accumulated walk cycle phase in radians.</summary>
    private float _bobPhase;

    /// <summary>
    /// Smoothed amplitude scalar [0, 1].
    /// Ramps up when walking, fades back to zero when stopping or airborne.
    /// </summary>
    private float _bobAmplitude;

    /// <summary>Cached delta-time so helper methods don't need an extra parameter.</summary>
    private float _dt;

    // Phase advances this many radians per unit of horizontal speed per second.
    // At a typical walk speed of ~4 u/s this gives ≈ 2 full vertical cycles/s,
    // matching the classic two-steps-per-second cadence.
    private const float BobFrequency = 1.65f;

    // Peak eye displacement at full amplitude.
    private const float BobVerticalAmplitude = 0.15f;  // up / down
    private const float BobLateralAmplitude = 0.15f;  // left / right (half cycle)

    // How quickly amplitude tracks the target (higher = snappier fade-in/out).
    private const float BobSmoothing = 10f;

    // Minimum horizontal speed before the bob activates.
    private const float BobSpeedThreshold = 0.5f;

    // ── Sprint FOV state ──────────────────────────────────────────────────────

    /// <summary>Current rendered FOV, smoothed between base and sprint target.</summary>
    private float _currentFov = -1f; // -1 = uninitialised, snaps on first frame

    private const float SprintFovBonus = 15f * MathF.PI / 180f;   // degrees added while sprinting
    private const float FovSmoothing = 8f;   // higher = snappier transition
    private const float SprintThreshold = 1.05f; // MoveSpeed / Basemovespeed ratio to count as sprint

    // ─────────────────────────────────────────────────────────────────────────

    public ModCamera(ICameraService cameraService, IGame game, IMeshDrawer meshDrawer, IGameWindowService gameService) : base(game)
    {
        this.cameraService = cameraService;
        this._meshDrawer = meshDrawer;
        this._gameService = gameService;
    }

    public override void OnBeforeRender3d(float deltaTime)
    {
        _dt = deltaTime;
        Game.Camera = Game.OverheadCamera ? OverheadCamera() : FppCamera();
    }

    public override void OnFrame(float deltaTime)
    {
        if (!Game.Spawned) // already tracks whether server has finished setup
            return;

        UpdateSprintFov();
    }

    public override void OnRender2d(float dt)
    {
        DrawSprintVignette();
    }

    private Matrix4 OverheadCamera()
    {
        cameraService.GetPosition(ref overheadCameraEye);
        Vector3 eye = overheadCameraEye;
        Vector3 target = new(
            cameraService.Center.X,
            cameraService.Center.Y + Game.GetCharacterEyesHeight(),
            cameraService.Center.Z);

        cameraService.OverHeadCameraDistance = LimitThirdPersonCameraToWalls(ref eye, ref target, cameraService.OverHeadCameraDistance);
        SetCameraEye(eye);
        return Matrix4.LookAt(eye, target, Up);
    }

    private Matrix4 FppCamera()
    {
        Vector3 forward = new();
        VectorUtils.ToVectorInFixedSystem(0, 0, 1, Game.Player.Position.RotX, Game.Player.Position.RotY, ref forward);

        float eyeX = Game.Player.Position.X;
        float eyeY = Game.Player.Position.Y + Game.GetCharacterEyesHeight();
        float eyeZ = Game.Player.Position.Z;
        Vector3 eye, target;

        if (!Game.EnableTppView)
        {
            eye = new Vector3(eyeX, eyeY, eyeZ);
            target = new Vector3(eyeX + forward.X, eyeY + forward.Y, eyeZ + forward.Z);

            // Bob is first-person only — TPP and overhead would look wrong.
            ApplyCameraBob(ref eye, ref target, forward);
        }
        else
        {
            eye = new Vector3(eyeX + (forward.X * -Game.TppCameraDistance),
                              eyeY + (forward.Y * -Game.TppCameraDistance),
                              eyeZ + (forward.Z * -Game.TppCameraDistance));
            target = new Vector3(eyeX, eyeY, eyeZ);
            Game.TppCameraDistance = LimitThirdPersonCameraToWalls(ref eye, ref target, Game.TppCameraDistance);
        }

        SetCameraEye(eye);
        return Matrix4.LookAt(eye, target, Up);
    }

    /// <summary>
    /// Applies a sinusoidal walking bob to <paramref name="eye"/> and
    /// <paramref name="target"/>. Both are shifted by the same world-space
    /// delta so the look direction is unchanged — only the eye position moves.
    /// </summary>
    private void ApplyCameraBob(ref Vector3 eye, ref Vector3 target, Vector3 forward)
    {
        // Horizontal speed only — vertical velocity (jumping/falling) must not
        // drive the phase or the bob will fire mid-air.
        float speed = MathF.Sqrt(
            Game.playervelocity.X * Game.playervelocity.X +
            Game.playervelocity.Z * Game.playervelocity.Z);

        bool shouldBob = Game.IsPlayerOnGround && speed > BobSpeedThreshold && Game.FreemoveLevel == 0;

        // Smoothly ramp amplitude toward 1 when walking, 0 when stopped/airborne.
        float targetAmplitude = shouldBob ? 1f : 0f;
        _bobAmplitude += (targetAmplitude - _bobAmplitude) * MathF.Min(1f, BobSmoothing * _dt);

        // Skip the trig entirely when the contribution is negligible.
        if (_bobAmplitude < 0.001f)
            return;

        // Advance the phase proportionally to speed so faster movement bobs faster.
        _bobPhase += _dt * speed * BobFrequency;

        // sin(phase)       → full-cycle vertical oscillation (up / down twice per stride)
        // sin(phase * 0.5) → half-cycle lateral sway (one left-right per full stride)
        float vertical = MathF.Sin(_bobPhase) * BobVerticalAmplitude * _bobAmplitude;
        float lateral = MathF.Sin(_bobPhase * 0.5f) * BobLateralAmplitude * _bobAmplitude;

        // Right vector in the horizontal plane — consistent with how the hand bob works.
        Vector3 right = Vector3.Cross(forward, Up);
        right.Y = 0f;   // keep sway purely horizontal regardless of pitch
        right = right.Normalized();

        Vector3 bobOffset = (Vector3.UnitY * vertical) + (right * lateral);
        eye += bobOffset;
        target += bobOffset;
    }

    /// <summary>
    /// Casts a ray from the camera target toward the eye and pulls the camera
    /// in if terrain blocks the view, with a minimum distance of 0.3 units.
    /// </summary>
    private float LimitThirdPersonCameraToWalls(ref Vector3 eye, ref Vector3 target, float distance)
    {
        const float MinDistance = 0.3f;

        Vector3 dir = eye - target;
        float dirLength = dir.Length;
        dir /= dirLength;

        Vector3 rayEnd = target + (dir * (Game.TppCameraDistance + 1));
        Line3D pick = new()
        {
            Start = target,
            End = rayEnd
        };

        ArraySegment<BlockPosSide> hits = Game.Pick(cameraService.BlockOctreeSearcher, pick, out int hitCount);
        if (hitCount > 0)
        {
            BlockPosSide nearest = Game.Nearest(hits, hitCount, target);
            float pickDistance = new Vector3(nearest.BlockPos[0] - target.X, nearest.BlockPos[1] - target.Y, nearest.BlockPos[2] - target.Z).Length;
            distance = Math.Max(MinDistance, Math.Min(pickDistance - 1, distance));
        }

        eye = target + (dir * distance);
        return distance;
    }

    private void UpdateSprintFov()
    {
        // Snap to the game's actual FOV on the first frame so there's no lerp from 0.
        float baseFov = Game.CurrentFov() - Game.FovOffset; // read true base
        if (_currentFov < 0f)
            _currentFov = 0f;

        bool isSprinting = Game.MoveSpeedNow() > Game.Basemovespeed * SprintThreshold;

        float targetOffset = isSprinting ? SprintFovBonus : 0f;
        _currentFov += (targetOffset - _currentFov) * MathF.Min(1f, FovSmoothing * _dt);

        Game.FovOffset = _currentFov;
    }

    // ── Sprint vignette ───────────────────────────────────────────────────────

    private readonly Draw2dData[] _vignetteQuads = new Draw2dData[256];
    private bool _vignetteInitialised;


    private void EnsureVignetteQuads()
    {
        if (_vignetteInitialised) return;
        for (int i = 0; i < _vignetteQuads.Length; i++)
            _vignetteQuads[i] = new Draw2dData { InAtlasId = -1 };
        _vignetteInitialised = true;
    }

    /// <summary>
    /// Draws the vignette overlay scaled to the full screen.
    /// Alpha is driven by the current FovOffset so it fades in/out with the FOV.
    /// </summary>
    private void DrawSprintVignette()
    {
        float t = Math.Clamp(Game.FovOffset / SprintFovBonus, 0f, 1f);
        if (t < 0.01f) return;

        EnsureVignetteQuads();

        int w = _gameService.CanvasWidth;
        int h = _gameService.CanvasHeight;
        const int Strips = 32;
        float maxThickness = 200f * t;
        float strip = maxThickness / Strips;
        int idx = 0;

        for (int i = 0; i < Strips; i++)
        {
            float offset = i * strip;
            float frac = 1f - (float)i / Strips; // 1.0 = outermost, 0 = innermost
            int alpha = (int)(frac * frac * frac * 170 * t);
            int color = ColorUtils.ColorFromArgb(alpha, 0, 0, 0);

            // Top — full width
            _vignetteQuads[idx].X1 = 0; _vignetteQuads[idx].Y1 = offset;
            _vignetteQuads[idx].Width = w; _vignetteQuads[idx].Height = strip;
            _vignetteQuads[idx].Color = color; idx++;

            // Bottom — full width
            _vignetteQuads[idx].X1 = 0; _vignetteQuads[idx].Y1 = h - offset - strip;
            _vignetteQuads[idx].Width = w; _vignetteQuads[idx].Height = strip;
            _vignetteQuads[idx].Color = color; idx++;

            // Left + Right — clipped vertically so corners aren't double-darkened
            float sideY = offset + strip;
            float sideH = h - 2f * sideY;
            if (sideH > 0)
            {
                _vignetteQuads[idx].X1 = offset; _vignetteQuads[idx].Y1 = sideY;
                _vignetteQuads[idx].Width = strip; _vignetteQuads[idx].Height = sideH;
                _vignetteQuads[idx].Color = color; idx++;

                _vignetteQuads[idx].X1 = w - offset - strip; _vignetteQuads[idx].Y1 = sideY;
                _vignetteQuads[idx].Width = strip; _vignetteQuads[idx].Height = sideH;
                _vignetteQuads[idx].Color = color; idx++;
            }
        }

        _meshDrawer.OrthoMode(w, h);
        Game.Draw2dTextures(_vignetteQuads, idx, 0);
        _meshDrawer.PerspectiveMode();
    }

    /// <summary>Writes the eye position back to the game for other systems to use.</summary>
    private void SetCameraEye(Vector3 eye) => Game.CameraEye = eye;
}