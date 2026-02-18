using System.IO;
using Engine.Content;

namespace Engine.Tests.Content;

public sealed class AssetsProviderTests
{
    [Fact]
    public void Assets_ShouldThrow_WhenProviderIsNotConfigured()
    {
        Assets.Reset();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => Assets.Load<object>("missing"));
        Assert.Contains("not configured", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Assets_ShouldDelegateCalls_ToConfiguredProvider()
    {
        var provider = new RecordingAssetsProvider();

        try
        {
            Assets.Configure(provider);
            var recipe = new TestRecipe("proc/string", recipeVersion: 1, seed: 11, payload: "x");

            string loaded = Assets.Load<string>("path/to/asset");
            string generated = Assets.GetOrCreate<string>(recipe);
            Assets.BakeAll();

            Assert.Equal("loaded:path/to/asset", loaded);
            Assert.Equal("generated:proc/string", generated);
            Assert.Equal(1, provider.BakeAllCallCount);
        }
        finally
        {
            Assets.Reset();
        }
    }

    [Fact]
    public void InMemoryProvider_Load_ShouldReturnRegisteredPathAsset()
    {
        var registry = new AssetRegistry();
        registry.Register(typeof(GameConfigAsset));
        var provider = new InMemoryAssetsProvider(registry);
        var asset = new GameConfigAsset("ok");

        provider.RegisterPathAsset("Game/Config/Main", asset);
        GameConfigAsset loaded = provider.Load<GameConfigAsset>("Game/Config/Main");

        Assert.Same(asset, loaded);
    }

    [Fact]
    public void InMemoryProvider_Load_ShouldFail_WhenRequestedTypeMismatches()
    {
        var registry = new AssetRegistry();
        registry.Register(typeof(GameConfigAsset));
        var provider = new InMemoryAssetsProvider(registry);
        provider.RegisterPathAsset("Game/Config/Main", new GameConfigAsset("ok"));

        Assert.Throws<InvalidCastException>(() => provider.Load<int>("Game/Config/Main"));
    }

    [Fact]
    public void InMemoryProvider_GetOrCreate_ShouldUseRuntimeCache()
    {
        var registry = new AssetRegistry();
        var provider = new InMemoryAssetsProvider(registry, buildConfigHash: "CFG");
        var generator = new CountingStringGenerator(generatorVersion: 3);
        provider.RegisterGenerator<TestRecipe, string>("proc/string", generator);
        var recipe = new TestRecipe("proc/string", recipeVersion: 2, seed: 10, payload: "hello");

        string first = provider.GetOrCreate<string>(recipe);
        string second = provider.GetOrCreate<string>(recipe);

        Assert.Equal("value:hello", first);
        Assert.Equal(first, second);
        Assert.Equal(1, generator.CallCount);
    }

    [Fact]
    public void InMemoryProvider_GetOrCreate_ShouldRespectRuntimeTypeBudget()
    {
        var provider = new InMemoryAssetsProvider(
            new AssetRegistry(),
            buildConfigHash: "CFG",
            runtimeTypeBudgets: new Dictionary<Type, int>
            {
                [typeof(string)] = 1
            });

        var generator = new CountingStringGenerator(generatorVersion: 1);
        provider.RegisterGenerator<TestRecipe, string>("proc/string", generator);

        var firstRecipe = new TestRecipe("proc/string", recipeVersion: 1, seed: 1, payload: "A");
        var secondRecipe = new TestRecipe("proc/string", recipeVersion: 1, seed: 2, payload: "B");

        _ = provider.GetOrCreate<string>(firstRecipe);
        _ = provider.GetOrCreate<string>(secondRecipe);
        _ = provider.GetOrCreate<string>(firstRecipe);

        Assert.Equal(3, generator.CallCount);
    }

    [Fact]
    public void InMemoryProvider_GetOrCreate_ShouldFail_WhenGeneratorMissing()
    {
        var provider = new InMemoryAssetsProvider(new AssetRegistry(), buildConfigHash: "CFG");
        var recipe = new TestRecipe("missing", recipeVersion: 1, seed: 1, payload: "x");

        Assert.Throws<KeyNotFoundException>(() => provider.GetOrCreate<string>(recipe));
    }

    [Fact]
    public void InMemoryProvider_GetOrCreate_ShouldReadByteAssetsFromDevDiskCache()
    {
        string cacheRoot = CreateTempDirectory();
        try
        {
            var diskCache = new DevDiskAssetCache(cacheRoot);
            var recipe = new TestRecipe("proc/bytes", recipeVersion: 1, seed: 7, payload: "payload");

            var firstGenerator = new CountingBytesGenerator(generatorVersion: 2);
            var firstProvider = new InMemoryAssetsProvider(
                new AssetRegistry(),
                buildConfigHash: "CFG",
                devDiskCache: diskCache);
            firstProvider.RegisterGenerator<TestRecipe, byte[]>("proc/bytes", firstGenerator);
            byte[] firstResult = firstProvider.GetOrCreate<byte[]>(recipe);

            var secondGenerator = new CountingBytesGenerator(generatorVersion: 2);
            var secondProvider = new InMemoryAssetsProvider(
                new AssetRegistry(),
                buildConfigHash: "CFG",
                devDiskCache: diskCache);
            secondProvider.RegisterGenerator<TestRecipe, byte[]>("proc/bytes", secondGenerator);
            byte[] secondResult = secondProvider.GetOrCreate<byte[]>(recipe);

            Assert.Equal(1, firstGenerator.CallCount);
            Assert.Equal(0, secondGenerator.CallCount);
            Assert.Equal(firstResult, secondResult);
        }
        finally
        {
            Directory.Delete(cacheRoot, recursive: true);
        }
    }

    [Fact]
    public void InMemoryProvider_BakeAll_ShouldGenerateEachUniqueAssetKeyOnce()
    {
        var provider = new InMemoryAssetsProvider(new AssetRegistry(), buildConfigHash: "CFG");
        var generator = new CountingStringGenerator(generatorVersion: 1);
        provider.RegisterGenerator<TestRecipe, string>("proc/string", generator);

        var first = new TestRecipe("proc/string", recipeVersion: 1, seed: 1, payload: "A");
        var duplicate = new TestRecipe("proc/string", recipeVersion: 1, seed: 1, payload: "A");
        var second = new TestRecipe("proc/string", recipeVersion: 1, seed: 2, payload: "B");

        provider.QueueBakeRecipe(first);
        provider.QueueBakeRecipe(duplicate);
        provider.QueueBakeRecipe(second);

        provider.BakeAll();

        Assert.Equal(2, generator.CallCount);
    }

    [DffAsset("Game/Config/Main", Category = "config", Tags = ["game"])]
    private sealed class GameConfigAsset
    {
        public GameConfigAsset(string value)
        {
            Value = value;
        }

        public string Value { get; }
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

        public string Payload => _payload;
    }

    private sealed class CountingStringGenerator : IAssetGenerator<TestRecipe, string>
    {
        public CountingStringGenerator(int generatorVersion)
        {
            GeneratorVersion = generatorVersion;
        }

        public int GeneratorVersion { get; }

        public int CallCount { get; private set; }

        public string Generate(TestRecipe recipe)
        {
            CallCount++;
            return $"value:{recipe.Payload}";
        }
    }

    private sealed class CountingBytesGenerator : IAssetGenerator<TestRecipe, byte[]>
    {
        public CountingBytesGenerator(int generatorVersion)
        {
            GeneratorVersion = generatorVersion;
        }

        public int GeneratorVersion { get; }

        public int CallCount { get; private set; }

        public byte[] Generate(TestRecipe recipe)
        {
            CallCount++;
            return System.Text.Encoding.UTF8.GetBytes($"bytes:{recipe.Payload}");
        }
    }

    private sealed class RecordingAssetsProvider : IAssetsProvider
    {
        public int BakeAllCallCount { get; private set; }

        public void BakeAll()
        {
            BakeAllCallCount++;
        }

        public T GetOrCreate<T>(IAssetRecipe recipe)
        {
            object value = $"generated:{recipe.GeneratorId}";
            return (T)value;
        }

        public T Load<T>(string path)
        {
            object value = $"loaded:{path}";
            return (T)value;
        }
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"engine-content-provider-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
