using System.Text;
using Engine.Scenes;
using Xunit;

namespace Engine.Tests.Scenes;

public sealed class SceneBinaryCodecTests
{
    [Fact]
    public void SceneRoundTrip_PreservesEntitiesAndComponents()
    {
        var scene = CreateScene();
        using var stream = new MemoryStream();

        SceneBinaryCodec.WriteScene(stream, scene);
        stream.Position = 0;

        var loaded = SceneBinaryCodec.ReadScene(stream);

        Assert.Equal(SceneFormat.SceneAssetVersion, loaded.Version);
        Assert.Equal(2, loaded.Entities.Count);
        Assert.Equal(2u, loaded.Entities[1].StableId);
        Assert.Equal("Camera", loaded.Entities[1].Name);

        Assert.Equal(2, loaded.Components.Count);
        Assert.Equal("Transform", loaded.Components[0].TypeId);
        Assert.Equal(new byte[] { 1, 2, 3 }, loaded.Components[0].Payload);
        Assert.Equal("Tag", loaded.Components[1].TypeId);
        Assert.Equal(Encoding.UTF8.GetBytes("Main"), loaded.Components[1].Payload);
    }

    [Fact]
    public void PrefabRoundTrip_PreservesEntitiesAndComponents()
    {
        var prefab = new PrefabAsset(
            [new SceneEntityDefinition(11, "PrefabRoot")],
            [new SceneComponentEntry(11, "Transform", [9, 8, 7])]);

        using var stream = new MemoryStream();
        SceneBinaryCodec.WritePrefab(stream, prefab);
        stream.Position = 0;

        var loaded = SceneBinaryCodec.ReadPrefab(stream);

        Assert.Equal(SceneFormat.PrefabAssetVersion, loaded.Version);
        Assert.Single(loaded.Entities);
        Assert.Single(loaded.Components);
        Assert.Equal(11u, loaded.Entities[0].StableId);
        Assert.Equal("Transform", loaded.Components[0].TypeId);
    }

    [Fact]
    public void ReadScene_RejectsVersionMismatch()
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(0x43465344u);
            writer.Write(SceneFormat.SceneAssetVersion + 1);
            writer.Write(0);
            writer.Write(0);
        }

        stream.Position = 0;

        var error = Assert.Throws<NotSupportedException>(() => SceneBinaryCodec.ReadScene(stream));
        Assert.Contains("version", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadScene_RejectsInvalidMagic()
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(0xDEADBEEFu);
            writer.Write(SceneFormat.SceneAssetVersion);
            writer.Write(0);
            writer.Write(0);
        }

        stream.Position = 0;

        var error = Assert.Throws<FormatException>(() => SceneBinaryCodec.ReadScene(stream));
        Assert.Contains("magic", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadScene_RejectsNegativeCounts()
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(0x43465344u);
            writer.Write(SceneFormat.SceneAssetVersion);
            writer.Write(-1);
        }

        stream.Position = 0;

        var error = Assert.Throws<FormatException>(() => SceneBinaryCodec.ReadScene(stream));
        Assert.Contains("count", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SceneAsset_RejectsUnknownComponentEntityReference()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => new SceneAsset(
                [new SceneEntityDefinition(1, "A")],
                [new SceneComponentEntry(2, "Transform", [1, 2, 3])]));

        Assert.Contains("unknown entity", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SceneAsset_RejectsDuplicateEntityStableIds()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => new SceneAsset(
                [new SceneEntityDefinition(1, "A"), new SceneEntityDefinition(1, "B")],
                []));

        Assert.Contains("duplicate", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ComponentPayload_IsDefensivelyCopied()
    {
        var bytes = new byte[] { 1, 2, 3, 4 };
        var component = new SceneComponentEntry(1, "Transform", bytes);

        bytes[0] = 99;

        Assert.Equal(new byte[] { 1, 2, 3, 4 }, component.Payload);
    }

    private static SceneAsset CreateScene()
    {
        return new SceneAsset(
        [
            new SceneEntityDefinition(1, "Player"),
            new SceneEntityDefinition(2, "Camera")
        ],
        [
            new SceneComponentEntry(1, "Transform", [1, 2, 3]),
            new SceneComponentEntry(2, "Tag", Encoding.UTF8.GetBytes("Main"))
        ]);
    }
}
