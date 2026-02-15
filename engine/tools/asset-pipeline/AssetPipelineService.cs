using System.Text.Json;

namespace Engine.AssetPipeline;

public static class AssetPipelineService
{
    private const int PakVersion = 1;
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public static AssetManifest LoadManifest(string manifestPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);

        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException($"Manifest file was not found: {manifestPath}", manifestPath);
        }

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        JsonElement root = document.RootElement;

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

            entries.Add(new AssetManifestEntry(path, kind));
            index++;
        }

        if (entries.Count == 0)
        {
            throw new InvalidDataException("Manifest must define at least one asset entry.");
        }

        return new AssetManifest(entries);
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

    public static void WritePak(string outputPakPath, AssetManifest manifest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPakPath);
        ArgumentNullException.ThrowIfNull(manifest);

        string directory = Path.GetDirectoryName(outputPakPath) ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        PakArchive archive = new(
            Version: PakVersion,
            CreatedAtUtc: DateTime.UtcNow,
            Entries: manifest.Assets.Select(static x => new PakEntry(x.Path, x.Kind)).ToArray());

        File.WriteAllText(outputPakPath, JsonSerializer.Serialize(archive, SerializerOptions));
    }

    public static PakArchive ReadPak(string pakPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pakPath);

        if (!File.Exists(pakPath))
        {
            throw new FileNotFoundException($"Pak file was not found: {pakPath}", pakPath);
        }

        PakArchive? archive = JsonSerializer.Deserialize<PakArchive>(File.ReadAllText(pakPath), SerializerOptions);
        if (archive is null)
        {
            throw new InvalidDataException($"Pak file '{pakPath}' is empty or invalid.");
        }

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
}
