using System.Reflection;

namespace Engine.Content;

public sealed record AssetDescriptor(
    string Path,
    Type AssetType,
    string? Category,
    IReadOnlyList<string> Tags);

public sealed class AssetRegistry
{
    private readonly Dictionary<string, AssetDescriptor> _assetsByPath = new(StringComparer.Ordinal);

    public IReadOnlyCollection<AssetDescriptor> Entries => _assetsByPath.Values;

    public void Register(Type assetType)
    {
        ArgumentNullException.ThrowIfNull(assetType);

        DffAssetAttribute? attribute = assetType.GetCustomAttribute<DffAssetAttribute>();
        if (attribute is null)
        {
            throw new InvalidDataException($"Type '{assetType.FullName}' is missing [{nameof(DffAssetAttribute)}].");
        }

        Register(assetType, attribute);
    }

    public void RegisterAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        foreach (Type type in EnumerateAssemblyTypes(assembly))
        {
            DffAssetAttribute? attribute = type.GetCustomAttribute<DffAssetAttribute>();
            if (attribute is null)
            {
                continue;
            }

            Register(type, attribute);
        }
    }

    public void RegisterAssemblies(IEnumerable<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        foreach (Assembly assembly in assemblies)
        {
            if (assembly is null)
            {
                throw new ArgumentException("Assemblies collection cannot contain null entries.", nameof(assemblies));
            }

            RegisterAssembly(assembly);
        }
    }

    public void RegisterAssemblies(params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        RegisterAssemblies((IEnumerable<Assembly>)assemblies);
    }

    public bool TryGet(string path, out AssetDescriptor descriptor)
    {
        string normalizedPath = NormalizePath(path);
        return _assetsByPath.TryGetValue(normalizedPath, out descriptor!);
    }

    public AssetDescriptor GetRequired(string path)
    {
        if (!TryGet(path, out AssetDescriptor descriptor))
        {
            throw new KeyNotFoundException($"Asset '{path}' is not registered.");
        }

        return descriptor;
    }

    private void Register(Type assetType, DffAssetAttribute attribute)
    {
        string normalizedPath = NormalizePath(attribute.Path);
        string[] normalizedTags = NormalizeTags(attribute.Tags);
        string? normalizedCategory = string.IsNullOrWhiteSpace(attribute.Category)
            ? null
            : attribute.Category.Trim();

        var descriptor = new AssetDescriptor(
            normalizedPath,
            assetType,
            normalizedCategory,
            normalizedTags);

        if (_assetsByPath.TryGetValue(normalizedPath, out AssetDescriptor? existing) && existing is not null)
        {
            if (existing.AssetType == descriptor.AssetType
                && string.Equals(existing.Category, descriptor.Category, StringComparison.Ordinal)
                && existing.Tags.SequenceEqual(descriptor.Tags, StringComparer.Ordinal))
            {
                return;
            }

            throw new InvalidDataException(
                $"Asset path '{normalizedPath}' is already registered to type '{existing.AssetType.FullName}'.");
        }

        _assetsByPath.Add(normalizedPath, descriptor);
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Asset path cannot be empty.", nameof(path));
        }

        string normalized = path.Trim().Replace('\\', '/');
        string[] segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            throw new InvalidDataException($"Asset path '{path}' is invalid.");
        }

        foreach (string segment in segments)
        {
            if (segment == "." || segment == "..")
            {
                throw new InvalidDataException($"Asset path '{path}' cannot contain relative navigation segments.");
            }
        }

        return string.Join('/', segments);
    }

    private static string[] NormalizeTags(string[] tags)
    {
        ArgumentNullException.ThrowIfNull(tags);

        var normalized = new HashSet<string>(StringComparer.Ordinal);
        foreach (string rawTag in tags)
        {
            if (rawTag is null)
            {
                throw new InvalidDataException("Asset tags cannot contain null values.");
            }

            string trimmed = rawTag.Trim();
            if (trimmed.Length == 0)
            {
                throw new InvalidDataException("Asset tags cannot contain empty values.");
            }

            normalized.Add(trimmed);
        }

        return normalized.OrderBy(static x => x, StringComparer.Ordinal).ToArray();
    }

    private static IEnumerable<Type> EnumerateAssemblyTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            Type[] resolvedTypes = ex.Types
                .Where(static type => type is not null)
                .Cast<Type>()
                .ToArray();
            if (resolvedTypes.Length == 0)
            {
                throw new InvalidDataException(
                    $"Assembly '{assembly.FullName}' could not be scanned for asset types.",
                    ex);
            }

            return resolvedTypes;
        }
    }
}
