using System.IO;
using Engine.Content;

namespace Engine.Tests.Content;

public sealed class AssetsRuntimeModeTests
{
    [Fact]
    public void ConfigurePakOnly_ShouldSetPakOnlyRuntimeMode()
    {
        var provider = new RecordingProvider();

        try
        {
            Assets.ConfigurePakOnly(provider);
            Assert.Equal(AssetsRuntimeMode.PakOnly, Assets.GetRuntimeMode());
        }
        finally
        {
            Assets.Reset();
        }
    }

    [Fact]
    public void Load_ShouldWorkInPakOnlyMode()
    {
        var provider = new RecordingProvider();

        try
        {
            Assets.ConfigurePakOnly(provider);

            string loaded = Assets.Load<string>("content/asset.bin");

            Assert.Equal("loaded:content/asset.bin", loaded);
            Assert.Equal(1, provider.LoadCallCount);
        }
        finally
        {
            Assets.Reset();
        }
    }

    [Fact]
    public void GetOrCreate_ShouldThrowInPakOnlyMode_AndNotDelegateToProvider()
    {
        var provider = new RecordingProvider();

        try
        {
            Assets.ConfigurePakOnly(provider);
            var recipe = new DummyRecipe("proc/test", 1, 101u, "payload");

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => Assets.GetOrCreate<string>(recipe));
            Assert.Contains("pak only", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, provider.GetOrCreateCallCount);
        }
        finally
        {
            Assets.Reset();
        }
    }

    [Fact]
    public void BakeAll_ShouldThrowInPakOnlyMode_AndNotDelegateToProvider()
    {
        var provider = new RecordingProvider();

        try
        {
            Assets.ConfigurePakOnly(provider);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => Assets.BakeAll());
            Assert.Contains("pak only", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, provider.BakeAllCallCount);
        }
        finally
        {
            Assets.Reset();
        }
    }

    [Fact]
    public void Configure_ShouldValidateRuntimeMode()
    {
        var provider = new RecordingProvider();

        try
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                Assets.Configure(provider, (AssetsRuntimeMode)999));
        }
        finally
        {
            Assets.Reset();
        }
    }

    private sealed class RecordingProvider : IAssetsProvider
    {
        public int LoadCallCount { get; private set; }

        public int GetOrCreateCallCount { get; private set; }

        public int BakeAllCallCount { get; private set; }

        public T Load<T>(string path)
        {
            LoadCallCount++;
            object value = $"loaded:{path}";
            return (T)value;
        }

        public T GetOrCreate<T>(IAssetRecipe recipe)
        {
            GetOrCreateCallCount++;
            object value = $"generated:{recipe.GeneratorId}";
            return (T)value;
        }

        public void BakeAll()
        {
            BakeAllCallCount++;
        }
    }

    private sealed class DummyRecipe : IAssetRecipe
    {
        private readonly string _payload;

        public DummyRecipe(string generatorId, int recipeVersion, ulong seed, string payload)
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
