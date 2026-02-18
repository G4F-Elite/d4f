using Engine.Procedural;

namespace Engine.Tests.Procedural;

public sealed class ProceduralTextureBuilderDomainWarpTests
{
    [Fact]
    public void GenerateHeight_WithDomainWarp_IsDeterministic()
    {
        ProceduralTextureRecipe recipe = new(
            Kind: ProceduralTextureKind.Perlin,
            Width: 32,
            Height: 32,
            Seed: 91u,
            FbmOctaves: 4,
            Frequency: 5f,
            DomainWarpStrength: 0.2f,
            DomainWarpFrequency: 9f);

        float[] first = TextureBuilder.GenerateHeight(recipe);
        float[] second = TextureBuilder.GenerateHeight(recipe);

        Assert.Equal(first, second);
    }

    [Fact]
    public void GenerateHeight_WithDomainWarp_ChangesSignalComparedToPlainNoise()
    {
        ProceduralTextureRecipe plain = new(
            Kind: ProceduralTextureKind.Simplex,
            Width: 64,
            Height: 64,
            Seed: 11u,
            FbmOctaves: 4,
            Frequency: 6f,
            DomainWarpStrength: 0f,
            DomainWarpFrequency: 8f);
        ProceduralTextureRecipe warped = plain with
        {
            DomainWarpStrength = 0.25f,
            DomainWarpFrequency = 10f
        };

        float[] first = TextureBuilder.GenerateHeight(plain);
        float[] second = TextureBuilder.GenerateHeight(warped);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void GenerateHeight_WithZeroDomainWarpStrength_MatchesPlainNoise()
    {
        ProceduralTextureRecipe recipeA = new(
            Kind: ProceduralTextureKind.Worley,
            Width: 40,
            Height: 24,
            Seed: 5u,
            FbmOctaves: 3,
            Frequency: 4f,
            DomainWarpStrength: 0f,
            DomainWarpFrequency: 8f);
        ProceduralTextureRecipe recipeB = recipeA with
        {
            DomainWarpStrength = 0f,
            DomainWarpFrequency = 32f
        };

        float[] first = TextureBuilder.GenerateHeight(recipeA);
        float[] second = TextureBuilder.GenerateHeight(recipeB);

        Assert.Equal(first, second);
    }

    [Fact]
    public void RecipeValidation_RejectsInvalidDomainWarpParameters()
    {
        ProceduralTextureRecipe negativeStrength = new(
            Kind: ProceduralTextureKind.Perlin,
            Width: 8,
            Height: 8,
            Seed: 1u,
            DomainWarpStrength: -0.1f);
        Assert.Throws<ArgumentOutOfRangeException>(() => negativeStrength.Validate());

        ProceduralTextureRecipe zeroFrequency = new(
            Kind: ProceduralTextureKind.Perlin,
            Width: 8,
            Height: 8,
            Seed: 1u,
            DomainWarpStrength: 0.1f,
            DomainWarpFrequency: 0f);
        Assert.Throws<ArgumentOutOfRangeException>(() => zeroFrequency.Validate());

        ProceduralTextureRecipe nanFrequency = new(
            Kind: ProceduralTextureKind.Perlin,
            Width: 8,
            Height: 8,
            Seed: 1u,
            DomainWarpStrength: 0.1f,
            DomainWarpFrequency: float.NaN);
        Assert.Throws<ArgumentOutOfRangeException>(() => nanFrequency.Validate());
    }
}
