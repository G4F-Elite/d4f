namespace Engine.Content;

public sealed class InMemoryAssetsProvider : IAssetsProvider
{
    private readonly AssetRegistry _assetRegistry;
    private readonly RuntimeAssetCache<object> _runtimeCache;
    private readonly DevDiskAssetCache? _devDiskCache;
    private readonly Dictionary<string, object> _pathAssets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IUntypedAssetGenerator> _generators = new(StringComparer.Ordinal);
    private readonly List<IAssetRecipe> _bakeQueue = [];
    private readonly string _buildConfigHash;

    public InMemoryAssetsProvider(
        AssetRegistry assetRegistry,
        int runtimeCacheCapacity = 256,
        string? buildConfigHash = null,
        DevDiskAssetCache? devDiskCache = null)
    {
        _assetRegistry = assetRegistry ?? throw new ArgumentNullException(nameof(assetRegistry));
        _runtimeCache = new RuntimeAssetCache<object>(runtimeCacheCapacity);
        _devDiskCache = devDiskCache;
        _buildConfigHash = string.IsNullOrWhiteSpace(buildConfigHash)
            ? AssetKeyBuilder.ComputeBuildConfigHash(new Dictionary<string, string>
            {
                ["profile"] = "dev"
            })
            : buildConfigHash;
    }

    public void RegisterPathAsset<T>(string path, T asset)
        where T : notnull
    {
        AssetDescriptor descriptor = _assetRegistry.GetRequired(path);
        if (!descriptor.AssetType.IsAssignableFrom(asset.GetType()))
        {
            throw new InvalidDataException(
                $"Asset instance type '{asset.GetType().FullName}' is not assignable to '{descriptor.AssetType.FullName}'.");
        }

        _pathAssets[descriptor.Path] = asset;
    }

    public void RegisterGenerator<TRecipe, TOutput>(
        string generatorId,
        IAssetGenerator<TRecipe, TOutput> generator)
        where TRecipe : IAssetRecipe
        where TOutput : notnull
    {
        if (string.IsNullOrWhiteSpace(generatorId))
        {
            throw new ArgumentException("Generator id cannot be empty.", nameof(generatorId));
        }

        ArgumentNullException.ThrowIfNull(generator);
        if (generator.GeneratorVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(generator),
                "Generator version must be greater than zero.");
        }

        _generators[generatorId] = new UntypedAssetGenerator<TRecipe, TOutput>(generator);
    }

    public void QueueBakeRecipe(IAssetRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        _bakeQueue.Add(recipe);
    }

    public T Load<T>(string path)
    {
        AssetDescriptor descriptor = _assetRegistry.GetRequired(path);
        if (!_pathAssets.TryGetValue(descriptor.Path, out object? asset))
        {
            throw new KeyNotFoundException($"Asset '{descriptor.Path}' is not available in path storage.");
        }

        if (asset is not T typedAsset)
        {
            throw new InvalidCastException(
                $"Asset '{descriptor.Path}' is '{asset.GetType().FullName}', requested '{typeof(T).FullName}'.");
        }

        return typedAsset;
    }

    public T GetOrCreate<T>(IAssetRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);

        AssetKey key = ResolveKey(recipe, out IUntypedAssetGenerator generator);
        if (_runtimeCache.TryGet(key, out object cachedAsset))
        {
            if (cachedAsset is not T typedCachedAsset)
            {
                throw new InvalidCastException(
                    $"Cached asset for '{recipe.GeneratorId}' is '{cachedAsset.GetType().FullName}', requested '{typeof(T).FullName}'.");
            }

            return typedCachedAsset;
        }

        if (typeof(T) == typeof(byte[])
            && _devDiskCache is not null
            && _devDiskCache.TryLoad(key, out byte[] cachedBytes))
        {
            object boxedBytes = cachedBytes;
            _runtimeCache.Set(key, boxedBytes);
            return (T)boxedBytes;
        }

        object generatedAsset = generator.Generate(recipe);
        if (generatedAsset is not T typedGeneratedAsset)
        {
            throw new InvalidCastException(
                $"Generator '{recipe.GeneratorId}' produced '{generatedAsset.GetType().FullName}', requested '{typeof(T).FullName}'.");
        }

        _runtimeCache.Set(key, generatedAsset);
        if (generatedAsset is byte[] generatedBytes && _devDiskCache is not null)
        {
            _devDiskCache.Store(key, generatedBytes);
        }

        return typedGeneratedAsset;
    }

    public void BakeAll()
    {
        foreach (IAssetRecipe recipe in _bakeQueue)
        {
            AssetKey key = ResolveKey(recipe, out IUntypedAssetGenerator generator);
            if (_runtimeCache.TryGet(key, out _))
            {
                continue;
            }

            if (_devDiskCache is not null && _devDiskCache.TryLoad(key, out byte[] cachedBytes))
            {
                _runtimeCache.Set(key, cachedBytes);
                continue;
            }

            object generatedAsset = generator.Generate(recipe);
            _runtimeCache.Set(key, generatedAsset);

            if (generatedAsset is byte[] generatedBytes && _devDiskCache is not null)
            {
                _devDiskCache.Store(key, generatedBytes);
            }
        }
    }

    private AssetKey ResolveKey(IAssetRecipe recipe, out IUntypedAssetGenerator generator)
    {
        if (!_generators.TryGetValue(recipe.GeneratorId, out generator!))
        {
            throw new KeyNotFoundException($"Generator '{recipe.GeneratorId}' is not registered.");
        }

        if (!generator.RecipeType.IsAssignableFrom(recipe.GetType()))
        {
            throw new InvalidDataException(
                $"Recipe type '{recipe.GetType().FullName}' is incompatible with generator '{recipe.GeneratorId}'. Expected '{generator.RecipeType.FullName}'.");
        }

        return AssetKeyBuilder.Create(recipe, generator.GeneratorVersion, _buildConfigHash);
    }

    private interface IUntypedAssetGenerator
    {
        int GeneratorVersion { get; }

        Type RecipeType { get; }

        object Generate(IAssetRecipe recipe);
    }

    private sealed class UntypedAssetGenerator<TRecipe, TOutput> : IUntypedAssetGenerator
        where TRecipe : IAssetRecipe
        where TOutput : notnull
    {
        private readonly IAssetGenerator<TRecipe, TOutput> _generator;

        public UntypedAssetGenerator(IAssetGenerator<TRecipe, TOutput> generator)
        {
            _generator = generator;
        }

        public int GeneratorVersion => _generator.GeneratorVersion;

        public Type RecipeType => typeof(TRecipe);

        public object Generate(IAssetRecipe recipe)
        {
            if (recipe is not TRecipe typedRecipe)
            {
                throw new InvalidCastException(
                    $"Recipe type '{recipe.GetType().FullName}' is not assignable to '{typeof(TRecipe).FullName}'.");
            }

            TOutput result = _generator.Generate(typedRecipe);
            if (result is null)
            {
                throw new InvalidDataException(
                    $"Generator '{typedRecipe.GeneratorId}' returned null for output type '{typeof(TOutput).FullName}'.");
            }

            return result;
        }
    }
}
