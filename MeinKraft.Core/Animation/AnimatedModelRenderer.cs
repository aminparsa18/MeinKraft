using OpenTK.Mathematics;

/// <summary>
/// Renders an <see cref="AnimatedModel"/> by traversing its node hierarchy
/// and interpolating keyframe data each frame.
/// </summary>
public class AnimatedModelRenderer
{
    /// <summary>
    /// The frame rate at which animation data is authored.
    /// Used to convert real time (seconds) to animation frames.
    /// </summary>
    private const int fps = 60;

    /// <summary>
    /// Conversion factor from model units to world units.
    /// All position, pivot, and size values are stored in model units (1/16 of a world unit).
    /// </summary>
    private const float ModelUnitScale = 16f;

    /// <summary>The game instance used for GL draw calls.</summary>
    private readonly IOpenGlService openGlService;

    /// <summary>The model currently being rendered.</summary>
    private AnimatedModel m;

    /// <summary>Index of the currently playing animation in <see cref="AnimatedModel.Animations"/>.</summary>
    private readonly int anim;

    /// <summary>Current playback position, in frames.</summary>
    private float frame;

    /// <summary>
    /// Keyframes pre-grouped by (animationName, nodeName, type) to avoid
    /// linear scans during rendering. Built once when the model is assigned.
    /// </summary>
    private readonly Dictionary<(string anim, string node, KeyframeType type), List<Keyframe>> keyframeCache;

    private readonly IMeshDrawer meshDrawer;

    /// <summary>
    /// Initializes a new <see cref="AnimatedModelRenderer"/> for the given
    /// <paramref name="game"/> and <paramref name="model"/>.
    /// </summary>
    public AnimatedModelRenderer(IMeshDrawer meshDrawer, IOpenGlService openGlService)
    {
        keyframeCache = [];
        this.meshDrawer = meshDrawer;
        this.openGlService = openGlService;
    }

    public void Start(IGame game_, AnimatedModel model_)
    {
        m = model_;
        BuildKeyframeCache();
    }

    /// <summary>
    /// Pre-groups all keyframes by (animationName, nodeName, type) after
    /// deserialization to avoid linear scans during rendering.
    /// Called once after the model is assigned in <see cref="Start"/>.
    /// </summary>
    private void BuildKeyframeCache()
    {
        keyframeCache.Clear();
        foreach (Keyframe k in m.Keyframes)
        {
            (string AnimationName, string NodeName, KeyframeType Type) key = (k.AnimationName, k.NodeName, k.Type);
            if (!keyframeCache.TryGetValue(key, out List<Keyframe> list))
            {
                list = [];
                keyframeCache[key] = list;
            }

            list.Add(k);
        }
    }

    /// <summary>
    /// Advances the animation and draws the full node hierarchy for this frame.
    /// </summary>
    /// <param name="dt">Delta time in seconds since the last frame.</param>
    /// <param name="headDeg">Head pitch rotation in degrees, applied to nodes marked as head.</param>
    /// <param name="walkAnimation">When true, the animation eases to a rest pose when not moving.</param>
    /// <param name="moves">Whether the entity is currently moving.</param>
    /// <param name="light">Light intensity applied to the model, in the range 0-1.</param>
    public void Render(float dt, float headDeg, bool walkAnimation, bool moves, float light)
    {
        if (m == null)
        {
            return;
        }

        if (m.Animations == null)
        {
            return;
        }

        if (m.Animations[anim] == null)
        {
            return;
        }

        float length = m.Animations[anim].Length;
        if (moves)
        {
            frame += dt * fps;
            frame %= length;
        }

        if (walkAnimation)
        {
            // Ease the animation to the nearest rest pose (frame 0 or length/2)
            // rather than snapping, so the walk cycle finishes smoothly.
            float half = length / 2;
            if (frame != 0 && frame != half && frame != length)
            {
                frame += dt * fps;
                frame = frame < half
                    ? Math.Min(frame, half)
                    : Math.Min(frame, length);
            }
        }

        DrawNode("root", headDeg, light);
    }

    /// <summary>
    /// Recursively draws the node with the given <paramref name="parent"/> name
    /// and all of its children, applying animated transforms at each level.
    /// </summary>
    /// <param name="parent">The name of the parent node whose children to draw.</param>
    /// <param name="headDeg">Head pitch in degrees, applied to nodes marked as head.</param>
    /// <param name="light">Light intensity in the range 0-1.</param>
    private void DrawNode(string parent, float headDeg, float light)
    {
        for (int i = 0; i < m.Nodes.Count; i++)
        {
            Node n = m.Nodes[i];
            if (n == null)
            {
                continue;
            }

            if (n.ParentName != parent)
            {
                continue;
            }

            meshDrawer.GLPushMatrix();

            RectangleF[] r = CuboidRenderer.CuboidNet(n.SizeX, n.SizeY, n.SizeZ, n.U, n.V);
            CuboidRenderer.CuboidNetNormalize(r, m.Global.TexW, m.Global.TexH);

            // Scale — applied first, in local space
            Vector3 scale = GetAnimation(n, KeyframeType.Scale);
            if (scale != Vector3.Zero)
            {
                meshDrawer.GLScale(scale.X, scale.Y, scale.Z);
            }

            // Position — convert from model units to world units
            Vector3 position = GetAnimation(n, KeyframeType.Position) / ModelUnitScale;
            if (position != Vector3.Zero)
            {
                meshDrawer.GLTranslate(position[0], position[1], position[2]);
            }

            // Rotation — applied per axis to match GL convention
            Vector3 rotation = GetAnimation(n, KeyframeType.Rotation);
            if (rotation.X != 0)
            {
                meshDrawer.GLRotate(rotation.X, 1, 0, 0);
            }

            if (rotation.Y != 0)
            {
                meshDrawer.GLRotate(rotation.Y, 0, 1, 0);
            }

            if (rotation.Z != 0)
            {
                meshDrawer.GLRotate(rotation.Z, 0, 0, 1);
            }

            // Head pitch — applied after rotation so it compounds correctly
            if (n.Head == 1)
            {
                meshDrawer.GLRotate(headDeg, 1, 0, 0);
            }

            // Pivot — shifts the center of rotation, convert from model units
            Vector3 pivot = GetAnimation(n, KeyframeType.Pivot) / ModelUnitScale;
            meshDrawer.GLTranslate(pivot.X, pivot.Y, pivot.Z);

            // Size — the actual cuboid dimensions, convert from model units
            Vector3 size = GetAnimation(n, KeyframeType.Size) / ModelUnitScale;
            CuboidRenderer.DrawCuboidModel(openGlService, meshDrawer,
                -size.X / 2, -size.Y / 2, -size.Z / 2,
                size.X, size.Y, size.Z,
                r, light);

            // Recurse into children
            DrawNode(n.Name, headDeg, light);

            meshDrawer.GLPopMatrix();
        }
    }

    /// <summary>
    /// Returns the interpolated value of the given <paramref name="type"/> for
    /// <paramref name="node"/> at the current frame position.
    /// Falls back to the node's default value if no keyframes exist for this type.
    /// </summary>
    /// <param name="node">The node to animate.</param>
    /// <param name="type">The property type to interpolate.</param>
    /// <returns>The interpolated value as a <see cref="Vector3"/>.</returns>
    private Vector3 GetAnimation(Node node, KeyframeType type)
    {
        List<Keyframe> frames = GetFrames(node.Name, type);

        if (frames == null || frames.Count == 0)
        {
            return GetDefaultFrame(node, type);
        }

        int currentI = GetFrameCurrent(frames);
        int nextI = (currentI + 1) % frames.Count;

        Keyframe current = frames[currentI];
        Keyframe next = frames[nextI];
        float length = m.Animations[anim].Length;

        float t;
        if (next.Frame == current.Frame)
        {
            // Same frame — no interpolation needed
            t = 0;
        }
        else if (next.Frame > current.Frame)
        {
            // Normal forward interpolation
            t = (frame - current.Frame) / (next.Frame - current.Frame);
        }
        else
        {
            // Next frame wraps around past the end of the animation
            float elapsed = frame >= current.Frame
                ? frame - current.Frame
                : length - current.Frame + frame;

            t = elapsed / (length - current.Frame + next.Frame);
        }

        return new Vector3(
            MathHelper.Lerp(current.X, next.X, t),
            MathHelper.Lerp(current.Y, next.Y, t),
            MathHelper.Lerp(current.Z, next.Z, t)
        );
    }

    /// <summary>
    /// Returns the default (non-animated) value for the given <paramref name="type"/>
    /// from the node's base properties, used when no keyframe exists for this type.
    /// </summary>
    /// <param name="node">The node to read default values from.</param>
    /// <param name="type">The property type to retrieve.</param>
    /// <returns>The default value as a <see cref="Vector3"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="type"/> is not a recognised <see cref="KeyframeType"/>.
    /// </exception>
    private static Vector3 GetDefaultFrame(Node node, KeyframeType type)
    {
        return type switch
        {
            KeyframeType.Position => new Vector3(node.PosX, node.PosY, node.PosZ),
            KeyframeType.Rotation => new Vector3(node.RotateX, node.RotateY, node.RotateZ),
            KeyframeType.Size => new Vector3(node.SizeX, node.SizeY, node.SizeZ),
            KeyframeType.Pivot => new Vector3(node.PivotX, node.PivotY, node.PivotZ),
            KeyframeType.Scale => new Vector3(node.ScaleX, node.ScaleY, node.ScaleZ),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown KeyframeType.")
        };
    }

    /// <summary>
    /// Returns all keyframes matching the given <paramref name="nodeName"/> and
    /// <paramref name="type"/> in the current animation, using the pre-built cache.
    /// </summary>
    /// <param name="nodeName">The node to filter by.</param>
    /// <param name="type">The keyframe type to filter by.</param>
    /// <returns>
    /// The matching keyframes, or <c>null</c> if none exist for this combination.
    /// </returns>
    private List<Keyframe> GetFrames(string nodeName, KeyframeType type)
    {
        (string Name, string nodeName, KeyframeType type) key = (m.Animations[anim].Name, nodeName, type);
        keyframeCache.TryGetValue(key, out List<Keyframe> frames);
        return frames;
    }

    /// <summary>
    /// Finds the index of the keyframe that is closest to and at or before
    /// the current <see cref="frame"/> position.
    /// If no keyframe exists at or before the current position (i.e. we are
    /// before the first keyframe), returns the last keyframe to allow
    /// wrap-around interpolation.
    /// </summary>
    /// <param name="frames">The keyframes to search, filtered by node and type.</param>
    /// <returns>
    /// Index of the current keyframe, or -1 if <paramref name="frames"/> is empty.
    /// </returns>
    private int GetFrameCurrent(List<Keyframe> frames)
    {
        int current = -1;

        for (int i = 0; i < frames.Count; i++)
        {
            if (current == -1)
            {
                // Accept any frame as a starting candidate
                current = i;
                continue;
            }

            Keyframe k = frames[i];

            if (k.Frame <= frame && k.Frame > frames[current].Frame)
            {
                // Closer previous frame found
                current = i;
            }
            else if (frames[current].Frame > frame && k.Frame > frames[current].Frame)
            {
                // We haven't found any frame at or before current position yet,
                // so track the latest frame for wrap-around fallback
                current = i;
            }
        }

        return current;
    }
}
