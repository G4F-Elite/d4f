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
    float Frequency = 4f,
    float DomainWarpStrength = 0f,
    float DomainWarpFrequency = 8f)
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

        if (!float.IsFinite(DomainWarpStrength) || DomainWarpStrength < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(DomainWarpStrength), "Domain warp strength must be finite and non-negative.");
        }

        if (!float.IsFinite(DomainWarpFrequency) || DomainWarpFrequency <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(DomainWarpFrequency), "Domain warp frequency must be finite and greater than zero.");
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
                (float sampleU, float sampleV) = ApplyDomainWarp(u, v, recipe);
                data[y * recipe.Width + x] = recipe.Kind switch
                {
                    ProceduralTextureKind.Perlin => FractalNoise(sampleU, sampleV, recipe.Seed, recipe.FbmOctaves, recipe.Frequency),
                    ProceduralTextureKind.Simplex => FractalSimplexNoise(sampleU, sampleV, recipe.Seed, recipe.FbmOctaves, recipe.Frequency),
                    ProceduralTextureKind.Worley => FractalWorleyNoise(sampleU, sampleV, recipe.Seed, recipe.FbmOctaves, recipe.Frequency),
                    ProceduralTextureKind.Grid => Grid(sampleU, sampleV, recipe.Frequency, recipe.Seed),
                    ProceduralTextureKind.Brick => Brick(sampleU, sampleV, recipe.Frequency, recipe.Seed),
                    ProceduralTextureKind.Stripes => Stripes(sampleU, recipe.Frequency, recipe.Seed),
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

    private static float FractalSimplexNoise(float u, float v, uint seed, int octaves, float baseFrequency)
    {
        float amplitude = 1f;
        float frequency = baseFrequency;
        float sum = 0f;
        float amplitudeSum = 0f;

        for (int octave = 0; octave < octaves; octave++)
        {
            uint octaveSeed = seed ^ (uint)octave * 0x9E3779B9u;
            float sample = SimplexNoise2D(u * frequency, v * frequency, octaveSeed);
            sum += sample * amplitude;
            amplitudeSum += amplitude;
            amplitude *= 0.5f;
            frequency *= 2f;
        }

        return Math.Clamp(sum / MathF.Max(amplitudeSum, float.Epsilon), 0f, 1f);
    }

    private static float FractalWorleyNoise(float u, float v, uint seed, int octaves, float baseFrequency)
    {
        float amplitude = 1f;
        float frequency = baseFrequency;
        float sum = 0f;
        float amplitudeSum = 0f;

        for (int octave = 0; octave < octaves; octave++)
        {
            uint octaveSeed = seed ^ (uint)octave * 0xA24BAED5u;
            float sample = Worley(u, v, octaveSeed, frequency);
            sum += sample * amplitude;
            amplitudeSum += amplitude;
            amplitude *= 0.5f;
            frequency *= 2f;
        }

        return Math.Clamp(sum / MathF.Max(amplitudeSum, float.Epsilon), 0f, 1f);
    }

    private static float SimplexNoise2D(float x, float y, uint seed)
    {
        const float f2 = 0.366025403f; // (sqrt(3)-1)/2
        const float g2 = 0.211324865f; // (3-sqrt(3))/6

        float skew = (x + y) * f2;
        int i = (int)MathF.Floor(x + skew);
        int j = (int)MathF.Floor(y + skew);

        float unskew = (i + j) * g2;
        float x0 = x - (i - unskew);
        float y0 = y - (j - unskew);

        int i1 = x0 > y0 ? 1 : 0;
        int j1 = x0 > y0 ? 0 : 1;

        float x1 = x0 - i1 + g2;
        float y1 = y0 - j1 + g2;
        float x2 = x0 - 1f + 2f * g2;
        float y2 = y0 - 1f + 2f * g2;

        float n0 = SimplexContribution(i, j, x0, y0, seed);
        float n1 = SimplexContribution(i + i1, j + j1, x1, y1, seed);
        float n2 = SimplexContribution(i + 1, j + 1, x2, y2, seed);

        float value = 70f * (n0 + n1 + n2);
        return Math.Clamp(value * 0.5f + 0.5f, 0f, 1f);
    }

    private static float SimplexContribution(int i, int j, float x, float y, uint seed)
    {
        float radius = 0.5f - x * x - y * y;
        if (radius <= 0f)
        {
            return 0f;
        }

        uint hash = Hash(unchecked((uint)i), unchecked((uint)j), seed);
        float gradientDot = GradientDot2D(hash, x, y);
        float radius2 = radius * radius;
        return radius2 * radius2 * gradientDot;
    }

    private static float GradientDot2D(uint hash, float x, float y)
    {
        return (hash & 7u) switch
        {
            0u => x + y,
            1u => -x + y,
            2u => x - y,
            3u => -x - y,
            4u => x,
            5u => -x,
            6u => y,
            _ => -y
        };
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

    private static float Grid(float u, float v, float frequency, uint seed)
    {
        float frequencyScale = 0.75f + Hash01(seed, 10u, 0x60D17B5Bu) * 0.5f;
        float repeat = MathF.Max(1f, frequency * 2.5f * frequencyScale);
        float offsetU = Hash01(seed, 0u, 0x5F356495u);
        float offsetV = Hash01(0u, seed, 0xB79F3ABDu);
        float gx = MathF.Abs(Frac(u * repeat + offsetU) - 0.5f);
        float gy = MathF.Abs(Frac(v * repeat + offsetV) - 0.5f);
        float lineWidth = Math.Clamp(0.04f + 0.06f / repeat, 0.015f, 0.06f);
        float lineValue = 0.85f + Hash01(seed, 20u, 0xD331A5E7u) * 0.15f;
        float fillValue = 0.05f + Hash01(seed, 21u, 0x54A9BFC1u) * 0.2f;
        return gx < lineWidth || gy < lineWidth ? lineValue : fillValue;
    }

    private static float Brick(float u, float v, float frequency, uint seed)
    {
        float repeatXScale = 0.8f + Hash01(seed, 11u, 0x9A4FC29Du) * 0.4f;
        float repeatYScale = 0.8f + Hash01(seed, 12u, 0x3B70E4F3u) * 0.4f;
        float repeatY = MathF.Max(1f, frequency * 2f * repeatYScale);
        float repeatX = MathF.Max(1f, frequency * 2f * repeatXScale);
        float rowNoise = Hash01(seed, 1u, 0xC7A4B61Du);
        float row = MathF.Floor(v * repeatY + rowNoise);
        float stagger = ((int)row & 1) == 0 ? 0f : 0.5f;

        float offsetU = Hash01(seed, 2u, 0xA53A89EFu);
        float offsetV = Hash01(seed, 3u, 0x23F5A4C9u);
        float cellU = Frac(u * repeatX + stagger + offsetU);
        float cellV = Frac(v * repeatY + offsetV);
        float mortarWidth = Math.Clamp(0.03f + 0.08f / repeatX, 0.015f, 0.08f);
        bool mortar = cellU < mortarWidth || cellV < mortarWidth;
        float mortarValue = 0.03f + Hash01(seed, 22u, 0xF1EBC7A9u) * 0.08f;
        float brickValue = 0.55f + Hash01(seed, 23u, 0x8C52B4D1u) * 0.35f;
        return mortar ? mortarValue : brickValue;
    }

    private static float Stripes(float u, float frequency, uint seed)
    {
        float frequencyScale = 0.75f + Hash01(seed, 13u, 0xE79A5C31u) * 0.5f;
        float repeat = MathF.Max(1f, frequency * 4f * frequencyScale);
        float offset = Hash01(seed, 4u, 0x4B1C9E3Du);
        float lowValue = 0.15f + Hash01(seed, 24u, 0x53A8D1B7u) * 0.2f;
        float highValue = 0.65f + Hash01(seed, 25u, 0xA24197E3u) * 0.3f;
        return ((int)MathF.Floor(u * repeat + offset) & 1) == 0 ? lowValue : highValue;
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

    private static float Frac(float value)
    {
        return value - MathF.Floor(value);
    }

    private static float Lerp(float a, float b, float t)
    {
        return a + (b - a) * t;
    }

    private static float Hash01(uint x, uint y, uint seed)
    {
        uint hash = Hash(x, y, seed);
        return (hash & 0x00FFFFFFu) / 16777215f;
    }

    private static uint Hash(uint x, uint y, uint seed)
    {
        uint hash = x;
        hash = (hash * 0x27D4EB2Du) ^ y;
        hash = (hash * 0x165667B1u) ^ seed;
        hash ^= hash >> 15;
        hash *= 0x2C1B3C6Du;
        hash ^= hash >> 12;
        return hash;
    }
}
