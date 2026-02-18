using Engine.Procedural;

namespace Engine.Tests.Procedural;

public sealed class ProceduralTextureBuilderAlbedoTests
{
    [Fact]
    public void HeightToAlbedoMap_ShouldBeDeterministic()
    {
        ProceduralTextureRecipe recipe = new(
            Kind: ProceduralTextureKind.Worley,
            Width: 24,
            Height: 12,
            Seed: 4242u,
            FbmOctaves: 3,
            Frequency: 5f);
        float[] height = TextureBuilder.GenerateHeight(recipe);
        byte[] ao = TextureBuilder.HeightToAmbientOcclusionMap(height, recipe.Width, recipe.Height);

        byte[] first = TextureBuilder.HeightToAlbedoMap(height, recipe.Width, recipe.Height, recipe.Kind, recipe.Seed, ao);
        byte[] second = TextureBuilder.HeightToAlbedoMap(height, recipe.Width, recipe.Height, recipe.Kind, recipe.Seed, ao);

        Assert.Equal(first, second);
        Assert.Equal(recipe.Width * recipe.Height * 4, first.Length);
        Assert.All(Enumerable.Range(0, recipe.Width * recipe.Height), idx => Assert.Equal(255, first[idx * 4 + 3]));
    }

    [Fact]
    public void HeightToAlbedoMap_ShouldProduceChromaticOutput()
    {
        ProceduralTextureRecipe recipe = new(
            Kind: ProceduralTextureKind.Brick,
            Width: 16,
            Height: 16,
            Seed: 91u,
            FbmOctaves: 2,
            Frequency: 6f);
        float[] height = TextureBuilder.GenerateHeight(recipe);
        byte[] albedo = TextureBuilder.HeightToAlbedoMap(height, recipe.Width, recipe.Height, recipe.Kind, recipe.Seed);

        bool hasChromaticPixel = false;
        for (int i = 0; i < albedo.Length; i += 4)
        {
            if (albedo[i] != albedo[i + 1] || albedo[i + 1] != albedo[i + 2])
            {
                hasChromaticPixel = true;
                break;
            }
        }

        Assert.True(hasChromaticPixel);
    }

    [Fact]
    public void HeightToAlbedoMap_ShouldDarkenWhenAoIsLower()
    {
        const int width = 4;
        const int height = 4;
        float[] flatHeight = Enumerable.Repeat(0.5f, width * height).ToArray();
        var ao = new byte[width * height * 4];
        for (int i = 0; i < ao.Length; i += 4)
        {
            ao[i] = 255;
            ao[i + 1] = 255;
            ao[i + 2] = 255;
            ao[i + 3] = 255;
        }

        int center = ((height / 2) * width + (width / 2)) * 4;
        ao[center] = 32;
        ao[center + 1] = 32;
        ao[center + 2] = 32;

        byte[] albedo = TextureBuilder.HeightToAlbedoMap(
            flatHeight,
            width,
            height,
            ProceduralTextureKind.Perlin,
            seed: 7u,
            ambientOcclusionRgba8: ao);

        float centerLuma = Luminance(albedo, center);
        float cornerLuma = Luminance(albedo, 0);
        Assert.True(centerLuma < cornerLuma);
    }

    [Fact]
    public void HeightToAlbedoMap_ShouldValidateAoPayloadSize()
    {
        float[] height = Enumerable.Repeat(0.3f, 9).ToArray();
        byte[] invalidAo = new byte[12];

        Assert.Throws<InvalidDataException>(() =>
            TextureBuilder.HeightToAlbedoMap(height, 3, 3, ProceduralTextureKind.Grid, seed: 1u, ambientOcclusionRgba8: invalidAo));
    }

    private static float Luminance(byte[] rgba, int offset)
    {
        return (rgba[offset] * 0.2126f + rgba[offset + 1] * 0.7152f + rgba[offset + 2] * 0.0722f) / 255f;
    }
}
