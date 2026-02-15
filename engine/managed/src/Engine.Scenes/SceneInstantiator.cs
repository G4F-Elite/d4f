using Engine.Core.Handles;
using Engine.ECS;

namespace Engine.Scenes;

public delegate void SceneComponentApplier(World world, EntityId entity, string typeId, ReadOnlySpan<byte> payload);

public static class SceneInstantiator
{
    public static IReadOnlyDictionary<uint, EntityId> Instantiate(
        World world,
        SceneAsset scene,
        SceneComponentApplier componentApplier)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(componentApplier);

        var map = CreateEntityMap(world, scene.Entities);

        foreach (var component in scene.Components)
        {
            if (!map.TryGetValue(component.EntityStableId, out var entity))
            {
                throw new InvalidOperationException(
                    $"Component '{component.TypeId}' references unknown entity id {component.EntityStableId}.");
            }

            componentApplier(world, entity, component.TypeId, component.Payload);
        }

        return map;
    }

    public static IReadOnlyDictionary<uint, EntityId> Instantiate(
        World world,
        PrefabAsset prefab,
        SceneComponentApplier componentApplier)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(prefab);
        ArgumentNullException.ThrowIfNull(componentApplier);

        var map = CreateEntityMap(world, prefab.Entities);

        foreach (var component in prefab.Components)
        {
            if (!map.TryGetValue(component.EntityStableId, out var entity))
            {
                throw new InvalidOperationException(
                    $"Component '{component.TypeId}' references unknown entity id {component.EntityStableId}.");
            }

            componentApplier(world, entity, component.TypeId, component.Payload);
        }

        return map;
    }

    private static Dictionary<uint, EntityId> CreateEntityMap(
        World world,
        IReadOnlyList<SceneEntityDefinition> entities)
    {
        var map = new Dictionary<uint, EntityId>(entities.Count);

        foreach (var entityDefinition in entities)
        {
            if (map.ContainsKey(entityDefinition.StableId))
            {
                throw new InvalidOperationException(
                    $"Duplicate stable id {entityDefinition.StableId} in scene entity list.");
            }

            map[entityDefinition.StableId] = world.CreateEntity();
        }

        return map;
    }
}
