using System.IO;
using Engine.Content;
using Engine.Core.Abstractions;

namespace Engine.Tests.Content;

public sealed class MountedContentAssetsProviderTests
{
    [Fact]
    public void MountMethods_ShouldDelegateToContentRuntime()
    {
        var runtime = new FakeContentRuntimeFacade();
        var provider = new MountedContentAssetsProvider(runtime);

        provider.MountPak("D:/game/Game.pak");
        provider.MountDirectory("D:/game/dev-content");

        Assert.Equal("D:/game/Game.pak", runtime.LastMountedPakPath);
        Assert.Equal("D:/game/dev-content", runtime.LastMountedDirectoryPath);
    }

    [Fact]
    public void Load_ShouldReturnRawBytes_WhenRequestedTypeIsByteArray()
    {
        var runtime = new FakeContentRuntimeFacade();
        runtime.Files["assets/raw.bin"] = [1, 2, 3, 4];
        var provider = new MountedContentAssetsProvider(runtime);

        byte[] loaded = provider.Load<byte[]>("assets/raw.bin");

        Assert.Equal("assets/raw.bin", runtime.LastReadPath);
        Assert.Equal([1, 2, 3, 4], loaded);
    }

    [Fact]
    public void Load_ShouldDecodeTextureBlobPayload()
    {
        var runtime = new FakeContentRuntimeFacade();
        byte[] blob = TextureBlobCodec.Write(new TextureBlobData(
            TextureBlobFormat.Rgba8Unorm,
            TextureBlobColorSpace.Srgb,
            Width: 2,
            Height: 2,
            MipChain:
            [
                new TextureBlobMip(
                    Width: 2,
                    Height: 2,
                    RowPitchBytes: 8,
                    Data: [1, 2, 3, 4, 11, 12, 13, 14, 21, 22, 23, 24, 31, 32, 33, 34])
            ]));
        runtime.Files["textures/wall.bin"] = blob;
        var provider = new MountedContentAssetsProvider(runtime);

        TextureBlobData texture = provider.Load<TextureBlobData>("textures/wall.bin");

        Assert.Equal(2, texture.Width);
        Assert.Equal(2, texture.Height);
        Assert.Equal(TextureBlobColorSpace.Srgb, texture.ColorSpace);
        Assert.Equal(blob, TextureBlobCodec.Write(texture));
    }

    [Fact]
    public void Load_ShouldRejectUnsupportedTargetType()
    {
        var runtime = new FakeContentRuntimeFacade();
        runtime.Files["assets/raw.bin"] = [1, 2];
        var provider = new MountedContentAssetsProvider(runtime);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => provider.Load<Guid>("assets/raw.bin"));
        Assert.Contains("not supported", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetOrCreate_ShouldThrowInMountedMode()
    {
        var provider = new MountedContentAssetsProvider(new FakeContentRuntimeFacade());
        var recipe = new DummyRecipe();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => provider.GetOrCreate<byte[]>(recipe));
        Assert.Contains("does not support runtime recipe generation", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BakeAll_ShouldThrowInMountedMode()
    {
        var provider = new MountedContentAssetsProvider(new FakeContentRuntimeFacade());

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => provider.BakeAll());
        Assert.Contains("does not support BakeAll", exception.Message, StringComparison.Ordinal);
    }

    private sealed class DummyRecipe : IAssetRecipe
    {
        public string GeneratorId => "dummy";

        public int RecipeVersion => 1;

        public ulong Seed => 1u;

        public void Write(BinaryWriter writer)
        {
            writer.Write(42);
        }
    }

    private sealed class FakeContentRuntimeFacade : IContentRuntimeFacade
    {
        public Dictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);

        public string? LastMountedPakPath { get; private set; }

        public string? LastMountedDirectoryPath { get; private set; }

        public string? LastReadPath { get; private set; }

        public void MountPak(string pakPath)
        {
            LastMountedPakPath = pakPath;
        }

        public void MountDirectory(string directoryPath)
        {
            LastMountedDirectoryPath = directoryPath;
        }

        public byte[] ReadFile(string assetPath)
        {
            LastReadPath = assetPath;
            if (!Files.TryGetValue(assetPath, out byte[]? payload))
            {
                throw new FileNotFoundException($"Asset '{assetPath}' is missing.");
            }

            return payload.ToArray();
        }
    }
}
