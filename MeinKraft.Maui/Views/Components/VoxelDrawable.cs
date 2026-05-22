internal sealed class VoxelDrawable : IDrawable
{
    private const float TW = 28f;
    private const float TH = 16f;
    private const float FH = 32f;

    internal const double CycleMs = 3100;
    private const double InDur = 580;
    private const double HoldIn = 940;
    private const double HoldOut = 2120;
    private const double OutDur = 580;
    private const double StaggerMs = 88;

    private static readonly Color TopColor = Color.FromArgb("#7C5B31");
    private static readonly Color LeftColor = Color.FromArgb("#3D6B12");
    private static readonly Color RightColor = Color.FromArgb("#C9BB8D");
    private static readonly Color EdgeColor = Color.FromArgb("#0A0A0A");

    private sealed record Block(float Gx, float Gz, float Gy = 0);
    private sealed record Formation(Block[] Blocks, float Dx, float Dz);

    internal static readonly string[] Labels =
    [
        "Loading assets",
        "Connecting",
        "Generating world",
        "Spawning player",
    ];

    private static readonly Formation[] Formations =
    [
        new([new(-1.5f,0), new(-.5f,0), new(.5f,0), new(1.5f,0)],     -11f,   0f),
        new([new(-.5f,-.5f), new(.5f,-.5f), new(-.5f,.5f), new(.5f,.5f)], 0f, -11f),
        new([new(-1f,-.5f), new(0,-.5f), new(1f,-.5f), new(-1f,.5f)],  11f,   0f),
        new([new(-1.5f,0), new(-.5f,0), new(.5f,0), new(.5f,0,1)],      0f,  11f),
    ];

    public double ElapsedMs { get; set; }

    public void Draw(ICanvas canvas, RectF rect)
    {
        canvas.Antialias = true;

        float cx = rect.Width * 0.5f;
        float cy = rect.Height * 0.46f;

        int fi = (int)(Math.Floor(ElapsedMs / CycleMs) % Formations.Length);
        double e = ElapsedMs % CycleMs;
        var form = Formations[fi];

        var animated = new (float ax, float az, float gy, float depth)[form.Blocks.Length];

        for (int i = 0; i < form.Blocks.Length; i++)
        {
            var b = form.Blocks[i];
            double lag = i * StaggerMs;
            float ax = b.Gx, az = b.Gz;

            if (e < HoldIn)
            {
                float t = Clamp01((e - lag) / InDur);
                ax = Lerp(b.Gx + form.Dx, b.Gx, t);
                az = Lerp(b.Gz + form.Dz, b.Gz, t);
            }
            else if (e > HoldOut)
            {
                float t = Clamp01((e - HoldOut - lag) / OutDur);
                ax = Lerp(b.Gx, b.Gx - form.Dx, t);
                az = Lerp(b.Gz, b.Gz - form.Dz, t);
            }

            animated[i] = (ax, az, b.Gy, ax + az + b.Gy * 0.1f);
        }

        // Painter's algorithm: back to front
        Array.Sort(animated, (a, b) => a.depth.CompareTo(b.depth));

        canvas.StrokeColor = EdgeColor;
        canvas.StrokeSize = 1.6f;

        foreach (var (ax, az, gy, _) in animated)
        {
            float sx = cx + (ax - az) * TW;
            float sy = cy + (ax + az) * TH - gy * FH;
            DrawCube(canvas, sx, sy);
        }
    }

    private static void DrawCube(ICanvas canvas, float sx, float sy)
    {
        // Top face
        var top = new PathF();
        top.MoveTo(sx, sy - TH);
        top.LineTo(sx + TW, sy);
        top.LineTo(sx, sy + TH);
        top.LineTo(sx - TW, sy);
        top.Close();
        canvas.FillColor = TopColor;
        canvas.FillPath(top);
        canvas.DrawPath(top);

        // Left face
        var left = new PathF();
        left.MoveTo(sx - TW, sy);
        left.LineTo(sx, sy + TH);
        left.LineTo(sx, sy + TH + FH);
        left.LineTo(sx - TW, sy + FH);
        left.Close();
        canvas.FillColor = LeftColor;
        canvas.FillPath(left);
        canvas.DrawPath(left);

        // Right face
        var right = new PathF();
        right.MoveTo(sx, sy + TH);
        right.LineTo(sx + TW, sy);
        right.LineTo(sx + TW, sy + FH);
        right.LineTo(sx, sy + TH + FH);
        right.Close();
        canvas.FillColor = RightColor;
        canvas.FillPath(right);
        canvas.DrawPath(right);
    }

    private static float Lerp(float a, float b, double t) => (float)(a + (b - a) * t);
    private static float Clamp01(double t) => t < 0 ? 0f : t > 1 ? 1f : (float)t;
}