using System.Numerics;

namespace Engine.Cli;

internal static partial class ProceduralPreviewRasterizer
{
    private static Vector3 SampleRgb(TexturePayload texture, float u, float v)
    {
        return SampleRgb(texture.Rgba8, texture.Width, texture.Height, u, v);
    }

    private static Vector3 SampleRgb(byte[] rgba, int width, int height, float u, float v)
    {
        return SampleRgbaBilinear(rgba, width, height, u, v);
    }

    private static Vector3 SampleNormal(TexturePayload texture, float u, float v)
    {
        Vector3 encoded = SampleRgb(texture, u, v);
        Vector3 normal = new(
            encoded.X * 2f - 1f,
            encoded.Y * 2f - 1f,
            encoded.Z * 2f - 1f);
        return NormalizeOrFallback(normal, Vector3.UnitZ);
    }

    private static float SampleGray(TexturePayload texture, float u, float v)
    {
        Vector3 sample = SampleRgbaBilinear(texture.Rgba8, texture.Width, texture.Height, u, v);
        return sample.X;
    }

    private static Vector3 SampleRgbaBilinear(byte[] rgba, int width, int height, float u, float v)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Texture dimensions must be greater than zero.");
        }

        float wrappedU = u - MathF.Floor(u);
        float wrappedV = v - MathF.Floor(v);
        float x = wrappedU * width - 0.5f;
        float y = (1f - wrappedV) * height - 0.5f;

        int x0 = (int)MathF.Floor(x);
        int y0 = (int)MathF.Floor(y);
        int x1 = x0 + 1;
        int y1 = y0 + 1;

        float tx = x - x0;
        float ty = y - y0;

        Vector3 c00 = ReadPixelRgb(rgba, width, height, x0, y0);
        Vector3 c10 = ReadPixelRgb(rgba, width, height, x1, y0);
        Vector3 c01 = ReadPixelRgb(rgba, width, height, x0, y1);
        Vector3 c11 = ReadPixelRgb(rgba, width, height, x1, y1);

        Vector3 top = Vector3.Lerp(c00, c10, tx);
        Vector3 bottom = Vector3.Lerp(c01, c11, tx);
        return Vector3.Lerp(top, bottom, ty);
    }

    private static Vector3 ReadPixelRgb(byte[] rgba, int width, int height, int x, int y)
    {
        int wrappedX = WrapPixel(x, width);
        int wrappedY = WrapPixel(y, height);
        int offset = ((wrappedY * width) + wrappedX) * 4;
        return new Vector3(
            rgba[offset] / 255f,
            rgba[offset + 1] / 255f,
            rgba[offset + 2] / 255f);
    }

    private static int WrapPixel(int value, int size)
    {
        int mod = value % size;
        return mod < 0 ? mod + size : mod;
    }

    private static Vector3 ComputeSurfaceTangent(Vector3 normal)
    {
        Vector3 axis = MathF.Abs(normal.Y) < 0.95f ? Vector3.UnitY : Vector3.UnitX;
        Vector3 tangent = Vector3.Cross(axis, normal);
        return NormalizeOrFallback(tangent, Vector3.UnitX);
    }

    private static Vector3 OrthonormalizeTangent(Vector3 normal, Vector3 tangent)
    {
        Vector3 safeNormal = NormalizeOrFallback(normal, Vector3.UnitZ);
        Vector3 safeTangent = NormalizeOrFallback(tangent, ComputeSurfaceTangent(safeNormal));
        Vector3 projected = safeTangent - safeNormal * Vector3.Dot(safeNormal, safeTangent);
        return NormalizeOrFallback(projected, ComputeSurfaceTangent(safeNormal));
    }

    private static Vector3 ApplyNormalMap(
        Vector3 geometricNormal,
        Vector3 tangent,
        Vector3 bitangent,
        Vector3 sampledNormal,
        float strength)
    {
        Vector3 safeNormal = NormalizeOrFallback(geometricNormal, Vector3.UnitZ);
        Vector3 safeTangent = OrthonormalizeTangent(safeNormal, tangent);
        Vector3 safeBitangent = NormalizeOrFallback(
            bitangent - safeNormal * Vector3.Dot(safeNormal, bitangent),
            Vector3.Cross(safeNormal, safeTangent));
        safeBitangent = NormalizeOrFallback(safeBitangent, Vector3.Cross(safeNormal, safeTangent));
        Vector3 tangentNormal = NormalizeOrFallback(
            new Vector3(sampledNormal.X * strength, sampledNormal.Y * strength, MathF.Max(0.01f, sampledNormal.Z)),
            Vector3.UnitZ);
        Vector3 worldNormal =
            safeTangent * tangentNormal.X +
            safeBitangent * tangentNormal.Y +
            safeNormal * tangentNormal.Z;
        return NormalizeOrFallback(worldNormal, safeNormal);
    }

    private static Vector3 NormalizeOrFallback(Vector3 value, Vector3 fallback)
    {
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z) || value.LengthSquared() <= 1e-8f)
        {
            return fallback;
        }

        Vector3 normalized = Vector3.Normalize(value);
        if (!float.IsFinite(normalized.X) || !float.IsFinite(normalized.Y) || !float.IsFinite(normalized.Z))
        {
            return fallback;
        }

        return normalized;
    }
}
