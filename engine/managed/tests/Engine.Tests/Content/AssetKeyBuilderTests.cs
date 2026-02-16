using System.IO;
using Engine.Content;

namespace Engine.Tests.Content;

public sealed class AssetKeyBuilderTests
{
    [Fact]
    public void Create_ShouldProduceStableKey_WhenInputsAreEqual()
    {
        var recipe = new TestRecipe("proc/texture", recipeVersion: 2, seed: 42, payload: "stone");

        AssetKey first = AssetKeyBuilder.Create(recipe, generatorVersion: 5, buildConfigHash: "CFG1");
        AssetKey second = AssetKeyBuilder.Create(recipe, generatorVersion: 5, buildConfigHash: "CFG1");

        Assert.Equal(first, second);
        Assert.Equal(first.RecipeHash, second.RecipeHash);
    }

    [Fact]
    public void Create_ShouldChangeRecipeHash_WhenRecipePayloadChanges()
    {
        var firstRecipe = new TestRecipe("proc/texture", recipeVersion: 2, seed: 42, payload: "stone");
        var secondRecipe = new TestRecipe("proc/texture", recipeVersion: 2, seed: 42, payload: "sand");

        AssetKey first = AssetKeyBuilder.Create(firstRecipe, generatorVersion: 5, buildConfigHash: "CFG1");
        AssetKey second = AssetKeyBuilder.Create(secondRecipe, generatorVersion: 5, buildConfigHash: "CFG1");

        Assert.NotEqual(first.RecipeHash, second.RecipeHash);
    }

    [Fact]
    public void ComputeBuildConfigHash_ShouldIgnoreDictionaryOrder()
    {
        IReadOnlyDictionary<string, string> first = new Dictionary<string, string>
        {
            ["runtime"] = "win-x64",
            ["configuration"] = "Release"
        };
        IReadOnlyDictionary<string, string> second = new Dictionary<string, string>
        {
            ["configuration"] = "Release",
            ["runtime"] = "win-x64"
        };

        string firstHash = AssetKeyBuilder.ComputeBuildConfigHash(first);
        string secondHash = AssetKeyBuilder.ComputeBuildConfigHash(second);

        Assert.Equal(firstHash, secondHash);
    }

    [Fact]
    public void AssetKeyConstructor_ShouldValidateArguments()
    {
        Assert.Throws<ArgumentException>(() => new AssetKey("", 1, 1, "RH", "BH"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AssetKey("gen", 0, 1, "RH", "BH"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AssetKey("gen", 1, 0, "RH", "BH"));
        Assert.Throws<ArgumentException>(() => new AssetKey("gen", 1, 1, "", "BH"));
        Assert.Throws<ArgumentException>(() => new AssetKey("gen", 1, 1, "RH", ""));
    }

    [Fact]
    public void ComputeBuildConfigHash_ShouldFail_WhenKeyIsEmpty()
    {
        IReadOnlyDictionary<string, string> config = new Dictionary<string, string>
        {
            [""] = "value"
        };

        Assert.Throws<InvalidDataException>(() => AssetKeyBuilder.ComputeBuildConfigHash(config));
    }

    private sealed class TestRecipe : IAssetRecipe
    {
        private readonly string _payload;

        public TestRecipe(string generatorId, int recipeVersion, ulong seed, string payload)
        {
            GeneratorId = generatorId;
            RecipeVersion = recipeVersion;
            Seed = seed;
            _payload = payload;
        }

        public string GeneratorId { get; }

        public int RecipeVersion { get; }

        public ulong Seed { get; }

        public void Write(BinaryWriter writer)
        {
            writer.Write(_payload);
        }
    }
}
