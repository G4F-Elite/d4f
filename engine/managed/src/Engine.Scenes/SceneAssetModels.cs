using System.Collections.ObjectModel;

namespace Engine.Scenes;

public readonly record struct SceneEntityDefinition
{
    public SceneEntityDefinition(uint stableId, string name)
    {
        if (stableId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(stableId), "Stable id must be non-zero.");
        }

        ArgumentNullException.ThrowIfNull(name);

        StableId = stableId;
        Name = name;
    }

    public uint StableId { get; }

    public string Name { get; }
}

public sealed class SceneComponentEntry
{
    public SceneComponentEntry(uint entityStableId, string typeId, ReadOnlySpan<byte> payload)
    {
        if (entityStableId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(entityStableId), "Entity stable id must be non-zero.");
        }

        if (string.IsNullOrWhiteSpace(typeId))
        {
            throw new ArgumentException("Component type id must be non-empty.", nameof(typeId));
        }

        EntityStableId = entityStableId;
        TypeId = typeId;
        Payload = payload.ToArray();
    }

    public uint EntityStableId { get; }

    public string TypeId { get; }

    public byte[] Payload { get; }
}

public sealed class SceneAsset
{
    public SceneAsset(IReadOnlyList<SceneEntityDefinition> entities, IReadOnlyList<SceneComponentEntry> components)
    {
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(components);

        ValidateEntitiesAndComponents(entities, components);

        Entities = new ReadOnlyCollection<SceneEntityDefinition>(entities.ToArray());
        Components = new ReadOnlyCollection<SceneComponentEntry>(components.ToArray());
    }

    public uint Version => SceneFormat.SceneAssetVersion;

    public IReadOnlyList<SceneEntityDefinition> Entities { get; }

    public IReadOnlyList<SceneComponentEntry> Components { get; }

    private static void ValidateEntitiesAndComponents(
        IReadOnlyList<SceneEntityDefinition> entities,
        IReadOnlyList<SceneComponentEntry> components)
    {
        var knownEntityIds = new HashSet<uint>(entities.Count);
        foreach (var entity in entities)
        {
            if (!knownEntityIds.Add(entity.StableId))
            {
                throw new InvalidOperationException(
                    $"Scene contains duplicate entity stable id {entity.StableId}.");
            }
        }

        foreach (var component in components)
        {
            if (!knownEntityIds.Contains(component.EntityStableId))
            {
                throw new InvalidOperationException(
                    $"Component '{component.TypeId}' references unknown entity id {component.EntityStableId}.");
            }
        }
    }
}

public sealed class PrefabAsset
{
    public PrefabAsset(IReadOnlyList<SceneEntityDefinition> entities, IReadOnlyList<SceneComponentEntry> components)
    {
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(components);

        if (entities.Count == 0)
        {
            throw new InvalidOperationException("Prefab must contain at least one entity.");
        }

        ValidateEntitiesAndComponents(entities, components);

        Entities = new ReadOnlyCollection<SceneEntityDefinition>(entities.ToArray());
        Components = new ReadOnlyCollection<SceneComponentEntry>(components.ToArray());
    }

    public uint Version => SceneFormat.PrefabAssetVersion;

    public IReadOnlyList<SceneEntityDefinition> Entities { get; }

    public IReadOnlyList<SceneComponentEntry> Components { get; }

    private static void ValidateEntitiesAndComponents(
        IReadOnlyList<SceneEntityDefinition> entities,
        IReadOnlyList<SceneComponentEntry> components)
    {
        var knownEntityIds = new HashSet<uint>(entities.Count);
        foreach (var entity in entities)
        {
            if (!knownEntityIds.Add(entity.StableId))
            {
                throw new InvalidOperationException(
                    $"Prefab contains duplicate entity stable id {entity.StableId}.");
            }
        }

        foreach (var component in components)
        {
            if (!knownEntityIds.Contains(component.EntityStableId))
            {
                throw new InvalidOperationException(
                    $"Component '{component.TypeId}' references unknown entity id {component.EntityStableId}.");
            }
        }
    }
}
