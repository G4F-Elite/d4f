using System.Globalization;
using Engine.Net;
using Engine.Rendering;

namespace Engine.Procedural;

public sealed record NetProceduralChunkEntityBinding(
    uint EntityId,
    string AssetKey,
    ulong ProceduralSeed,
    NetProceduralRecipeRef Recipe,
    RenderMeshInstance Instance)
{
    public NetProceduralChunkEntityBinding Validate()
    {
        if (EntityId == 0u)
        {
            throw new ArgumentOutOfRangeException(nameof(EntityId), "Entity id must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(AssetKey))
        {
            throw new ArgumentException("Asset key cannot be empty.", nameof(AssetKey));
        }

        ArgumentNullException.ThrowIfNull(Recipe);
        if (!Instance.Mesh.IsValid || !Instance.Material.IsValid || !Instance.Texture.IsValid)
        {
            throw new InvalidDataException("Render instance contains an invalid handle.");
        }

        return this;
    }
}

public sealed record NetProceduralChunkApplyResult(
    IReadOnlyList<NetProceduralChunkEntityBinding> ActiveEntities,
    IReadOnlyList<uint> SpawnedEntityIds,
    IReadOnlyList<uint> UpdatedEntityIds,
    IReadOnlyList<uint> DespawnedEntityIds,
    int CachedAssetCount)
{
    public NetProceduralChunkApplyResult Validate()
    {
        ArgumentNullException.ThrowIfNull(ActiveEntities);
        ArgumentNullException.ThrowIfNull(SpawnedEntityIds);
        ArgumentNullException.ThrowIfNull(UpdatedEntityIds);
        ArgumentNullException.ThrowIfNull(DespawnedEntityIds);
        if (CachedAssetCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(CachedAssetCount), "Cached asset count cannot be negative.");
        }

        foreach (NetProceduralChunkEntityBinding binding in ActiveEntities)
        {
            _ = binding.Validate();
        }

        return this;
    }
}

public sealed class NetProceduralChunkReplicator : IDisposable
{
    private const string ChunkGeneratorId = "proc/chunk/content";
    private readonly IRenderingFacade _rendering;
    private readonly ProceduralChunkUploadOptions _uploadOptions;
    private readonly int _defaultSurfaceWidth;
    private readonly int _defaultSurfaceHeight;
    private readonly Dictionary<string, CachedAssetEntry> _assetsByKey = new(StringComparer.Ordinal);
    private readonly Dictionary<uint, TrackedEntityEntry> _entitiesById = new();
    private bool _disposed;

    public NetProceduralChunkReplicator(
        IRenderingFacade rendering,
        int defaultSurfaceWidth = 128,
        int defaultSurfaceHeight = 128,
        in ProceduralChunkUploadOptions uploadOptions = default)
    {
        ArgumentNullException.ThrowIfNull(rendering);
        if (defaultSurfaceWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultSurfaceWidth), "Default surface width must be greater than zero.");
        }

        if (defaultSurfaceHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultSurfaceHeight), "Default surface height must be greater than zero.");
        }

        _rendering = rendering;
        _uploadOptions = uploadOptions;
        _defaultSurfaceWidth = defaultSurfaceWidth;
        _defaultSurfaceHeight = defaultSurfaceHeight;
    }

    public NetProceduralChunkApplyResult Apply(NetSnapshot snapshot)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(snapshot);

        var seenEntityIds = new HashSet<uint>();
        var spawnedEntityIds = new List<uint>();
        var updatedEntityIds = new List<uint>();
        var despawnedEntityIds = new List<uint>();

        foreach (NetEntityState entity in snapshot.Entities)
        {
            seenEntityIds.Add(entity.EntityId);

            if (entity.ProceduralRecipe is null || !IsSupportedRecipe(entity.ProceduralRecipe))
            {
                if (_entitiesById.Remove(entity.EntityId, out TrackedEntityEntry? removed))
                {
                    ReleaseAssetReference(removed.AssetKey);
                    despawnedEntityIds.Add(entity.EntityId);
                }

                continue;
            }

            if (_entitiesById.TryGetValue(entity.EntityId, out TrackedEntityEntry? existing) &&
                existing.AssetKey == entity.AssetKey &&
                existing.ProceduralSeed == entity.ProceduralSeed &&
                RecipesAreEquivalent(existing.Recipe, entity.ProceduralRecipe))
            {
                continue;
            }

            if (existing is null)
            {
                spawnedEntityIds.Add(entity.EntityId);
            }
            else
            {
                ReleaseAssetReference(existing.AssetKey);
                updatedEntityIds.Add(entity.EntityId);
            }

            CachedAssetEntry assetEntry = AcquireAsset(entity.AssetKey, entity.ProceduralSeed, entity.ProceduralRecipe);
            _entitiesById[entity.EntityId] = new TrackedEntityEntry(
                entity.EntityId,
                entity.AssetKey,
                entity.ProceduralSeed,
                entity.ProceduralRecipe,
                assetEntry.Upload.Instance);
        }

        if (_entitiesById.Count > seenEntityIds.Count)
        {
            uint[] trackedEntityIds = _entitiesById.Keys.ToArray();
            foreach (uint trackedEntityId in trackedEntityIds)
            {
                if (seenEntityIds.Contains(trackedEntityId))
                {
                    continue;
                }

                TrackedEntityEntry removed = _entitiesById[trackedEntityId];
                _entitiesById.Remove(trackedEntityId);
                ReleaseAssetReference(removed.AssetKey);
                despawnedEntityIds.Add(trackedEntityId);
            }
        }

        NetProceduralChunkEntityBinding[] activeEntities = _entitiesById.Values
            .OrderBy(static x => x.EntityId)
            .Select(static x => new NetProceduralChunkEntityBinding(
                x.EntityId,
                x.AssetKey,
                x.ProceduralSeed,
                x.Recipe,
                x.Instance))
            .ToArray();
        spawnedEntityIds.Sort();
        updatedEntityIds.Sort();
        despawnedEntityIds.Sort();

        return new NetProceduralChunkApplyResult(
            ActiveEntities: activeEntities,
            SpawnedEntityIds: spawnedEntityIds,
            UpdatedEntityIds: updatedEntityIds,
            DespawnedEntityIds: despawnedEntityIds,
            CachedAssetCount: _assetsByKey.Count).Validate();
    }

    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        foreach (CachedAssetEntry entry in _assetsByKey.Values)
        {
            entry.Upload.Destroy(_rendering);
        }

        _entitiesById.Clear();
        _assetsByKey.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (CachedAssetEntry entry in _assetsByKey.Values)
        {
            entry.Upload.Destroy(_rendering);
        }

        _entitiesById.Clear();
        _assetsByKey.Clear();
        _disposed = true;
    }

    private CachedAssetEntry AcquireAsset(
        string assetKey,
        ulong proceduralSeed,
        NetProceduralRecipeRef recipe)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetKey);
        ArgumentNullException.ThrowIfNull(recipe);

        if (_assetsByKey.TryGetValue(assetKey, out CachedAssetEntry? existing))
        {
            if (existing.ProceduralSeed != proceduralSeed ||
                !RecipesAreEquivalent(existing.Recipe, recipe))
            {
                throw new InvalidDataException(
                    $"Asset key '{assetKey}' was reused with different procedural recipe metadata.");
            }

            existing.RefCount = checked(existing.RefCount + 1);
            return existing;
        }

        ChunkRecipeDescriptor descriptor = ParseRecipeDescriptor(recipe, _defaultSurfaceWidth, _defaultSurfaceHeight);
        if (proceduralSeed > uint.MaxValue)
        {
            throw new InvalidDataException(
                $"Procedural seed '{proceduralSeed}' for asset '{assetKey}' exceeds supported 32-bit range.");
        }

        var chunk = new LevelMeshChunk(
            NodeId: descriptor.NodeId,
            MeshTag: descriptor.MeshTag);
        ProceduralChunkContent content = ProceduralChunkContentFactory.Build(
            chunk,
            seed: (uint)proceduralSeed,
            surfaceWidth: descriptor.SurfaceWidth,
            surfaceHeight: descriptor.SurfaceHeight);
        ProceduralChunkUploadResult upload = ProceduralChunkRenderUploader.Upload(_rendering, content, _uploadOptions);

        var created = new CachedAssetEntry(
            assetKey,
            proceduralSeed,
            recipe,
            upload,
            refCount: 1);
        _assetsByKey.Add(assetKey, created);
        return created;
    }

    private void ReleaseAssetReference(string assetKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetKey);

        if (!_assetsByKey.TryGetValue(assetKey, out CachedAssetEntry? entry))
        {
            throw new InvalidOperationException(
                $"Asset key '{assetKey}' was not found while releasing a tracked entity.");
        }

        if (entry.RefCount <= 0)
        {
            throw new InvalidOperationException(
                $"Asset key '{assetKey}' has an invalid reference count '{entry.RefCount}'.");
        }

        entry.RefCount--;
        if (entry.RefCount == 0)
        {
            entry.Upload.Destroy(_rendering);
            _assetsByKey.Remove(assetKey);
        }
    }

    private static bool IsSupportedRecipe(NetProceduralRecipeRef recipe)
    {
        return string.Equals(recipe.GeneratorId, ChunkGeneratorId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool RecipesAreEquivalent(NetProceduralRecipeRef left, NetProceduralRecipeRef right)
    {
        if (!string.Equals(left.GeneratorId, right.GeneratorId, StringComparison.OrdinalIgnoreCase) ||
            left.GeneratorVersion != right.GeneratorVersion ||
            left.RecipeVersion != right.RecipeVersion ||
            !string.Equals(left.RecipeHash, right.RecipeHash, StringComparison.Ordinal))
        {
            return false;
        }

        if (left.Parameters.Count != right.Parameters.Count)
        {
            return false;
        }

        foreach ((string key, string leftValue) in left.Parameters)
        {
            if (!right.Parameters.TryGetValue(key, out string? rightValue) ||
                !string.Equals(leftValue, rightValue, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static ChunkRecipeDescriptor ParseRecipeDescriptor(
        NetProceduralRecipeRef recipe,
        int defaultSurfaceWidth,
        int defaultSurfaceHeight)
    {
        ArgumentNullException.ThrowIfNull(recipe);

        if (!recipe.Parameters.TryGetValue("meshTag", out string? meshTag) ||
            string.IsNullOrWhiteSpace(meshTag))
        {
            throw new InvalidDataException("Chunk recipe parameter 'meshTag' is required.");
        }

        int nodeId = ParseRequiredInt(recipe.Parameters, "nodeId", minValue: 0, maxValue: int.MaxValue);
        int surfaceWidth = ParseOptionalInt(recipe.Parameters, "surfaceWidth", defaultSurfaceWidth, minValue: 1, maxValue: 4096);
        int surfaceHeight = ParseOptionalInt(recipe.Parameters, "surfaceHeight", defaultSurfaceHeight, minValue: 1, maxValue: 4096);
        return new ChunkRecipeDescriptor(
            NodeId: nodeId,
            MeshTag: meshTag.Trim(),
            SurfaceWidth: surfaceWidth,
            SurfaceHeight: surfaceHeight);
    }

    private static int ParseRequiredInt(
        IReadOnlyDictionary<string, string> parameters,
        string key,
        int minValue,
        int maxValue)
    {
        if (!parameters.TryGetValue(key, out string? raw))
        {
            throw new InvalidDataException($"Chunk recipe parameter '{key}' is required.");
        }

        return ParseIntValue(raw, key, minValue, maxValue);
    }

    private static int ParseOptionalInt(
        IReadOnlyDictionary<string, string> parameters,
        string key,
        int defaultValue,
        int minValue,
        int maxValue)
    {
        if (!parameters.TryGetValue(key, out string? raw))
        {
            return defaultValue;
        }

        return ParseIntValue(raw, key, minValue, maxValue);
    }

    private static int ParseIntValue(string rawValue, string key, int minValue, int maxValue)
    {
        if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
        {
            throw new InvalidDataException(
                $"Chunk recipe parameter '{key}' must be an integer value.");
        }

        if (parsed < minValue || parsed > maxValue)
        {
            throw new InvalidDataException(
                $"Chunk recipe parameter '{key}' must be within [{minValue}..{maxValue}].");
        }

        return parsed;
    }

    private sealed class CachedAssetEntry
    {
        public CachedAssetEntry(
            string assetKey,
            ulong proceduralSeed,
            NetProceduralRecipeRef recipe,
            ProceduralChunkUploadResult upload,
            int refCount)
        {
            AssetKey = assetKey;
            ProceduralSeed = proceduralSeed;
            Recipe = recipe;
            Upload = upload;
            RefCount = refCount;
        }

        public string AssetKey { get; }

        public ulong ProceduralSeed { get; }

        public NetProceduralRecipeRef Recipe { get; }

        public ProceduralChunkUploadResult Upload { get; }

        public int RefCount { get; set; }
    }

    private sealed record TrackedEntityEntry(
        uint EntityId,
        string AssetKey,
        ulong ProceduralSeed,
        NetProceduralRecipeRef Recipe,
        RenderMeshInstance Instance);

    private readonly record struct ChunkRecipeDescriptor(
        int NodeId,
        string MeshTag,
        int SurfaceWidth,
        int SurfaceHeight);
}
