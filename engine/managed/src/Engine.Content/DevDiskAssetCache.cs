namespace Engine.Content;

public sealed class DevDiskAssetCache
{
    private readonly string _rootDirectory;

    public DevDiskAssetCache(string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            throw new ArgumentException("Cache root directory cannot be empty.", nameof(rootDirectory));
        }

        _rootDirectory = Path.GetFullPath(rootDirectory);
        Directory.CreateDirectory(_rootDirectory);
    }

    public string RootDirectory => _rootDirectory;

    public void Store(AssetKey key, ReadOnlySpan<byte> payload)
    {
        string path = ResolveEntryPath(key);
        string directory = Path.GetDirectoryName(path)
            ?? throw new InvalidDataException($"Resolved cache path '{path}' is invalid.");
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(path, payload.ToArray());
    }

    public bool TryLoad(AssetKey key, out byte[] payload)
    {
        string path = ResolveEntryPath(key);
        if (!File.Exists(path))
        {
            payload = [];
            return false;
        }

        payload = File.ReadAllBytes(path);
        return true;
    }

    public bool Remove(AssetKey key)
    {
        string path = ResolveEntryPath(key);
        if (!File.Exists(path))
        {
            return false;
        }

        File.Delete(path);
        return true;
    }

    public string ResolveEntryPath(AssetKey key)
    {
        string safeGeneratorId = ToSafeFileToken(key.GeneratorId);
        string safeBuildHash = ToSafeFileToken(key.BuildConfigHash);
        string safeRecipeHash = ToSafeFileToken(key.RecipeHash);
        return Path.Combine(
            _rootDirectory,
            safeGeneratorId,
            $"g{key.GeneratorVersion}",
            $"r{key.RecipeVersion}",
            safeBuildHash,
            $"{safeRecipeHash}.bin");
    }

    private static string ToSafeFileToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException("Cache key token cannot be empty.");
        }

        char[] invalidChars = Path.GetInvalidFileNameChars();
        var buffer = new char[value.Length];
        for (int i = 0; i < value.Length; i++)
        {
            char current = value[i];
            bool isInvalid = invalidChars.Contains(current);
            buffer[i] = isInvalid ? '_' : current;
        }

        string safe = new(buffer);
        if (safe.Length == 0)
        {
            throw new InvalidDataException("Cache key token became empty after sanitization.");
        }

        return safe;
    }
}
