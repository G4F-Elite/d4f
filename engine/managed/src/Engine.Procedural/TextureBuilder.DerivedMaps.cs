namespace Engine.Procedural;

public static partial class TextureBuilder
{
    public static ProceduralTextureSurface GenerateSurfaceMaps(
        ProceduralTextureRecipe recipe,
        float normalStrength = 1f,
        float roughnessContrast = 2f,
        int aoRadius = 2,
        float aoStrength = 1f)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        _ = recipe.Validate();

        float[] height = GenerateHeight(recipe);
        byte[] albedo = GenerateRgba8(recipe);
        byte[] normal = HeightToNormalMap(height, recipe.Width, recipe.Height, normalStrength);
        byte[] roughness = HeightToRoughnessMap(height, recipe.Width, recipe.Height, roughnessContrast);
        byte[] ao = HeightToAmbientOcclusionMap(height, recipe.Width, recipe.Height, aoRadius, aoStrength);
        IReadOnlyList<TextureMipLevel> mipChain = GenerateMipChainRgba8(albedo, recipe.Width, recipe.Height);

        return new ProceduralTextureSurface(
            Width: recipe.Width,
            Height: recipe.Height,
            HeightMap: height,
            AlbedoRgba8: albedo,
            NormalRgba8: normal,
            RoughnessRgba8: roughness,
            AmbientOcclusionRgba8: ao,
            MipChain: mipChain).Validate();
    }

    public static byte[] HeightToRoughnessMap(
        float[] height,
        int width,
        int heightPixels,
        float contrast = 2f,
        float baseRoughness = 0.15f)
    {
        ValidateHeightArray(height, width, heightPixels);

        if (contrast <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(contrast), "Roughness contrast must be greater than zero.");
        }

        if (baseRoughness < 0f || baseRoughness > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(baseRoughness), "Base roughness must be within [0,1].");
        }

        var roughness = new float[height.Length];
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
                float slope = MathF.Sqrt(dx * dx + dy * dy);
                float value = Math.Clamp(baseRoughness + slope * contrast, 0f, 1f);
                roughness[y * width + x] = value;
            }
        }

        return ToGrayscaleRgba(roughness);
    }

    public static byte[] HeightToAmbientOcclusionMap(
        float[] height,
        int width,
        int heightPixels,
        int radius = 2,
        float strength = 1f)
    {
        ValidateHeightArray(height, width, heightPixels);

        if (radius <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), "AO radius must be greater than zero.");
        }

        if (strength <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(strength), "AO strength must be greater than zero.");
        }

        var ao = new float[height.Length];
        for (int y = 0; y < heightPixels; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float center = height[y * width + x];
                float occlusion = 0f;
                float weightSum = 0f;

                for (int oy = -radius; oy <= radius; oy++)
                {
                    int sy = Math.Clamp(y + oy, 0, heightPixels - 1);
                    for (int ox = -radius; ox <= radius; ox++)
                    {
                        if (ox == 0 && oy == 0)
                        {
                            continue;
                        }

                        int sx = Math.Clamp(x + ox, 0, width - 1);
                        float neighbor = height[sy * width + sx];
                        float positiveDelta = Math.Max(0f, neighbor - center);
                        float weight = 1f / MathF.Sqrt(ox * ox + oy * oy);
                        occlusion += positiveDelta * weight;
                        weightSum += weight;
                    }
                }

                float normalizedOcclusion = weightSum <= float.Epsilon
                    ? 0f
                    : occlusion / weightSum;
                float aoValue = 1f - Math.Clamp(normalizedOcclusion * strength, 0f, 1f);
                ao[y * width + x] = aoValue;
            }
        }

        return ToGrayscaleRgba(ao);
    }

    public static IReadOnlyList<TextureMipLevel> GenerateMipChainRgba8(
        byte[] baseLevelRgba8,
        int width,
        int heightPixels)
    {
        ArgumentNullException.ThrowIfNull(baseLevelRgba8);
        if (width <= 0 || heightPixels <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Texture dimensions must be greater than zero.");
        }

        int expectedLength = checked(width * heightPixels * 4);
        if (baseLevelRgba8.Length != expectedLength)
        {
            throw new InvalidDataException(
                $"Base mip payload size {baseLevelRgba8.Length} does not match dimensions {width}x{heightPixels} ({expectedLength} bytes).");
        }

        var chain = new List<TextureMipLevel>();
        int currentWidth = width;
        int currentHeight = heightPixels;
        byte[] currentLevel = baseLevelRgba8.ToArray();
        chain.Add(new TextureMipLevel(currentWidth, currentHeight, currentLevel).Validate());

        while (currentWidth > 1 || currentHeight > 1)
        {
            int nextWidth = Math.Max(1, currentWidth / 2);
            int nextHeight = Math.Max(1, currentHeight / 2);
            var nextLevel = new byte[checked(nextWidth * nextHeight * 4)];

            for (int y = 0; y < nextHeight; y++)
            {
                int srcY0 = Math.Min(y * 2, currentHeight - 1);
                int srcY1 = Math.Min(srcY0 + 1, currentHeight - 1);

                for (int x = 0; x < nextWidth; x++)
                {
                    int srcX0 = Math.Min(x * 2, currentWidth - 1);
                    int srcX1 = Math.Min(srcX0 + 1, currentWidth - 1);

                    int s00 = (srcY0 * currentWidth + srcX0) * 4;
                    int s10 = (srcY0 * currentWidth + srcX1) * 4;
                    int s01 = (srcY1 * currentWidth + srcX0) * 4;
                    int s11 = (srcY1 * currentWidth + srcX1) * 4;
                    int dst = (y * nextWidth + x) * 4;

                    for (int c = 0; c < 4; c++)
                    {
                        int sum = currentLevel[s00 + c] + currentLevel[s10 + c] + currentLevel[s01 + c] + currentLevel[s11 + c];
                        nextLevel[dst + c] = (byte)(sum / 4);
                    }
                }
            }

            currentWidth = nextWidth;
            currentHeight = nextHeight;
            currentLevel = nextLevel;
            chain.Add(new TextureMipLevel(currentWidth, currentHeight, currentLevel).Validate());
        }

        return chain.ToArray();
    }

    private static void ValidateHeightArray(float[] height, int width, int heightPixels)
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
    }

    private static byte[] ToGrayscaleRgba(float[] samples)
    {
        var bytes = new byte[checked(samples.Length * 4)];
        for (int i = 0; i < samples.Length; i++)
        {
            byte value = (byte)Math.Clamp((int)MathF.Round(samples[i] * 255f), 0, 255);
            int offset = i * 4;
            bytes[offset] = value;
            bytes[offset + 1] = value;
            bytes[offset + 2] = value;
            bytes[offset + 3] = 255;
        }

        return bytes;
    }
}
