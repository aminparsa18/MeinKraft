public class ColorUtils
{
    public static int ColorFromArgb(int a, int r, int g, int b)
        => (a << 24) | (r << 16) | (g << 8) | b;

    public static int ColorA(int color) => (color >> 24) & 0xFF;
    public static int ColorR(int color) => (color >> 16) & 0xFF;
    public static int ColorG(int color) => (color >> 8) & 0xFF;
    public static int ColorB(int color) => color & 0xFF;

    public static int InterpolateColor(float progress, int[] colors, int colorsLength)
    {
        float one = 1;
        int colora = (int)((colorsLength - 1) * progress);
        if (colora < 0)
        {
            colora = 0;
        }

        if (colora >= colorsLength)
        {
            colora = colorsLength - 1;
        }

        int colorb = colora + 1;
        if (colorb >= colorsLength)
        {
            colorb = colorsLength - 1;
        }

        int a = colors[colora];
        int b = colors[colorb];
        float p = (progress - (one * colora / (colorsLength - 1))) * (colorsLength - 1);
        int A = (int)(ColorA(a) + ((ColorA(b) - ColorA(a)) * p));
        int R = (int)(ColorR(a) + ((ColorR(b) - ColorR(a)) * p));
        int G = (int)(ColorG(a) + ((ColorG(b) - ColorG(a)) * p));
        int B = (int)(ColorB(a) + ((ColorB(b) - ColorB(a)) * p));
        return ColorFromArgb(A, R, G, B);
    }
}