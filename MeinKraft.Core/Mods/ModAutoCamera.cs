//This is a cinematic camera system — you mark waypoints while exploring, then tell it to play back a smooth camera
//flight through all of them. It works like this:

//Record waypoints — .cam p captures your current position and orientation into a fixed array of up to 256 points
//Play — .cam start [seconds] calculates the total path length, derives a travel speed (distance ÷ time), 
//then each frame advances a timer, figures out which segment you're on, and uses Catmull-Rom spline interpolation
//to smoothly blend between the surrounding four waypoints — giving a natural curved path
//rather than straight lines between points
//Record to AVI — .cam rec [real seconds][video seconds] does the same but also grabs screenshots at 60fps
//and writes them to an AVI file, with a speed ratio so you can record in slow-motion or fast-forward
//Save/load paths — .cam save serialises waypoints to the clipboard as integers (positions in centimetres,
//orientations in milliradians); .cam load parses them back

//When playback ends or is stopped, the player's original position and camera control are restored.

using System.Text;
using TextCopy;

/// <summary>
/// A client-side mod that records a sequence of camera waypoints and plays
/// the camera smoothly through them using Catmull-Rom spline interpolation.
/// Optionally captures each frame to an AVI video file during playback.
/// </summary>
public class ModAutoCamera : ModBase
{
    /// <summary>Target frame rate for AVI recording (frames per second).</summary>
    private const int Framerate = 60;

    /// <summary>Maximum number of camera waypoints that can be stored.</summary>
    private const int MaxCameraPoints = 256;

    /// <summary>
    /// Default recording speed multiplier when <c>.cam rec [seconds]</c> is used
    /// without a video-duration argument. A value of 10 means one second of real
    /// playback becomes 0.1 s of video (10× speed).
    /// </summary>
    private const float DefaultRecSpeed = 10f;

    // ── Dependencies ──────────────────────────────────────────────────────────

    private readonly IGameWindowService _platform;

    // ── Waypoint storage ──────────────────────────────────────────────────────

    /// <summary>Fixed-size pool of camera waypoints.</summary>
    private readonly CameraPoint[] _cameraPoints = new CameraPoint[MaxCameraPoints];

    /// <summary>Number of waypoints currently stored.</summary>
    private int _cameraPointsCount;

    // ── Playback state ────────────────────────────────────────────────────────

    /// <summary>
    /// Whether the camera is currently playing back.
    /// Replaces the fragile <c>_playingTime == -1</c> sentinel.
    /// </summary>
    private bool _isPlaying;

    /// <summary>Elapsed real-time seconds since playback began.</summary>
    private float _playingTime;

    /// <summary>
    /// World-units per second at which the camera travels along the path.
    /// Derived from total path length divided by the requested playback duration.
    /// </summary>
    private float _playingSpeed;

    /// <summary>
    /// Precomputed cumulative distances along the path, indexed by segment start.
    /// Built once at <see cref="StartPlayback"/> so <see cref="OnNewFrame"/> never
    /// recalculates segment lengths per frame.
    /// </summary>
    private float[] _segmentStartDists = Array.Empty<float>();

    // ── Saved camera state ────────────────────────────────────────────────────

    /// <summary>Player position saved before playback, restored when playback stops.</summary>
    private float _previousPositionX, _previousPositionY, _previousPositionZ;

    /// <summary>Player orientation saved before playback, restored when playback stops.</summary>
    private float _previousOrientationX, _previousOrientationY, _previousOrientationZ;

    /// <summary>Freemove level that was active before playback started, restored on stop.</summary>
    private FreeMoveLevel _previousFreemove;

    // ── AVI recording ─────────────────────────────────────────────────────────

    /// <summary>Active AVI writer. <see langword="null"/> when not recording.</summary>
    private IAviWriter? _avi;

    /// <summary>
    /// Ratio of real-time seconds to recorded-video seconds.
    /// A value of 2 means one second of real playback becomes 0.5 s of video (2× speed).
    /// </summary>
    private float _recSpeed;

    /// <summary>
    /// Precomputed interval between AVI frames in real-time seconds.
    /// Cached at <see cref="StartPlayback"/> to avoid recomputing each frame.
    /// </summary>
    private float _frameInterval;

    /// <summary>Accumulated real-time delta used to decide when to capture the next AVI frame.</summary>
    private float _writeAccum;

    /// <summary>
    /// <see langword="true"/> after the first frame following playback start has been skipped.
    /// The first frame is skipped because the screen has not been redrawn yet at that point.
    /// </summary>
    private bool _firstFrameDone;

    // ── Constructor ───────────────────────────────────────────────────────────

    public ModAutoCamera(IGameWindowService platform, IGame game) : base(game)
    {
        _platform = platform;
    }

    // ── ModBase overrides ─────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override bool OnClientCommand(ClientCommandArgs args)
    {
        if (args.Command != "cam")
        {
            return false;
        }

        string[] arguments = args.Arguments.Split(" ");

        if (string.IsNullOrWhiteSpace(args.Arguments))
        {
            PrintHelp();
            return true;
        }

        switch (arguments[0])
        {
            case "p":
                AddPoint();
                break;

            case "start":
            case "play":
            case "rec":
                StartPlayback(arguments);
                break;

            case "stop":
                Game.AddChatLine("Camera stopped.");
                Stop();
                break;

            case "clear":
                Game.AddChatLine("Camera points cleared.");
                _cameraPointsCount = 0;
                Stop();
                break;

            case "save":
                SavePointsToClipboard();
                break;

            case "load":
                if (arguments.Length >= 2)
                {
                    LoadPointsFromString(arguments[1]);
                }

                break;
        }

        return true;
    }

    /// <inheritdoc/>
    public override void OnFrame(float dt)
    {
        if (!_isPlaying)
        {
            return;
        }

        _playingTime += dt;

        UpdateAvi(dt);

        float playingDist = _playingTime * _playingSpeed;

        // ── Fix #4: use precomputed segment distances ─────────────────────────
        int foundPoint = -1;
        for (int i = 0; i < _cameraPointsCount - 1; i++)
        {
            float segEnd = _segmentStartDists[i + 1];
            if (playingDist >= _segmentStartDists[i] && playingDist < segEnd)
            {
                foundPoint = i;
                break;
            }
        }

        if (foundPoint == -1)
        {
            Stop();
            return;
        }

        ApplyCatmullRomPosition(foundPoint, _segmentStartDists[foundPoint], playingDist);
    }

    // ── Command handlers ──────────────────────────────────────────────────────

    /// <summary>Displays the in-game help text for all <c>.cam</c> sub-commands.</summary>
    private void PrintHelp()
    {
        Game.AddChatLine("&6AutoCamera help.");
        Game.AddChatLine("&6.cam p&f - add a point to path");
        Game.AddChatLine("&6.cam start [real seconds]&f - play the path");
        Game.AddChatLine("&6.cam rec [real seconds] [video seconds]&f - play and record to .avi file");
        Game.AddChatLine("&6.cam stop&f - stop playing and recording");
        Game.AddChatLine("&6.cam clear&f - remove all points from path");
        Game.AddChatLine("&6.cam save&f - copy path points to clipboard");
        Game.AddChatLine("&6.cam load [points]&f - load path points");
    }

    /// <summary>
    /// Captures the player's current position and orientation as a new waypoint.
    /// </summary>
    private void AddPoint()
    {
        _cameraPoints[_cameraPointsCount++] = new CameraPoint
        {
            PositionGlX = Game.LocalPositionX,
            PositionGlY = Game.LocalPositionY,
            PositionGlZ = Game.LocalPositionZ,
            OrientationGlX = Game.LocalOrientationX,
            OrientationGlY = Game.LocalOrientationY,
            OrientationGlZ = Game.LocalOrientationZ,
        };
        Game.AddChatLine("Point defined.");
    }

    /// <summary>
    /// Validates preconditions and then begins camera playback, optionally opening
    /// an AVI file for recording.
    /// </summary>
    private void StartPlayback(string[] arguments)
    {
        if (!Game.AllowFreeMove)
        {
            Game.AddChatLine("Free move not allowed.");
            return;
        }

        if (_cameraPointsCount == 0)
        {
            Game.AddChatLine("No points defined. Enter points with \".cam p\" command.");
            return;
        }

        _playingSpeed = 1f;
        float totalRecTime = -1f;

        if (arguments[0] == "rec")
        {
            if (arguments.Length >= 3)
            {
                // ── Fix #5: TryParse instead of Parse ─────────────────────────
                if (!float.TryParse(arguments[2], out totalRecTime))
                {
                    Game.AddChatLine("Invalid video duration. Usage: .cam rec [real seconds] [video seconds]");
                    return;
                }
            }

            _avi = new AviWriterCiCs();
            _avi.Open(
                $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.avi",
                Framerate,
                _platform.CanvasWidth,
                _platform.CanvasHeight);
        }

        if (arguments.Length >= 2)
        {
            // ── Fix #5: TryParse instead of Parse ─────────────────────────────
            if (!float.TryParse(arguments[1], out float totalTime))
            {
                Game.AddChatLine("Invalid duration. Usage: .cam start [real seconds]");
                _avi?.Close();
                _avi = null;
                return;
            }

            _playingSpeed = TotalDistance() / totalTime;
            _recSpeed = totalRecTime < 0f ? DefaultRecSpeed : totalTime / totalRecTime;
        }

        // ── Fix #8: cache frame interval once ────────────────────────────────
        _frameInterval = 1f / Framerate * _recSpeed;

        // ── Fix #4: precompute cumulative segment distances ───────────────────
        _segmentStartDists = new float[_cameraPointsCount];
        _segmentStartDists[0] = 0f;
        for (int i = 1; i < _cameraPointsCount; i++)
        {
            _segmentStartDists[i] = _segmentStartDists[i - 1]
                                  + Distance(_cameraPoints[i - 1], _cameraPoints[i]);
        }

        _playingTime = 0f;
        _writeAccum = 0f;
        _firstFrameDone = false;
        _isPlaying = true;

        // Save current camera state so it can be restored when playback ends.
        _previousPositionX = Game.LocalPositionX;
        _previousPositionY = Game.LocalPositionY;
        _previousPositionZ = Game.LocalPositionZ;
        _previousOrientationX = Game.LocalOrientationX;
        _previousOrientationY = Game.LocalOrientationY;
        _previousOrientationZ = Game.LocalOrientationZ;

        Game.EnableDraw2d = false;
        _previousFreemove = Game.FreemoveLevel;
        Game.FreemoveLevel = FreeMoveLevel.Noclip;
        Game.EnableCameraControl = false;
    }

    /// <summary>
    /// Serialises all current waypoints to a compact comma-separated string and
    /// copies it to the system clipboard so the path can be shared or reloaded later.
    /// </summary>
    /// <remarks>
    /// Format: <c>1,x0,y0,z0,rx0,ry0,rz0,x1,…</c>
    /// Positions are stored as integer centimetres; orientations as integer milliradians.
    /// </remarks>
    private void SavePointsToClipboard()
    {
        StringBuilder sb = new("1,");
        for (int i = 0; i < _cameraPointsCount; i++)
        {
            CameraPoint p = _cameraPoints[i];
            sb.Append((int)(p.PositionGlX * 100)).Append(',');
            sb.Append((int)(p.PositionGlY * 100)).Append(',');
            sb.Append((int)(p.PositionGlZ * 100)).Append(',');
            sb.Append((int)(p.OrientationGlX * 1000)).Append(',');
            sb.Append((int)(p.OrientationGlY * 1000)).Append(',');
            sb.Append((int)(p.OrientationGlZ * 1000));
            if (i != _cameraPointsCount - 1)
            {
                sb.Append(',');
            }
        }

        var clipboard = new Clipboard();
        clipboard.SetText(sb.ToString());
        Game.AddChatLine("Camera points copied to clipboard.");
    }

    /// <summary>
    /// Parses a comma-separated waypoint string (as produced by <see cref="SavePointsToClipboard"/>)
    /// and replaces the current set of waypoints with the decoded values.
    /// </summary>
    private void LoadPointsFromString(string data)
    {
        string[] parts = data.Split(',');
        int n = (parts.Length - 1) / 6;
        _cameraPointsCount = 0;

        for (int i = 0; i < n; i++)
        {
            // ── Fix #2: explicit float division to avoid integer truncation ────
            _cameraPoints[_cameraPointsCount++] = new CameraPoint
            {
                PositionGlX = int.Parse(parts[1 + (i * 6) + 0]) / 100f,
                PositionGlY = int.Parse(parts[1 + (i * 6) + 1]) / 100f,
                PositionGlZ = int.Parse(parts[1 + (i * 6) + 2]) / 100f,
                OrientationGlX = int.Parse(parts[1 + (i * 6) + 3]) / 1000f,
                OrientationGlY = int.Parse(parts[1 + (i * 6) + 4]) / 1000f,
                OrientationGlZ = int.Parse(parts[1 + (i * 6) + 5]) / 1000f,
            };
        }

        Game.AddChatLine($"Camera points loaded: {n}");
    }

    /// <summary>
    /// Stops camera playback, restores the player's previous position, orientation,
    /// and freemove level, re-enables camera control and the HUD, and closes any
    /// open AVI writer.
    /// </summary>
    private void Stop()
    {
        Game.EnableDraw2d = true;
        Game.EnableCameraControl = true;

        if (_isPlaying)
        {
            Game.FreemoveLevel = _previousFreemove;
            Game.LocalPositionX = _previousPositionX;
            Game.LocalPositionY = _previousPositionY;
            Game.LocalPositionZ = _previousPositionZ;
            Game.LocalOrientationX = _previousOrientationX;
            Game.LocalOrientationY = _previousOrientationY;
            Game.LocalOrientationZ = _previousOrientationZ;
        }

        // ── Fix #3: bool flag instead of sentinel float ───────────────────────
        _isPlaying = false;
        _avi?.Close();
        _avi = null;
    }

    // ── Playback helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Evaluates the Catmull-Rom spline at the current playback distance and applies
    /// the resulting position and orientation to the local camera.
    /// </summary>
    private void ApplyCatmullRomPosition(int segmentIndex, float segmentStartDist, float playingDist)
    {
        CameraPoint a = _cameraPoints[segmentIndex];
        CameraPoint b = _cameraPoints[segmentIndex + 1];
        CameraPoint aMinus = segmentIndex - 1 >= 0
            ? _cameraPoints[segmentIndex - 1] : a;
        CameraPoint bPlus = segmentIndex + 2 < _cameraPointsCount
            ? _cameraPoints[segmentIndex + 2] : b;

        float t = (playingDist - segmentStartDist) / Distance(a, b);

        Game.LocalPositionX = CatmullRom(t, aMinus.PositionGlX, a.PositionGlX, b.PositionGlX, bPlus.PositionGlX);
        Game.LocalPositionY = CatmullRom(t, aMinus.PositionGlY, a.PositionGlY, b.PositionGlY, bPlus.PositionGlY);
        Game.LocalPositionZ = CatmullRom(t, aMinus.PositionGlZ, a.PositionGlZ, b.PositionGlZ, bPlus.PositionGlZ);

        Game.LocalOrientationX = CatmullRom(t, aMinus.OrientationGlX, a.OrientationGlX, b.OrientationGlX, bPlus.OrientationGlX);
        Game.LocalOrientationY = CatmullRom(t, aMinus.OrientationGlY, a.OrientationGlY, b.OrientationGlY, bPlus.OrientationGlY);
        Game.LocalOrientationZ = CatmullRom(t, aMinus.OrientationGlZ, a.OrientationGlZ, b.OrientationGlZ, bPlus.OrientationGlZ);
    }

    /// <summary>
    /// Conditionally captures a screenshot and appends it to the AVI file.
    /// Rate-controlled by <see cref="_frameInterval"/>.
    /// </summary>
    private void UpdateAvi(float dt)
    {
        if (_avi == null)
        {
            return;
        }

        if (!_firstFrameDone)
        {
            // Skip the very first frame: the screen has not been redrawn yet after
            // the camera was repositioned, so capturing it would produce a stale image.
            _firstFrameDone = true;
            return;
        }

        _writeAccum += dt;

        // ── Fix #8: use cached _frameInterval ─────────────────────────────────
        if (_writeAccum >= _frameInterval)
        {
            _writeAccum -= _frameInterval;
            Bitmap bmp = _platform.GrabScreenshot();
            _avi.AddFrame(bmp);
            bmp.Dispose();
        }
    }

    /// <summary>
    /// Returns the total arc length of the camera path by summing straight-line
    /// distances between all consecutive waypoints.
    /// </summary>
    private float TotalDistance()
    {
        float total = 0f;
        for (int i = 0; i < _cameraPointsCount - 1; i++)
        {
            total += Distance(_cameraPoints[i], _cameraPoints[i + 1]);
        }

        return total;
    }

    /// <summary>
    /// Returns the Euclidean distance between two waypoints (position only).
    /// </summary>
    private static float Distance(CameraPoint a, CameraPoint b)
    {
        float dx = a.PositionGlX - b.PositionGlX;
        float dy = a.PositionGlY - b.PositionGlY;
        float dz = a.PositionGlZ - b.PositionGlZ;
        return MathF.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }

    /// <summary>
    /// Evaluates the Catmull-Rom spline for a single scalar coordinate.
    /// </summary>
    /// <param name="t">Interpolation parameter in [0, 1].</param>
    /// <param name="p0">Control point before the segment start.</param>
    /// <param name="p1">Segment start value.</param>
    /// <param name="p2">Segment end value.</param>
    /// <param name="p3">Control point after the segment end.</param>
    public static float CatmullRom(float t, float p0, float p1, float p2, float p3)
    {
        return 0.5f * (
              (2 * p1)
            + ((-p0 + p2) * t)
            + (((2 * p0) - (5 * p1) + (4 * p2) - p3) * (t * t))
            + ((-p0 + (3 * p1) - (3 * p2) + p3) * (t * t * t)));
    }
}

/// <summary>
/// Stores the position and orientation of a single camera waypoint.
/// Stored as a struct so the <c>_cameraPoints[]</c> array is fully contiguous
/// in memory — no per-waypoint heap allocation.
/// </summary>
public struct CameraPoint
{
    /// <summary>World-space X coordinate of the camera.</summary>
    public float PositionGlX;

    /// <summary>World-space Y coordinate of the camera.</summary>
    public float PositionGlY;

    /// <summary>World-space Z coordinate of the camera.</summary>
    public float PositionGlZ;

    /// <summary>Camera orientation around the X axis (pitch), in radians.</summary>
    public float OrientationGlX;

    /// <summary>Camera orientation around the Y axis (yaw), in radians.</summary>
    public float OrientationGlY;

    /// <summary>Camera orientation around the Z axis (roll), in radians.</summary>
    public float OrientationGlZ;
}