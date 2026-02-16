using System.Text.Json;
using Engine.Scenes;

namespace Engine.AssetPipeline;

internal static class AssetCompiler
{
    public static IReadOnlyList<PakEntry> CompileAssets(
        AssetManifest manifest,
        string baseDirectory,
        string outputRootDirectory)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputRootDirectory);

        Directory.CreateDirectory(outputRootDirectory);

        var entries = new List<PakEntry>(manifest.Assets.Count);
        foreach (AssetManifestEntry entry in manifest.Assets)
        {
            string sourcePath = AssetPipelineService.ResolveRelativePath(baseDirectory, entry.Path);
            string sourceRelativePath = NormalizeRelativeAssetPath(entry.Path);
            string compiledRelativePath = BuildCompiledRelativePath(entry.Kind, sourceRelativePath);
            string compiledFullPath = AssetPipelineService.ResolveRelativePath(outputRootDirectory, compiledRelativePath);
            string compiledDirectory = Path.GetDirectoryName(compiledFullPath) ?? outputRootDirectory;
            Directory.CreateDirectory(compiledDirectory);

            CompileAsset(entry.Kind, sourcePath, compiledFullPath);

            long sizeBytes = new FileInfo(compiledFullPath).Length;
            string assetKey = PakEntryKeyBuilder.Compute(entry.Path, entry.Kind, compiledRelativePath, sizeBytes);
            entries.Add(new PakEntry(entry.Path, entry.Kind, compiledRelativePath, sizeBytes, 0, assetKey));
        }

        return entries;
    }

    private static void CompileAsset(string kind, string sourcePath, string compiledFullPath)
    {
        switch (kind)
        {
            case "texture":
                CompiledAssetWriter.WriteTexture(sourcePath, compiledFullPath);
                return;
            case "mesh":
                CompiledAssetWriter.WriteMesh(sourcePath, compiledFullPath);
                return;
            case "scene":
                CompileScene(sourcePath, compiledFullPath);
                return;
            case "prefab":
                CompilePrefab(sourcePath, compiledFullPath);
                return;
            default:
                CompiledAssetWriter.WriteRaw(sourcePath, compiledFullPath);
                return;
        }
    }

    private static void CompileScene(string sourcePath, string compiledFullPath)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(sourcePath));
        SceneAsset scene = ParseSceneAsset(document.RootElement, sourcePath);
        string tempBinaryPath = Path.GetTempFileName();
        try
        {
            using (FileStream output = File.Create(tempBinaryPath))
            {
                SceneBinaryCodec.WriteScene(output, scene);
            }

            CompiledAssetWriter.WrapAsSceneBinary(tempBinaryPath, compiledFullPath);
        }
        finally
        {
            File.Delete(tempBinaryPath);
        }
    }

    private static void CompilePrefab(string sourcePath, string compiledFullPath)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(sourcePath));
        PrefabAsset prefab = ParsePrefabAsset(document.RootElement, sourcePath);
        string tempBinaryPath = Path.GetTempFileName();
        try
        {
            using (FileStream output = File.Create(tempBinaryPath))
            {
                SceneBinaryCodec.WritePrefab(output, prefab);
            }

            CompiledAssetWriter.WrapAsPrefabBinary(tempBinaryPath, compiledFullPath);
        }
        finally
        {
            File.Delete(tempBinaryPath);
        }
    }

    private static SceneAsset ParseSceneAsset(JsonElement root, string sourcePath)
    {
        var entities = ParseEntities(root, sourcePath);
        var components = ParseComponents(root, sourcePath);
        return new SceneAsset(entities, components);
    }

    private static PrefabAsset ParsePrefabAsset(JsonElement root, string sourcePath)
    {
        var entities = ParseEntities(root, sourcePath);
        var components = ParseComponents(root, sourcePath);
        return new PrefabAsset(entities, components);
    }

    private static IReadOnlyList<SceneEntityDefinition> ParseEntities(JsonElement root, string sourcePath)
    {
        if (!root.TryGetProperty("entities", out JsonElement entitiesElement) || entitiesElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"Asset '{sourcePath}' must contain an 'entities' array.");
        }

        var entities = new List<SceneEntityDefinition>();
        var index = 0;
        foreach (JsonElement entityElement in entitiesElement.EnumerateArray())
        {
            if (entityElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException($"Entity at index {index} in '{sourcePath}' must be an object.");
            }

            uint stableId = ReadRequiredUInt(entityElement, "stableId", sourcePath, $"entities[{index}]");
            string name = ReadRequiredString(entityElement, "name", sourcePath, $"entities[{index}]");
            entities.Add(new SceneEntityDefinition(stableId, name));
            index++;
        }

        return entities;
    }

    private static IReadOnlyList<SceneComponentEntry> ParseComponents(JsonElement root, string sourcePath)
    {
        if (!root.TryGetProperty("components", out JsonElement componentsElement) || componentsElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"Asset '{sourcePath}' must contain a 'components' array.");
        }

        var components = new List<SceneComponentEntry>();
        var index = 0;
        foreach (JsonElement componentElement in componentsElement.EnumerateArray())
        {
            if (componentElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException($"Component at index {index} in '{sourcePath}' must be an object.");
            }

            uint entityStableId = ReadRequiredUInt(componentElement, "entityStableId", sourcePath, $"components[{index}]");
            string typeId = ReadRequiredString(componentElement, "typeId", sourcePath, $"components[{index}]");
            string payloadBase64 = ReadRequiredString(componentElement, "payloadBase64", sourcePath, $"components[{index}]");

            byte[] payload;
            try
            {
                payload = Convert.FromBase64String(payloadBase64);
            }
            catch (FormatException ex)
            {
                throw new InvalidDataException(
                    $"'{sourcePath}:components[{index}].payloadBase64' is not valid base64.",
                    ex);
            }

            components.Add(new SceneComponentEntry(entityStableId, typeId, payload));
            index++;
        }

        return components;
    }

    private static string NormalizeRelativeAssetPath(string inputPath)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            throw new InvalidDataException("Asset path cannot be empty.");
        }

        string normalized = inputPath.Replace('\\', '/').Trim();
        if (Path.IsPathRooted(normalized))
        {
            normalized = Path.GetFileName(normalized);
        }

        string[] segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            throw new InvalidDataException($"Asset path '{inputPath}' is invalid.");
        }

        foreach (string segment in segments)
        {
            if (segment == "." || segment == "..")
            {
                throw new InvalidDataException($"Asset path '{inputPath}' cannot contain relative navigation segments.");
            }
        }

        return string.Join('/', segments);
    }

    private static string BuildCompiledRelativePath(string kind, string sourceRelativePath)
    {
        string safeKind = string.IsNullOrWhiteSpace(kind) ? "raw" : kind.Trim();
        return $"{safeKind}/{sourceRelativePath}.bin";
    }

    private static uint ReadRequiredUInt(JsonElement element, string propertyName, string sourcePath, string location)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property) || property.ValueKind != JsonValueKind.Number)
        {
            throw new InvalidDataException($"'{sourcePath}:{location}.{propertyName}' must be a number.");
        }

        if (!property.TryGetUInt32(out uint value) || value == 0)
        {
            throw new InvalidDataException($"'{sourcePath}:{location}.{propertyName}' must be a non-zero unsigned integer.");
        }

        return value;
    }

    private static string ReadRequiredString(JsonElement element, string propertyName, string sourcePath, string location)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property) || property.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException($"'{sourcePath}:{location}.{propertyName}' must be a string.");
        }

        string? value = property.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException($"'{sourcePath}:{location}.{propertyName}' cannot be empty.");
        }

        return value;
    }
}
