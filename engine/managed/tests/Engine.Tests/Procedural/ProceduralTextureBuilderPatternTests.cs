using Engine.Procedural;

namespace Engine.Tests.Procedural;

public sealed class ProceduralTextureBuilderPatternTests
{
    [Fact]
    public void PatternTextures_AreDeterministicForSameRecipe()
    {
        foreach (ProceduralTextureKind kind in PatternKinds)
        {
            ProceduralTextureRecipe recipe = new(
                Kind: kind,
                Width: 32,
                Height: 24,
                Seed: 42u,
                FbmOctaves: 4,
                Frequency: 5f);

            float[] first = TextureBuilder.GenerateHeight(recipe);
            float[] second = TextureBuilder.GenerateHeight(recipe);
            Assert.Equal(first, second);
        }
    }

    [Fact]
    public void PatternTextures_ChangeWhenSeedChanges()
    {
        foreach (ProceduralTextureKind kind in PatternKinds)
        {
            ProceduralTextureRecipe recipe = new(
                Kind: kind,
                Width: 32,
                Height: 24,
                Seed: 100u,
                FbmOctaves: 4,
                Frequency: 4f);

            float[] first = TextureBuilder.GenerateHeight(recipe);
            float[] second = TextureBuilder.GenerateHeight(recipe with { Seed = 101u });

            Assert.NotEqual(first, second);
        }
    }

    [Fact]
    public void PatternTextures_ChangeWhenFrequencyChanges()
    {
        foreach (ProceduralTextureKind kind in PatternKinds)
        {
            ProceduralTextureRecipe recipe = new(
                Kind: kind,
                Width: 40,
                Height: 20,
                Seed: 7u,
                FbmOctaves: 4,
                Frequency: 2f);

            float[] first = TextureBuilder.GenerateHeight(recipe);
            float[] second = TextureBuilder.GenerateHeight(recipe with { Frequency = 8f });

            Assert.NotEqual(first, second);
        }
    }

    private static readonly ProceduralTextureKind[] PatternKinds =
    [
        ProceduralTextureKind.Grid,
        ProceduralTextureKind.Brick,
        ProceduralTextureKind.Stripes
    ];
}
