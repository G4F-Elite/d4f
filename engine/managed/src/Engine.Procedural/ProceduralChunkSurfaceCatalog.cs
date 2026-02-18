using System.Numerics;

namespace Engine.Procedural;

public static class ProceduralChunkSurfaceCatalog
{
    public static ProceduralTextureSurface BuildChunkSurface(
        LevelMeshChunk chunk,
        uint seed,
        int width = 128,
        int height = 128,
        bool enableDomainWarp = true)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Surface width must be greater than zero.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Surface height must be greater than zero.");
        }

        LevelChunkTag tag = LevelChunkTag.Parse(chunk.MeshTag);
        ProceduralTextureRecipe recipe = BuildRecipe(tag, chunk.NodeId, seed, width, height, enableDomainWarp);
        (float normalStrength, float roughnessContrast, int aoRadius, float aoStrength) =
            SelectDerivedMapSettings(tag);
        ProceduralTextureSurface baseSurface = TextureBuilder.GenerateSurfaceMaps(
            recipe,
            normalStrength,
            roughnessContrast,
            aoRadius,
            aoStrength);
        return ApplyPaletteAndRoughness(baseSurface, tag, seed, chunk.NodeId);
    }

    private static ProceduralTextureRecipe BuildRecipe(
        LevelChunkTag tag,
        int nodeId,
        uint seed,
        int width,
        int height,
        bool enableDomainWarp)
    {
        uint recipeSeed = seed
            ^ ((uint)nodeId * 0x9E3779B9u)
            ^ ((uint)tag.Variant * 0x85EBCA6Bu)
            ^ ((uint)tag.NodeType * 0xC2B2AE35u);
        (float domainWarpStrength, float domainWarpFrequency) = enableDomainWarp
            ? SelectDomainWarpSettings(tag, seed, nodeId)
            : (0f, 8f);

        return new ProceduralTextureRecipe(
            Kind: SelectTextureKind(tag),
            Width: width,
            Height: height,
            Seed: recipeSeed,
            FbmOctaves: SelectOctaves(tag, seed, nodeId),
            Frequency: SelectFrequency(tag, seed, nodeId),
            DomainWarpStrength: domainWarpStrength,
            DomainWarpFrequency: domainWarpFrequency);
    }

    private static ProceduralTextureKind SelectTextureKind(LevelChunkTag tag)
    {
        return tag.NodeType switch
        {
            LevelNodeType.Room => tag.Variant switch
            {
                0 => ProceduralTextureKind.Brick,
                1 => ProceduralTextureKind.Perlin,
                2 => ProceduralTextureKind.Worley,
                _ => ProceduralTextureKind.Simplex
            },
            LevelNodeType.Corridor => tag.Variant switch
            {
                0 => ProceduralTextureKind.Stripes,
                1 => ProceduralTextureKind.Grid,
                2 => ProceduralTextureKind.Simplex,
                _ => ProceduralTextureKind.Perlin
            },
            LevelNodeType.Junction => tag.Variant switch
            {
                0 => ProceduralTextureKind.Grid,
                1 => ProceduralTextureKind.Worley,
                2 => ProceduralTextureKind.Perlin,
                _ => ProceduralTextureKind.Brick
            },
            LevelNodeType.DeadEnd => tag.Variant switch
            {
                0 => ProceduralTextureKind.Worley,
                1 => ProceduralTextureKind.Perlin,
                2 => ProceduralTextureKind.Grid,
                _ => ProceduralTextureKind.Simplex
            },
            LevelNodeType.Shaft => tag.Variant switch
            {
                0 => ProceduralTextureKind.Stripes,
                1 => ProceduralTextureKind.Worley,
                2 => ProceduralTextureKind.Grid,
                _ => ProceduralTextureKind.Perlin
            },
            _ => throw new InvalidDataException($"Unsupported level node type '{tag.NodeType}'.")
        };
    }

    private static int SelectOctaves(LevelChunkTag tag, uint seed, int nodeId)
    {
        int baseOctaves = tag.NodeType switch
        {
            LevelNodeType.Room => 3,
            LevelNodeType.Corridor => 4,
            LevelNodeType.Junction => 4,
            LevelNodeType.DeadEnd => 5,
            LevelNodeType.Shaft => 5,
            _ => 4
        };

        float roll = Sample01(seed, nodeId, tag.Variant, 101u);
        int bonus = roll >= 0.7f ? 1 : 0;
        return Math.Clamp(baseOctaves + bonus, 2, 6);
    }

    private static float SelectFrequency(LevelChunkTag tag, uint seed, int nodeId)
    {
        float baseFrequency = tag.NodeType switch
        {
            LevelNodeType.Room => 3.2f,
            LevelNodeType.Corridor => 5.8f,
            LevelNodeType.Junction => 4.6f,
            LevelNodeType.DeadEnd => 6.2f,
            LevelNodeType.Shaft => 7.0f,
            _ => 4.0f
        };

        float variantBias = tag.Variant switch
        {
            0 => 0.85f,
            1 => 1.00f,
            2 => 1.15f,
            _ => 1.30f
        };
        float noiseScale = 0.85f + Sample01(seed, nodeId, tag.Variant, 103u) * 0.4f;
        return MathF.Max(1f, baseFrequency * variantBias * noiseScale);
    }

    private static (float Strength, float Frequency) SelectDomainWarpSettings(LevelChunkTag tag, uint seed, int nodeId)
    {
        float baseStrength = tag.NodeType switch
        {
            LevelNodeType.Room => 0.012f,
            LevelNodeType.Corridor => 0.020f,
            LevelNodeType.Junction => 0.018f,
            LevelNodeType.DeadEnd => 0.026f,
            LevelNodeType.Shaft => 0.028f,
            _ => 0.015f
        };
        float variantBoost = tag.Variant * 0.005f;
        float strengthJitter = Sample01(seed, nodeId, tag.Variant, 105u) * 0.012f;
        float strength = Math.Clamp(baseStrength + variantBoost + strengthJitter, 0f, 0.085f);

        float baseFrequency = tag.NodeType switch
        {
            LevelNodeType.Room => 8.0f,
            LevelNodeType.Corridor => 10.0f,
            LevelNodeType.Junction => 9.0f,
            LevelNodeType.DeadEnd => 11.0f,
            LevelNodeType.Shaft => 12.0f,
            _ => 8.0f
        };
        float frequencyScale = 0.90f + Sample01(seed, nodeId, tag.Variant, 107u) * 0.45f;
        float frequency = MathF.Max(1f, baseFrequency * frequencyScale);
        return (strength, frequency);
    }

    private static (float NormalStrength, float RoughnessContrast, int AoRadius, float AoStrength) SelectDerivedMapSettings(
        LevelChunkTag tag)
    {
        return tag.NodeType switch
        {
            LevelNodeType.Room => (1.1f, 1.8f, 2, 0.8f),
            LevelNodeType.Corridor => (1.3f, 2.4f, 2, 1.0f),
            LevelNodeType.Junction => (1.2f, 2.0f, 2, 0.9f),
            LevelNodeType.DeadEnd => (1.5f, 2.8f, 3, 1.1f),
            LevelNodeType.Shaft => (1.6f, 3.0f, 3, 1.15f),
            _ => throw new InvalidDataException($"Unsupported level node type '{tag.NodeType}'.")
        };
    }

    private static ProceduralTextureSurface ApplyPaletteAndRoughness(
        ProceduralTextureSurface surface,
        LevelChunkTag tag,
        uint seed,
        int nodeId)
    {
        surface = surface.Validate();
        (Vector3 dark, Vector3 light) = SelectPalette(tag, seed, nodeId);

        byte[] albedo = surface.AlbedoRgba8.ToArray();
        byte[] roughness = surface.RoughnessRgba8.ToArray();
        float roughnessBias = tag.NodeType switch
        {
            LevelNodeType.Room => 0.08f,
            LevelNodeType.Corridor => 0.14f,
            LevelNodeType.Junction => 0.11f,
            LevelNodeType.DeadEnd => 0.20f,
            LevelNodeType.Shaft => 0.24f,
            _ => 0.1f
        };
        float roughnessScale = 0.82f + (tag.Variant * 0.08f);

        for (int i = 0; i < surface.HeightMap.Length; i++)
        {
            int offset = i * 4;
            float heightSample = surface.HeightMap[i];
            float ao = surface.AmbientOcclusionRgba8[offset] / 255f;
            float baseSample = surface.AlbedoRgba8[offset] / 255f;
            float blend = Math.Clamp(heightSample * 0.65f + (1f - ao) * 0.25f + baseSample * 0.10f, 0f, 1f);
            Vector3 color = Vector3.Lerp(dark, light, blend);

            albedo[offset] = ToByte(color.X * 255f);
            albedo[offset + 1] = ToByte(color.Y * 255f);
            albedo[offset + 2] = ToByte(color.Z * 255f);
            albedo[offset + 3] = 255;

            float rough = roughness[offset] / 255f;
            float tunedRoughness = Math.Clamp((rough * roughnessScale) + roughnessBias, 0f, 1f);
            byte roughnessByte = ToByte(tunedRoughness * 255f);
            roughness[offset] = roughnessByte;
            roughness[offset + 1] = roughnessByte;
            roughness[offset + 2] = roughnessByte;
            roughness[offset + 3] = 255;
        }

        IReadOnlyList<TextureMipLevel> mipChain = TextureBuilder.GenerateMipChainRgba8(
            albedo,
            surface.Width,
            surface.Height);

        return new ProceduralTextureSurface(
            surface.Width,
            surface.Height,
            surface.HeightMap.ToArray(),
            albedo,
            surface.NormalRgba8.ToArray(),
            roughness,
            surface.MetallicRgba8.ToArray(),
            surface.AmbientOcclusionRgba8.ToArray(),
            mipChain).Validate();
    }

    private static (Vector3 Dark, Vector3 Light) SelectPalette(LevelChunkTag tag, uint seed, int nodeId)
    {
        float jitter = Sample01(seed, nodeId, tag.Variant, 109u);
        return tag.NodeType switch
        {
            LevelNodeType.Room => (
                new Vector3(0.28f, 0.23f, 0.19f) + new Vector3(jitter * 0.04f, jitter * 0.03f, 0f),
                new Vector3(0.70f, 0.63f, 0.54f) + new Vector3(jitter * 0.05f, jitter * 0.04f, jitter * 0.02f)),
            LevelNodeType.Corridor => (
                new Vector3(0.18f, 0.24f, 0.29f) + new Vector3(0f, jitter * 0.02f, jitter * 0.03f),
                new Vector3(0.52f, 0.63f, 0.68f) + new Vector3(jitter * 0.02f, jitter * 0.03f, jitter * 0.04f)),
            LevelNodeType.Junction => (
                new Vector3(0.24f, 0.24f, 0.25f) + new Vector3(jitter * 0.03f),
                new Vector3(0.63f, 0.62f, 0.61f) + new Vector3(jitter * 0.04f)),
            LevelNodeType.DeadEnd => (
                new Vector3(0.25f, 0.17f, 0.14f) + new Vector3(jitter * 0.03f, 0f, 0f),
                new Vector3(0.65f, 0.43f, 0.35f) + new Vector3(jitter * 0.04f, jitter * 0.02f, 0f)),
            LevelNodeType.Shaft => (
                new Vector3(0.14f, 0.20f, 0.17f) + new Vector3(0f, jitter * 0.03f, jitter * 0.01f),
                new Vector3(0.45f, 0.60f, 0.53f) + new Vector3(jitter * 0.02f, jitter * 0.04f, jitter * 0.02f)),
            _ => throw new InvalidDataException($"Unsupported level node type '{tag.NodeType}'.")
        };
    }

    private static float Sample01(uint seed, int nodeId, int variant, uint salt)
    {
        uint value = seed
            ^ ((uint)nodeId * 747796405u)
            ^ ((uint)variant * 2891336453u)
            ^ salt;
        value ^= value >> 16;
        value *= 2246822519u;
        value ^= value >> 13;
        value *= 3266489917u;
        value ^= value >> 16;
        return (value & 0x00FFFFFFu) / 16777215.0f;
    }

    private static byte ToByte(float value)
    {
        int scaled = (int)MathF.Round(value);
        if (scaled <= 0)
        {
            return 0;
        }

        return scaled >= 255 ? (byte)255 : (byte)scaled;
    }
}
