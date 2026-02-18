using Engine.Procedural;
using System.Numerics;

namespace Engine.Tests.Procedural;

public sealed class ProceduralTextureBuilderAdvancedTests
{
    [Fact]
    public void GenerateSurfaceMaps_ShouldProduceDeterministicRoughnessMetallicAoAndMipChain()
    {
        ProceduralTextureRecipe recipe = new(
            Kind: ProceduralTextureKind.Perlin,
            Width: 32,
            Height: 16,
            Seed: 777u,
            FbmOctaves: 4,
            Frequency: 6f);

        ProceduralTextureSurface first = TextureBuilder.GenerateSurfaceMaps(recipe);
        ProceduralTextureSurface second = TextureBuilder.GenerateSurfaceMaps(recipe);

        Assert.Equal(first.HeightMap, second.HeightMap);
        Assert.Equal(first.RoughnessRgba8, second.RoughnessRgba8);
        Assert.Equal(first.MetallicRgba8, second.MetallicRgba8);
        Assert.Equal(first.AmbientOcclusionRgba8, second.AmbientOcclusionRgba8);
        Assert.Equal(first.MipChain.Count, second.MipChain.Count);
        Assert.Equal(32 * 16 * 4, first.RoughnessRgba8.Length);
        Assert.Equal(32 * 16 * 4, first.MetallicRgba8.Length);
        Assert.Equal(32 * 16 * 4, first.AmbientOcclusionRgba8.Length);
        Assert.Equal(32, first.MipChain[0].Width);
        Assert.Equal(16, first.MipChain[0].Height);
        Assert.Equal(1, first.MipChain[^1].Width);
        Assert.Equal(1, first.MipChain[^1].Height);
    }

    [Fact]
    public void GenerateMipChainRgba8_ShouldBuildExpectedPyramid_ForNonPowerOfTwoTexture()
    {
        ProceduralTextureRecipe recipe = new(
            Kind: ProceduralTextureKind.Grid,
            Width: 10,
            Height: 6,
            Seed: 1u);
        byte[] baseLevel = TextureBuilder.GenerateRgba8(recipe);

        IReadOnlyList<TextureMipLevel> mipChain = TextureBuilder.GenerateMipChainRgba8(baseLevel, 10, 6);

        Assert.Equal(4, mipChain.Count);
        Assert.Equal((10, 6), (mipChain[0].Width, mipChain[0].Height));
        Assert.Equal((5, 3), (mipChain[1].Width, mipChain[1].Height));
        Assert.Equal((2, 1), (mipChain[2].Width, mipChain[2].Height));
        Assert.Equal((1, 1), (mipChain[3].Width, mipChain[3].Height));
        Assert.All(mipChain, static level => Assert.Equal(level.Width * level.Height * 4, level.Rgba8.Length));
    }

    [Fact]
    public void HeightToAmbientOcclusionMap_ShouldDarkenCavities()
    {
        const int width = 9;
        const int height = 9;
        var heightMap = new float[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                heightMap[y * width + x] = 0.9f;
            }
        }

        // Synthetic cavity in the center.
        heightMap[(height / 2) * width + (width / 2)] = 0.1f;

        byte[] ao = TextureBuilder.HeightToAmbientOcclusionMap(heightMap, width, height, radius: 2, strength: 2f);

        int centerOffset = ((height / 2) * width + (width / 2)) * 4;
        int cornerOffset = 0;
        Assert.True(ao[centerOffset] < ao[cornerOffset]);
    }

    [Fact]
    public void HeightToRoughnessMap_ShouldIncreaseRoughnessOnSteepSlope()
    {
        const int width = 8;
        const int height = 1;
        var heightMap = new float[width * height];
        for (int x = 0; x < width; x++)
        {
            heightMap[x] = x < 4 ? 0.1f : 0.9f;
        }

        byte[] roughness = TextureBuilder.HeightToRoughnessMap(heightMap, width, height, contrast: 5f, baseRoughness: 0.1f);

        int flatOffset = 1 * 4;
        int edgeOffset = 3 * 4;
        Assert.True(roughness[edgeOffset] > roughness[flatOffset]);
    }

    [Fact]
    public void HeightToMetallicMap_ShouldStayWithinUnitRange_AndReflectTextureKind()
    {
        const int width = 8;
        const int height = 1;
        var heightMap = new float[width * height];
        for (int x = 0; x < width; x++)
        {
            heightMap[x] = x / (float)(width - 1);
        }

        byte[] brick = TextureBuilder.HeightToMetallicMap(heightMap, width, height, ProceduralTextureKind.Brick);
        byte[] grid = TextureBuilder.HeightToMetallicMap(heightMap, width, height, ProceduralTextureKind.Grid);

        Assert.True(grid[0] > brick[0]);
        Assert.InRange(brick.Min(static x => x), 0, 255);
        Assert.InRange(grid.Max(static x => x), 0, 255);
    }

    [Fact]
    public void GenerateMipChainRgba8_ShouldFail_WhenBasePayloadSizeIsInvalid()
    {
        byte[] invalidBase = new byte[15];

        Assert.Throws<InvalidDataException>(() => TextureBuilder.GenerateMipChainRgba8(invalidBase, 2, 2));
    }

    [Fact]
    public void GenerateNormalMipChainRgba8_ShouldNormalizeDownsampledVectors()
    {
        byte[] baseNormals =
        [
            EncodeSigned(1f), EncodeSigned(0f), EncodeSigned(0f), 255,
            EncodeSigned(-1f), EncodeSigned(0f), EncodeSigned(0f), 255,
            EncodeSigned(0f), EncodeSigned(1f), EncodeSigned(0f), 255,
            EncodeSigned(0f), EncodeSigned(-1f), EncodeSigned(0f), 255
        ];

        IReadOnlyList<TextureMipLevel> mipChain = TextureBuilder.GenerateNormalMipChainRgba8(baseNormals, 2, 2);

        Assert.Equal(2, mipChain.Count);
        TextureMipLevel topMip = mipChain[1];
        Assert.Equal((1, 1), (topMip.Width, topMip.Height));
        Vector3 decoded = DecodeNormal(topMip.Rgba8, 0);
        Assert.InRange(decoded.X, -0.05f, 0.05f);
        Assert.InRange(decoded.Y, -0.05f, 0.05f);
        Assert.InRange(decoded.Z, 0.95f, 1.0f);
        Assert.Equal(255, topMip.Rgba8[3]);
    }

    [Fact]
    public void GenerateNormalMipChainRgba8_ShouldFail_WhenBasePayloadSizeIsInvalid()
    {
        byte[] invalidBase = new byte[15];

        Assert.Throws<InvalidDataException>(() => TextureBuilder.GenerateNormalMipChainRgba8(invalidBase, 2, 2));
    }

    private static byte EncodeSigned(float value)
    {
        int encoded = (int)MathF.Round((Math.Clamp(value, -1f, 1f) * 0.5f + 0.5f) * 255f);
        return (byte)Math.Clamp(encoded, 0, 255);
    }

    private static Vector3 DecodeNormal(byte[] rgba, int offset)
    {
        return new Vector3(
            (rgba[offset] / 255f) * 2f - 1f,
            (rgba[offset + 1] / 255f) * 2f - 1f,
            (rgba[offset + 2] / 255f) * 2f - 1f);
    }
}
