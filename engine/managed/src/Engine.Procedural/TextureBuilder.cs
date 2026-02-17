using System.Numerics;

namespace Engine.Procedural;

public enum ProceduralTextureKind
{
    Perlin = 0,
    Simplex = 1,
    Worley = 2,
    Grid = 3,
    Brick = 4,
    Stripes = 5
}

public sealed record ProceduralTextureRecipe(
    ProceduralTextureKind Kind,
    int Width,
    int Height,
    uint Seed,
    int FbmOctaves = 4,
    float Frequency = 4f)
{
    public ProceduralTextureRecipe Validate()
    {
        if (!Enum.IsDefined(Kind))
        {
            throw new InvalidDataException($"Unsupported procedural texture kind: {Kind}.");
        }

        if (Width <= 0 || Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Width), "Texture dimensions must be greater than zero.");
        }

        if (FbmOctaves <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(FbmOctaves), "FBM octaves must be greater than zero.");
        }

        if (Frequency <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(Frequency), "Frequency must be greater than zero.");
        }

        return this;
    }
}

public static partial class TextureBuilder
{
    public static float[] GenerateHeight(ProceduralTextureRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        _ = recipe.Validate();

        var data = new float[checked(recipe.Width * recipe.Height)];
        for (int y = 0; y < recipe.Height; y++)
        {
            for (int x = 0; x < recipe.Width; x++)
            {
                float u = x / (float)recipe.Width;
                float v = y / (float)recipe.Height;
                data[y * recipe.Width + x] = recipe.Kind switch
                {
                    ProceduralTextureKind.Perlin => FractalNoise(u, v, recipe.Seed, recipe.FbmOctaves, recipe.Frequency),
                    ProceduralTextureKind.Simplex => FractalNoise(u + 13.37f, v - 3.11f, recipe.Seed ^ 0x9E3779B9u, recipe.FbmOctaves, recipe.Frequency),
                    ProceduralTextureKind.Worley => Worley(u, v, recipe.Seed, recipe.Frequency),
                    ProceduralTextureKind.Grid => Grid(u, v),
                    ProceduralTextureKind.Brick => Brick(u, v),
                    ProceduralTextureKind.Stripes => Stripes(u),
                    _ => throw new InvalidDataException($"Unsupported procedural texture kind: {recipe.Kind}.")
                };
            }
        }

        return data;
    }

    public static byte[] GenerateRgba8(ProceduralTextureRecipe recipe)
    {
        float[] height = GenerateHeight(recipe);
        var bytes = new byte[checked(recipe.Width * recipe.Height * 4)];

        for (int i = 0; i < height.Length; i++)
        {
            byte sample = (byte)Math.Clamp((int)MathF.Round(height[i] * 255f), 0, 255);
            int offset = i * 4;
            bytes[offset] = sample;
            bytes[offset + 1] = sample;
            bytes[offset + 2] = sample;
            bytes[offset + 3] = 255;
        }

        return bytes;
    }

    public static byte[] HeightToNormalMap(float[] height, int width, int heightPixels, float strength = 1f)
    {
        ArgumentNullException.ThrowIfNull(height);
        if (width <= 0 || heightPixels <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Dimensions must be greater than zero.");
        }

        if (height.Length != width * heightPixels)
        {
            throw new InvalidDataException("Height array size does not match dimensions.");
        }

        if (strength <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(strength), "Normal strength must be greater than zero.");
        }

        var normal = new byte[checked(width * heightPixels * 4)];
        for (int y = 0; y < heightPixels; y++)
        {
            int yPrev = Math.Max(y - 1, 0);
            int yNext = Math.Min(y + 1, heightPixels - 1);

            for (int x = 0; x < width; x++)
            {
                int xPrev = Math.Max(x - 1, 0);
                int xNext = Math.Min(x + 1, width - 1);

                float dx = height[y * width + xNext] - height[y * width + xPrev];
                float dy = height[yNext * width + x] - height[yPrev * width + x];
                var n = Vector3.Normalize(new Vector3(-dx * strength, 1f, -dy * strength));

                int offset = (y * width + x) * 4;
                normal[offset] = EncodeSignedNormal(n.X);
                normal[offset + 1] = EncodeSignedNormal(n.Y);
                normal[offset + 2] = EncodeSignedNormal(n.Z);
                normal[offset + 3] = 255;
            }
        }

        return normal;
    }

    private static byte EncodeSignedNormal(float value)
    {
        return (byte)Math.Clamp((int)MathF.Round((value * 0.5f + 0.5f) * 255f), 0, 255);
    }

    private static float FractalNoise(float u, float v, uint seed, int octaves, float baseFrequency)
    {
        float amplitude = 1f;
        float frequency = baseFrequency;
        float sum = 0f;
        float amplitudeSum = 0f;

        for (int octave = 0; octave < octaves; octave++)
        {
            sum += ValueNoise(u * frequency, v * frequency, seed ^ (uint)octave * 0x45D9F3Bu) * amplitude;
            amplitudeSum += amplitude;
            amplitude *= 0.5f;
            frequency *= 2f;
        }

        return Math.Clamp(sum / MathF.Max(amplitudeSum, float.Epsilon), 0f, 1f);
    }

    private static float Worley(float u, float v, uint seed, float frequency)
    {
        float x = u * frequency;
        float y = v * frequency;
        int cellX = (int)MathF.Floor(x);
        int cellY = (int)MathF.Floor(y);

        float nearest = float.MaxValue;
        for (int oy = -1; oy <= 1; oy++)
        {
            for (int ox = -1; ox <= 1; ox++)
            {
                int px = cellX + ox;
                int py = cellY + oy;
                float fx = px + Hash01((uint)px, (uint)py, seed);
                float fy = py + Hash01((uint)py, (uint)px, seed ^ 0xB5297A4Du);
                float dx = fx - x;
                float dy = fy - y;
                nearest = MathF.Min(nearest, MathF.Sqrt(dx * dx + dy * dy));
            }
        }

        return 1f - Math.Clamp(nearest, 0f, 1f);
    }

    private static float Grid(float u, float v)
    {
        float gx = MathF.Abs((u * 10f) % 1f - 0.5f);
        float gy = MathF.Abs((v * 10f) % 1f - 0.5f);
        return gx < 0.05f || gy < 0.05f ? 1f : 0.1f;
    }

    private static float Brick(float u, float v)
    {
        float row = MathF.Floor(v * 8f);
        float offset = ((int)row & 1) == 0 ? 0f : 0.5f;
        float cellU = (u * 8f + offset) % 1f;
        float cellV = (v * 8f) % 1f;
        bool mortar = cellU < 0.08f || cellV < 0.08f;
        return mortar ? 0.05f : 0.7f;
    }

    private static float Stripes(float u)
    {
        return ((int)MathF.Floor(u * 16f) & 1) == 0 ? 0.2f : 0.8f;
    }

    private static float ValueNoise(float x, float y, uint seed)
    {
        int x0 = (int)MathF.Floor(x);
        int y0 = (int)MathF.Floor(y);
        int x1 = x0 + 1;
        int y1 = y0 + 1;

        float tx = x - x0;
        float ty = y - y0;

        float n00 = Hash01((uint)x0, (uint)y0, seed);
        float n10 = Hash01((uint)x1, (uint)y0, seed);
        float n01 = Hash01((uint)x0, (uint)y1, seed);
        float n11 = Hash01((uint)x1, (uint)y1, seed);

        float ix0 = Lerp(n00, n10, SmoothStep(tx));
        float ix1 = Lerp(n01, n11, SmoothStep(tx));
        return Lerp(ix0, ix1, SmoothStep(ty));
    }

    private static float SmoothStep(float t)
    {
        return t * t * (3f - 2f * t);
    }

    private static float Lerp(float a, float b, float t)
    {
        return a + (b - a) * t;
    }

    private static float Hash01(uint x, uint y, uint seed)
    {
        uint hash = x;
        hash = (hash * 0x27D4EB2Du) ^ y;
        hash = (hash * 0x165667B1u) ^ seed;
        hash ^= hash >> 15;
        hash *= 0x2C1B3C6Du;
        hash ^= hash >> 12;
        return (hash & 0x00FFFFFFu) / 16777215f;
    }
}
