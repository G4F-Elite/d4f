using Engine.Procedural;

namespace Engine.Tests.Procedural;

public sealed class ProceduralTextureBuilderSimplexTests
{
    [Fact]
    public void GenerateHeight_SimplexIsDeterministicAndInRange()
    {
        ProceduralTextureRecipe recipe = new(
            Kind: ProceduralTextureKind.Simplex,
            Width: 48,
            Height: 32,
            Seed: 1337u,
            FbmOctaves: 5,
            Frequency: 7f);

        float[] first = TextureBuilder.GenerateHeight(recipe);
        float[] second = TextureBuilder.GenerateHeight(recipe);

        Assert.Equal(first, second);
        Assert.All(first, static sample =>
        {
            Assert.True(float.IsFinite(sample));
            Assert.InRange(sample, 0f, 1f);
        });
    }

    [Fact]
    public void GenerateHeight_SimplexChangesWithSeed()
    {
        ProceduralTextureRecipe a = new(
            Kind: ProceduralTextureKind.Simplex,
            Width: 32,
            Height: 32,
            Seed: 1u,
            FbmOctaves: 4,
            Frequency: 6f);
        ProceduralTextureRecipe b = a with { Seed = 2u };

        float[] first = TextureBuilder.GenerateHeight(a);
        float[] second = TextureBuilder.GenerateHeight(b);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void GenerateHeight_SimplexDiffersFromPerlinForSameRecipe()
    {
        ProceduralTextureRecipe perlinRecipe = new(
            Kind: ProceduralTextureKind.Perlin,
            Width: 24,
            Height: 24,
            Seed: 55u,
            FbmOctaves: 4,
            Frequency: 5f);
        ProceduralTextureRecipe simplexRecipe = perlinRecipe with { Kind = ProceduralTextureKind.Simplex };

        float[] perlin = TextureBuilder.GenerateHeight(perlinRecipe);
        float[] simplex = TextureBuilder.GenerateHeight(simplexRecipe);

        Assert.NotEqual(perlin, simplex);
    }
}
