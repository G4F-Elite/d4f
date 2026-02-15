using System.Text;

namespace Engine.Scenes;

public static class SceneBinaryCodec
{
    public static void WriteScene(Stream output, SceneAsset scene)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(scene);

        if (!output.CanWrite)
        {
            throw new ArgumentException("Output stream must be writable.", nameof(output));
        }

        using var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
        WriteHeader(writer, SceneFormat.SceneMagic, SceneFormat.SceneAssetVersion);
        WriteBody(writer, scene.Entities, scene.Components);
    }

    public static SceneAsset ReadScene(Stream input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!input.CanRead)
        {
            throw new ArgumentException("Input stream must be readable.", nameof(input));
        }

        using var reader = new BinaryReader(input, Encoding.UTF8, leaveOpen: true);
        var version = ReadHeader(reader, SceneFormat.SceneMagic, "scene");
        EnsureSupportedVersion(version, SceneFormat.SceneAssetVersion, "scene");
        var (entities, components) = ReadBody(reader);
        return new SceneAsset(entities, components);
    }

    public static void WritePrefab(Stream output, PrefabAsset prefab)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(prefab);

        if (!output.CanWrite)
        {
            throw new ArgumentException("Output stream must be writable.", nameof(output));
        }

        using var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
        WriteHeader(writer, SceneFormat.PrefabMagic, SceneFormat.PrefabAssetVersion);
        WriteBody(writer, prefab.Entities, prefab.Components);
    }

    public static PrefabAsset ReadPrefab(Stream input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!input.CanRead)
        {
            throw new ArgumentException("Input stream must be readable.", nameof(input));
        }

        using var reader = new BinaryReader(input, Encoding.UTF8, leaveOpen: true);
        var version = ReadHeader(reader, SceneFormat.PrefabMagic, "prefab");
        EnsureSupportedVersion(version, SceneFormat.PrefabAssetVersion, "prefab");
        var (entities, components) = ReadBody(reader);
        return new PrefabAsset(entities, components);
    }

    private static void WriteHeader(BinaryWriter writer, uint magic, uint version)
    {
        writer.Write(magic);
        writer.Write(version);
    }

    private static uint ReadHeader(BinaryReader reader, uint expectedMagic, string assetKind)
    {
        try
        {
            var magic = reader.ReadUInt32();
            if (magic != expectedMagic)
            {
                throw new FormatException(
                    $"Invalid {assetKind} asset magic 0x{magic:X8}. Expected 0x{expectedMagic:X8}.");
            }

            return reader.ReadUInt32();
        }
        catch (EndOfStreamException ex)
        {
            throw new FormatException($"Unexpected end of stream while reading {assetKind} header.", ex);
        }
    }

    private static void EnsureSupportedVersion(uint actualVersion, uint expectedVersion, string assetKind)
    {
        if (actualVersion != expectedVersion)
        {
            throw new NotSupportedException(
                $"Unsupported {assetKind} asset version {actualVersion}. Expected {expectedVersion}.");
        }
    }

    private static void WriteBody(
        BinaryWriter writer,
        IReadOnlyList<SceneEntityDefinition> entities,
        IReadOnlyList<SceneComponentEntry> components)
    {
        writer.Write(entities.Count);
        foreach (var entity in entities)
        {
            writer.Write(entity.StableId);
            writer.Write(entity.Name);
        }

        writer.Write(components.Count);
        foreach (var component in components)
        {
            writer.Write(component.EntityStableId);
            writer.Write(component.TypeId);
            writer.Write(component.Payload.Length);
            writer.Write(component.Payload);
        }
    }

    private static (List<SceneEntityDefinition> Entities, List<SceneComponentEntry> Components) ReadBody(BinaryReader reader)
    {
        try
        {
            var entityCount = reader.ReadInt32();
            EnsureNonNegativeCount(entityCount, "entity");

            var entities = new List<SceneEntityDefinition>(entityCount);
            for (var i = 0; i < entityCount; i++)
            {
                var stableId = reader.ReadUInt32();
                var name = reader.ReadString();
                entities.Add(new SceneEntityDefinition(stableId, name));
            }

            var componentCount = reader.ReadInt32();
            EnsureNonNegativeCount(componentCount, "component");

            var components = new List<SceneComponentEntry>(componentCount);
            for (var i = 0; i < componentCount; i++)
            {
                var entityStableId = reader.ReadUInt32();
                var typeId = reader.ReadString();
                var payloadLength = reader.ReadInt32();
                EnsureNonNegativeCount(payloadLength, "component payload");
                var payload = reader.ReadBytes(payloadLength);

                if (payload.Length != payloadLength)
                {
                    throw new FormatException(
                        $"Unexpected end of stream while reading component payload: expected {payloadLength} bytes, got {payload.Length}.");
                }

                components.Add(new SceneComponentEntry(entityStableId, typeId, payload));
            }

            return (entities, components);
        }
        catch (EndOfStreamException ex)
        {
            throw new FormatException("Unexpected end of stream while reading scene asset body.", ex);
        }
    }

    private static void EnsureNonNegativeCount(int count, string kind)
    {
        if (count < 0)
        {
            throw new FormatException($"Invalid {kind} count {count}.");
        }
    }
}
