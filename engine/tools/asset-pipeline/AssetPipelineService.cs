using System.Text.Json;

namespace Engine.AssetPipeline;

public static class AssetPipelineService
{
    private const int PakVersion = 3;
    public const int SourceManifestVersion = 1;
    public const string CompiledManifestFileName = "compiled.manifest.bin";

    public static AssetManifest LoadManifest(string manifestPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);

        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException($"Manifest file was not found: {manifestPath}", manifestPath);
        }

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        JsonElement root = document.RootElement;
        int manifestVersion = ReadRequiredInt(root, "version", "root");
        if (manifestVersion != SourceManifestVersion)
        {
            throw new InvalidDataException(
                $"Unsupported manifest version {manifestVersion}. Expected {SourceManifestVersion}.");
        }

        if (!root.TryGetProperty("assets", out JsonElement assetsElement) || assetsElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("Manifest must contain an 'assets' array.");
        }

        var entries = new List<AssetManifestEntry>();
        int index = 0;
        foreach (JsonElement assetElement in assetsElement.EnumerateArray())
        {
            if (assetElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException($"Manifest asset at index {index} must be an object.");
            }

            string path = ReadRequiredString(assetElement, "path", $"assets[{index}]");
            string kind = ReadRequiredString(assetElement, "kind", $"assets[{index}]");
            string category = ReadOptionalCategory(assetElement, $"assets[{index}]");
            IReadOnlyList<string> tags = ReadOptionalTags(assetElement, $"assets[{index}]");

            entries.Add(new AssetManifestEntry(path, kind, category, tags));
            index++;
        }

        if (entries.Count == 0)
        {
            throw new InvalidDataException("Manifest must define at least one asset entry.");
        }

        return new AssetManifest(manifestVersion, entries);
    }

    public static void ValidateAssetsExist(AssetManifest manifest, string baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);

        foreach (AssetManifestEntry entry in manifest.Assets)
        {
            string resolvedPath = ResolveRelativePath(baseDirectory, entry.Path);
            if (!File.Exists(resolvedPath))
            {
                throw new FileNotFoundException($"Asset file was not found: {resolvedPath}", resolvedPath);
            }
        }
    }

    public static IReadOnlyList<PakEntry> CompileAssets(
        AssetManifest manifest,
        string baseDirectory,
        string outputRootDirectory)
    {
        return AssetCompiler.CompileAssets(manifest, baseDirectory, outputRootDirectory);
    }

    public static PakArchive WritePak(string outputPakPath, IReadOnlyList<PakEntry> entries)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPakPath);
        ArgumentNullException.ThrowIfNull(entries);

        if (entries.Count == 0)
        {
            throw new InvalidDataException("Pak must contain at least one entry.");
        }

        return PakBinaryCodec.WriteFromCompiledEntries(outputPakPath, entries);
    }

    public static PakArchive WritePak(string outputPakPath, AssetManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var compatibilityEntries = manifest.Assets
            .Select(static x =>
                new PakEntry(
                    x.Path,
                    x.Kind,
                    x.Path,
                    0,
                    0,
                    PakEntryKeyBuilder.Compute(x.Path, x.Kind, x.Path, 0),
                    x.Category,
                    x.Tags))
            .ToArray();

        return PakBinaryCodec.WriteMetadataOnly(outputPakPath, compatibilityEntries);
    }

    public static void WriteCompiledManifest(string outputPath, IReadOnlyList<PakEntry> entries)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(entries);

        CompiledManifestCodec.Write(outputPath, entries);
    }

    public static PakArchive ReadPak(string pakPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pakPath);

        PakArchive archive = PakBinaryCodec.Read(pakPath);
        if (archive.Version != PakVersion)
        {
            throw new InvalidDataException($"Unsupported pak version {archive.Version}. Expected {PakVersion}.");
        }

        if (archive.Entries is null || archive.Entries.Count == 0)
        {
            throw new InvalidDataException("Pak archive does not contain entries.");
        }

        foreach (PakEntry entry in archive.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Path))
            {
                throw new InvalidDataException("Pak entry path cannot be empty.");
            }

            if (string.IsNullOrWhiteSpace(entry.Kind))
            {
                throw new InvalidDataException($"Pak entry kind cannot be empty for asset '{entry.Path}'.");
            }

            if (string.IsNullOrWhiteSpace(entry.CompiledPath))
            {
                throw new InvalidDataException($"Pak entry compiled path cannot be empty for asset '{entry.Path}'.");
            }

            if (entry.SizeBytes < 0)
            {
                throw new InvalidDataException($"Pak entry size cannot be negative for asset '{entry.Path}'.");
            }

            if (entry.OffsetBytes < 0)
            {
                throw new InvalidDataException($"Pak entry offset cannot be negative for asset '{entry.Path}'.");
            }

            if (string.IsNullOrWhiteSpace(entry.AssetKey))
            {
                throw new InvalidDataException($"Pak entry asset key cannot be empty for asset '{entry.Path}'.");
            }
        }

        return archive;
    }

    public static string ResolveRelativePath(string baseDirectory, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(baseDirectory, path));
    }

    private static string ReadRequiredString(JsonElement element, string propertyName, string location)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property) || property.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException($"'{location}.{propertyName}' must be a non-empty string.");
        }

        string? value = property.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException($"'{location}.{propertyName}' cannot be empty.");
        }

        return value;
    }

    private static int ReadRequiredInt(JsonElement element, string propertyName, string location)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property) || property.ValueKind != JsonValueKind.Number)
        {
            throw new InvalidDataException($"'{location}.{propertyName}' must be a number.");
        }

        if (!property.TryGetInt32(out int value))
        {
            throw new InvalidDataException($"'{location}.{propertyName}' must be a valid integer.");
        }

        return value;
    }

    private static string ReadOptionalCategory(JsonElement element, string location)
    {
        if (!element.TryGetProperty("category", out JsonElement property))
        {
            return string.Empty;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException($"'{location}.category' must be a string.");
        }

        string? value = property.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException($"'{location}.category' cannot be empty.");
        }

        return value.Trim();
    }

    private static IReadOnlyList<string> ReadOptionalTags(JsonElement element, string location)
    {
        if (!element.TryGetProperty("tags", out JsonElement property))
        {
            return Array.Empty<string>();
        }

        if (property.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"'{location}.tags' must be an array of strings.");
        }

        var tags = new List<string>();
        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int index = 0;
        foreach (JsonElement tagElement in property.EnumerateArray())
        {
            if (tagElement.ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException($"'{location}.tags[{index}]' must be a string.");
            }

            string? rawValue = tagElement.GetString();
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                throw new InvalidDataException($"'{location}.tags[{index}]' cannot be empty.");
            }

            string tag = rawValue.Trim();
            if (unique.Add(tag))
            {
                tags.Add(tag);
            }

            index++;
        }

        return tags;
    }
}
