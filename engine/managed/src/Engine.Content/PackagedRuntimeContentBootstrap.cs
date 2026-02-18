using System.Text.Json;
using Engine.Core.Abstractions;

namespace Engine.Content;

public static class PackagedRuntimeContentBootstrap
{
    private const string ConfigDirectoryName = "config";
    private const string RuntimeConfigFileName = "runtime.json";
    private const string ContentModeProperty = "contentMode";
    private const string ContentPakProperty = "contentPak";
    private const string PakOnlyMode = "pak-only";

    public static string GetDefaultRuntimeConfigPath(string? appBaseDirectory = null)
    {
        string resolvedAppBaseDirectory = string.IsNullOrWhiteSpace(appBaseDirectory)
            ? AppContext.BaseDirectory
            : appBaseDirectory;
        string normalizedAppBaseDirectory = Path.GetFullPath(resolvedAppBaseDirectory);
        return Path.GetFullPath(Path.Combine(
            normalizedAppBaseDirectory,
            "..",
            ConfigDirectoryName,
            RuntimeConfigFileName));
    }

    public static MountedContentAssetsProvider ConfigureFromRuntimeConfig(
        IContentRuntimeFacade contentRuntime,
        string runtimeConfigPath)
    {
        ArgumentNullException.ThrowIfNull(contentRuntime);
        if (string.IsNullOrWhiteSpace(runtimeConfigPath))
        {
            throw new ArgumentException("Runtime config path cannot be empty.", nameof(runtimeConfigPath));
        }

        string normalizedConfigPath = Path.GetFullPath(runtimeConfigPath);
        if (!File.Exists(normalizedConfigPath))
        {
            throw new FileNotFoundException(
                $"Runtime content config was not found: {normalizedConfigPath}",
                normalizedConfigPath);
        }

        RuntimeContentConfig config = ReadConfig(normalizedConfigPath);
        if (!string.Equals(config.ContentMode, PakOnlyMode, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Unsupported runtime content mode '{config.ContentMode}' in '{normalizedConfigPath}'. " +
                "Expected 'pak-only'.");
        }

        string resolvedPakPath = ResolveContentPakPath(normalizedConfigPath, config.ContentPak);
        if (!File.Exists(resolvedPakPath))
        {
            throw new FileNotFoundException(
                $"Runtime content pak was not found: {resolvedPakPath}",
                resolvedPakPath);
        }

        var provider = new MountedContentAssetsProvider(contentRuntime, AssetsRuntimeMode.PakOnly);
        provider.MountPak(resolvedPakPath);
        Assets.ConfigurePakOnly(provider);
        return provider;
    }

    private static RuntimeContentConfig ReadConfig(string runtimeConfigPath)
    {
        JsonDocument document = ReadJsonDocument(runtimeConfigPath);
        using (document)
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException(
                    $"Runtime content config '{runtimeConfigPath}' must be a JSON object.");
            }

            string contentMode = ReadRequiredString(root, ContentModeProperty, runtimeConfigPath);
            string contentPak = ReadRequiredString(root, ContentPakProperty, runtimeConfigPath);
            return new RuntimeContentConfig(contentMode, contentPak);
        }
    }

    private static JsonDocument ReadJsonDocument(string runtimeConfigPath)
    {
        try
        {
            return JsonDocument.Parse(File.ReadAllBytes(runtimeConfigPath));
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                $"Runtime content config '{runtimeConfigPath}' contains invalid JSON.",
                ex);
        }
    }

    private static string ReadRequiredString(
        JsonElement root,
        string propertyName,
        string runtimeConfigPath)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement property))
        {
            throw new InvalidDataException(
                $"Runtime content config '{runtimeConfigPath}' is missing required '{propertyName}' property.");
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException(
                $"Runtime content config '{runtimeConfigPath}' property '{propertyName}' must be a JSON string.");
        }

        string? rawValue = property.GetString();
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            throw new InvalidDataException(
                $"Runtime content config '{runtimeConfigPath}' property '{propertyName}' cannot be empty.");
        }

        return rawValue.Trim();
    }

    private static string ResolveContentPakPath(string runtimeConfigPath, string configuredPakPath)
    {
        if (Path.IsPathRooted(configuredPakPath))
        {
            return Path.GetFullPath(configuredPakPath);
        }

        string configDirectory = Path.GetDirectoryName(runtimeConfigPath)
            ?? throw new InvalidDataException($"Runtime content config path is invalid: {runtimeConfigPath}");
        string packageRoot = Path.GetFullPath(Path.Combine(configDirectory, ".."));
        string normalizedRelativePakPath = configuredPakPath
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(packageRoot, normalizedRelativePakPath));
    }

    private readonly record struct RuntimeContentConfig(string ContentMode, string ContentPak);
}
