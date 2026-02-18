namespace Engine.Cli;

internal static partial class ProceduralPreviewRasterizer
{
    private static float Edge(float ax, float ay, float bx, float by, float px, float py)
    {
        return (px - ax) * (by - ay) - (py - ay) * (bx - ax);
    }

    private static float Lerp(float a, float b, float t)
    {
        return a + ((b - a) * t);
    }

    private static uint Hash(uint seed, uint x, uint y)
    {
        uint value = seed ^ (x * 374761393u) ^ (y * 668265263u);
        value ^= value >> 13;
        value *= 1274126177u;
        value ^= value >> 16;
        return value;
    }

    private static float Sample01(uint seed, uint x, uint y)
    {
        return (Hash(seed, x, y) & 0x00FFFFFFu) / 16777215f;
    }

    private static byte ToByte(float value)
    {
        int rounded = (int)MathF.Round(value);
        if (rounded <= 0)
        {
            return 0;
        }

        return rounded >= 255 ? (byte)255 : (byte)rounded;
    }
}
