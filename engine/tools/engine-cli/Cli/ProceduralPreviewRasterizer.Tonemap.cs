using System.Numerics;

namespace Engine.Cli;

internal static partial class ProceduralPreviewRasterizer
{
    private static Vector3 ToneMapToDisplay(Vector3 linearColor)
    {
        return new Vector3(
            ToneMapChannel(linearColor.X),
            ToneMapChannel(linearColor.Y),
            ToneMapChannel(linearColor.Z));
    }

    private static float ToneMapChannel(float linearValue)
    {
        float x = MathF.Max(0f, linearValue);
        float mapped = (x * (2.51f * x + 0.03f)) / (x * (2.43f * x + 0.59f) + 0.14f);
        return MathF.Pow(Math.Clamp(mapped, 0f, 1f), 1f / 2.2f);
    }
}
