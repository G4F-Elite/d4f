namespace Engine.Content;

public sealed class InMemoryAssetsProvider : IAssetsProvider
{
    private readonly AssetRegistry _assetRegistry;
    private readonly RuntimeAssetCache<object> _runtimeCache;
    private readonly DevDiskAssetCache? _devDiskCache;
    private readonly Dictionary<Type, int> _runtimeTypeBudgets;
    private readonly Dictionary<Type, LinkedList<AssetKey>> _runtimeTypeLru = new();
    private readonly Dictionary<AssetKey, LinkedListNode<AssetKey>> _typeLruNodesByKey = new();
    private readonly Dictionary<AssetKey, Type> _runtimeKeyTypes = new();
    private readonly Dictionary<string, object> _pathAssets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IUntypedAssetGenerator> _generators = new(StringComparer.Ordinal);
    private readonly List<IAssetRecipe> _bakeQueue = [];
    private readonly string _buildConfigHash;

    public InMemoryAssetsProvider(
        IEnumerable<System.Reflection.Assembly> assetAssemblies,
        int runtimeCacheCapacity = 256,
        string? buildConfigHash = null,
        DevDiskAssetCache? devDiskCache = null,
        IReadOnlyDictionary<Type, int>? runtimeTypeBudgets = null)
        : this(
            AssetRegistryDiscovery.BuildFromAssemblies(assetAssemblies),
            runtimeCacheCapacity,
            buildConfigHash,
            devDiskCache,
            runtimeTypeBudgets)
    {
    }

    public InMemoryAssetsProvider(
        AssetRegistry assetRegistry,
        int runtimeCacheCapacity = 256,
        string? buildConfigHash = null,
        DevDiskAssetCache? devDiskCache = null,
        IReadOnlyDictionary<Type, int>? runtimeTypeBudgets = null)
    {
        _assetRegistry = assetRegistry ?? throw new ArgumentNullException(nameof(assetRegistry));
        _runtimeCache = new RuntimeAssetCache<object>(runtimeCacheCapacity);
        _devDiskCache = devDiskCache;
        _runtimeTypeBudgets = NormalizeTypeBudgets(runtimeTypeBudgets);
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
            TouchTrackedKey(key);
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
            StoreRuntimeAsset(key, boxedBytes);
            return (T)boxedBytes;
        }

        object generatedAsset = generator.Generate(recipe);
        if (generatedAsset is not T typedGeneratedAsset)
        {
            throw new InvalidCastException(
                $"Generator '{recipe.GeneratorId}' produced '{generatedAsset.GetType().FullName}', requested '{typeof(T).FullName}'.");
        }

        StoreRuntimeAsset(key, generatedAsset);
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
                StoreRuntimeAsset(key, cachedBytes);
                continue;
            }

            object generatedAsset = generator.Generate(recipe);
            StoreRuntimeAsset(key, generatedAsset);

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

    private static Dictionary<Type, int> NormalizeTypeBudgets(IReadOnlyDictionary<Type, int>? runtimeTypeBudgets)
    {
        if (runtimeTypeBudgets is null || runtimeTypeBudgets.Count == 0)
        {
            return new Dictionary<Type, int>();
        }

        var normalized = new Dictionary<Type, int>();
        foreach ((Type type, int budget) in runtimeTypeBudgets)
        {
            if (type is null)
            {
                throw new InvalidDataException("Runtime type budget map cannot contain null keys.");
            }

            if (budget <= 0)
            {
                throw new InvalidDataException(
                    $"Runtime type budget for '{type.FullName}' must be greater than zero.");
            }

            normalized[type] = budget;
        }

        return normalized;
    }

    private void StoreRuntimeAsset(AssetKey key, object asset)
    {
        if (_runtimeCache.Set(key, asset, out AssetKey evictedKey, out _))
        {
            RemoveTrackedKey(evictedKey);
        }

        TrackRuntimeAsset(key, asset.GetType());
        EnforceTypeBudget(asset.GetType());
    }

    private void EnforceTypeBudget(Type assetType)
    {
        if (!_runtimeTypeBudgets.TryGetValue(assetType, out int budget))
        {
            return;
        }

        if (!_runtimeTypeLru.TryGetValue(assetType, out LinkedList<AssetKey>? list))
        {
            return;
        }

        while (list.Count > budget)
        {
            LinkedListNode<AssetKey>? tail = list.Last;
            if (tail is null)
            {
                return;
            }

            AssetKey evictedKey = tail.Value;
            _runtimeCache.Remove(evictedKey);
            RemoveTrackedKey(evictedKey);
        }
    }

    private void TouchTrackedKey(AssetKey key)
    {
        if (!_typeLruNodesByKey.TryGetValue(key, out LinkedListNode<AssetKey>? node))
        {
            return;
        }

        if (!_runtimeKeyTypes.TryGetValue(key, out Type? assetType))
        {
            return;
        }

        if (!_runtimeTypeLru.TryGetValue(assetType, out LinkedList<AssetKey>? list))
        {
            return;
        }

        list.Remove(node);
        list.AddFirst(node);
    }

    private void TrackRuntimeAsset(AssetKey key, Type assetType)
    {
        if (_runtimeKeyTypes.TryGetValue(key, out Type? existingType))
        {
            if (existingType == assetType)
            {
                TouchTrackedKey(key);
                return;
            }

            RemoveTrackedKey(key);
        }

        if (!_runtimeTypeLru.TryGetValue(assetType, out LinkedList<AssetKey>? list))
        {
            list = new LinkedList<AssetKey>();
            _runtimeTypeLru.Add(assetType, list);
        }

        LinkedListNode<AssetKey> node = list.AddFirst(key);
        _runtimeKeyTypes[key] = assetType;
        _typeLruNodesByKey[key] = node;
    }

    private void RemoveTrackedKey(AssetKey key)
    {
        if (!_runtimeKeyTypes.TryGetValue(key, out Type? assetType))
        {
            return;
        }

        _runtimeKeyTypes.Remove(key);
        if (_typeLruNodesByKey.TryGetValue(key, out LinkedListNode<AssetKey>? node))
        {
            _typeLruNodesByKey.Remove(key);
            if (_runtimeTypeLru.TryGetValue(assetType, out LinkedList<AssetKey>? list))
            {
                list.Remove(node);
                if (list.Count == 0)
                {
                    _runtimeTypeLru.Remove(assetType);
                }
            }
        }
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
