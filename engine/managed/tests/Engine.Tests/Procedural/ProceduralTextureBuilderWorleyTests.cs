using Engine.Procedural;

namespace Engine.Tests.Procedural;

public sealed class ProceduralTextureBuilderWorleyTests
{
    [Fact]
    public void GenerateHeight_WorleyFbmIsDeterministicAndInRange()
    {
        ProceduralTextureRecipe recipe = new(
            Kind: ProceduralTextureKind.Worley,
            Width: 40,
            Height: 28,
            Seed: 991u,
            FbmOctaves: 4,
            Frequency: 5f);

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
    public void GenerateHeight_WorleyFbmChangesWithOctaves()
    {
        ProceduralTextureRecipe singleOctave = new(
            Kind: ProceduralTextureKind.Worley,
            Width: 32,
            Height: 32,
            Seed: 123u,
            FbmOctaves: 1,
            Frequency: 6f);
        ProceduralTextureRecipe fourOctaves = singleOctave with { FbmOctaves = 4 };

        float[] first = TextureBuilder.GenerateHeight(singleOctave);
        float[] second = TextureBuilder.GenerateHeight(fourOctaves);

        double absoluteDiff = 0.0;
        for (int i = 0; i < first.Length; i++)
        {
            absoluteDiff += Math.Abs(first[i] - second[i]);
        }

        Assert.True(absoluteDiff > 0.01, "Worley FBM should change when the octave count changes.");
    }

    [Fact]
    public void GenerateHeight_WorleyFbmChangesWithSeed()
    {
        ProceduralTextureRecipe recipe = new(
            Kind: ProceduralTextureKind.Worley,
            Width: 24,
            Height: 24,
            Seed: 7u,
            FbmOctaves: 3,
            Frequency: 4f);

        float[] first = TextureBuilder.GenerateHeight(recipe);
        float[] second = TextureBuilder.GenerateHeight(recipe with { Seed = 8u });

        Assert.NotEqual(first, second);
    }
}
