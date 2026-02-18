using System.Text.Json;

namespace Engine.NativeBindings;

public static class PackagedRuntimeNativeBootstrap
{
    public const string NativeLibraryPathEnvironmentVariable = "DFF_NATIVE_LIBRARY_PATH";
    public const string NativeLibrarySearchPathEnvironmentVariable = "DFF_NATIVE_LIBRARY_SEARCH_PATH";

    private const string NativeLibraryProperty = "nativeLibrary";
    private const string NativeLibrarySearchPathProperty = "nativeLibrarySearchPath";
    private const string OriginToken = "$ORIGIN";

    public static void ConfigureEnvironmentFromRuntimeConfig(string runtimeConfigPath, string? appBaseDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(runtimeConfigPath))
        {
            throw new ArgumentException("Runtime config path cannot be empty.", nameof(runtimeConfigPath));
        }

        string normalizedConfigPath = Path.GetFullPath(runtimeConfigPath);
        if (!File.Exists(normalizedConfigPath))
        {
            throw new FileNotFoundException(
                $"Runtime native config was not found: {normalizedConfigPath}",
                normalizedConfigPath);
        }

        RuntimeNativeConfig config = ReadConfig(normalizedConfigPath);
        string nativeLibraryPath = ResolveNativeLibraryPath(normalizedConfigPath, config.NativeLibrary);
        if (!File.Exists(nativeLibraryPath))
        {
            throw new FileNotFoundException(
                $"Runtime native library was not found: {nativeLibraryPath}",
                nativeLibraryPath);
        }

        Environment.SetEnvironmentVariable(NativeLibraryPathEnvironmentVariable, nativeLibraryPath);

        if (config.NativeLibrarySearchPath is null)
        {
            Environment.SetEnvironmentVariable(NativeLibrarySearchPathEnvironmentVariable, null);
            return;
        }

        string resolvedSearchPath = ResolveSearchPath(
            normalizedConfigPath,
            config.NativeLibrarySearchPath,
            appBaseDirectory);
        if (!Directory.Exists(resolvedSearchPath))
        {
            throw new DirectoryNotFoundException(
                $"Runtime native library search path was not found: {resolvedSearchPath}");
        }

        Environment.SetEnvironmentVariable(NativeLibrarySearchPathEnvironmentVariable, resolvedSearchPath);
    }

    public static void ApplyConfiguredSearchPathForCurrentPlatform()
    {
        string? loaderVariable = GetNativeLoaderSearchPathVariable();
        if (loaderVariable is null)
        {
            return;
        }

        bool ignoreCase = OperatingSystem.IsWindows();
        ApplyConfiguredSearchPath(loaderVariable, ignoreCase);
    }

    private static RuntimeNativeConfig ReadConfig(string runtimeConfigPath)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(File.ReadAllBytes(runtimeConfigPath));
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                $"Runtime native config '{runtimeConfigPath}' contains invalid JSON.",
                ex);
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException(
                    $"Runtime native config '{runtimeConfigPath}' must be a JSON object.");
            }

            string nativeLibrary = ReadRequiredString(root, runtimeConfigPath, NativeLibraryProperty);
            string? nativeLibrarySearchPath = ReadOptionalString(root, runtimeConfigPath, NativeLibrarySearchPathProperty);
            return new RuntimeNativeConfig(nativeLibrary, nativeLibrarySearchPath);
        }
    }

    private static string ResolveNativeLibraryPath(string runtimeConfigPath, string nativeLibrary)
    {
        if (Path.IsPathRooted(nativeLibrary))
        {
            return Path.GetFullPath(nativeLibrary);
        }

        string configDirectory = Path.GetDirectoryName(runtimeConfigPath)
            ?? throw new InvalidDataException($"Runtime native config path is invalid: {runtimeConfigPath}");
        string packageRoot = Path.GetFullPath(Path.Combine(configDirectory, ".."));
        return Path.GetFullPath(Path.Combine(packageRoot, NormalizeRelativePath(nativeLibrary)));
    }

    private static string ResolveSearchPath(
        string runtimeConfigPath,
        string configuredSearchPath,
        string? appBaseDirectory)
    {
        if (string.Equals(configuredSearchPath, OriginToken, StringComparison.OrdinalIgnoreCase))
        {
            string resolvedBase = string.IsNullOrWhiteSpace(appBaseDirectory)
                ? AppContext.BaseDirectory
                : appBaseDirectory;
            return Path.GetFullPath(resolvedBase);
        }

        if (Path.IsPathRooted(configuredSearchPath))
        {
            return Path.GetFullPath(configuredSearchPath);
        }

        string configDirectory = Path.GetDirectoryName(runtimeConfigPath)
            ?? throw new InvalidDataException($"Runtime native config path is invalid: {runtimeConfigPath}");
        string packageRoot = Path.GetFullPath(Path.Combine(configDirectory, ".."));
        return Path.GetFullPath(Path.Combine(packageRoot, NormalizeRelativePath(configuredSearchPath)));
    }

    internal static void ApplyConfiguredSearchPath(string loaderVariableName, bool pathComparisonIgnoreCase)
    {
        if (string.IsNullOrWhiteSpace(loaderVariableName))
        {
            throw new ArgumentException("Loader variable name cannot be empty.", nameof(loaderVariableName));
        }

        string? configuredSearchPath = Environment.GetEnvironmentVariable(NativeLibrarySearchPathEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(configuredSearchPath))
        {
            return;
        }

        string normalizedSearchPath = Path.GetFullPath(configuredSearchPath);
        if (!Directory.Exists(normalizedSearchPath))
        {
            throw new DirectoryNotFoundException(
                $"Runtime native library search path was not found: {normalizedSearchPath}");
        }

        string existingValue = Environment.GetEnvironmentVariable(loaderVariableName) ?? string.Empty;
        var comparer = pathComparisonIgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        List<string> entries = existingValue
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Path.GetFullPath)
            .Distinct(comparer)
            .ToList();
        if (entries.Any(path => comparer.Equals(path, normalizedSearchPath)))
        {
            return;
        }

        string updatedValue = string.IsNullOrWhiteSpace(existingValue)
            ? normalizedSearchPath
            : normalizedSearchPath + Path.PathSeparator + existingValue;
        Environment.SetEnvironmentVariable(loaderVariableName, updatedValue);
    }

    private static string? GetNativeLoaderSearchPathVariable()
    {
        if (OperatingSystem.IsWindows())
        {
            return "PATH";
        }

        if (OperatingSystem.IsLinux())
        {
            return "LD_LIBRARY_PATH";
        }

        if (OperatingSystem.IsMacOS())
        {
            return "DYLD_LIBRARY_PATH";
        }

        return null;
    }

    private static string NormalizeRelativePath(string path)
    {
        return path
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
    }

    private static string ReadRequiredString(JsonElement root, string runtimeConfigPath, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement property))
        {
            throw new InvalidDataException(
                $"Runtime native config '{runtimeConfigPath}' is missing required '{propertyName}' property.");
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException(
                $"Runtime native config '{runtimeConfigPath}' property '{propertyName}' must be a JSON string.");
        }

        string? value = property.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException(
                $"Runtime native config '{runtimeConfigPath}' property '{propertyName}' cannot be empty.");
        }

        return value.Trim();
    }

    private static string? ReadOptionalString(JsonElement root, string runtimeConfigPath, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException(
                $"Runtime native config '{runtimeConfigPath}' property '{propertyName}' must be a JSON string or null.");
        }

        string? value = property.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException(
                $"Runtime native config '{runtimeConfigPath}' property '{propertyName}' cannot be empty.");
        }

        return value.Trim();
    }

    private readonly record struct RuntimeNativeConfig(string NativeLibrary, string? NativeLibrarySearchPath);
}
