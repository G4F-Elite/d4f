using System.Text;
using Engine.Core.Handles;
using Engine.ECS;
using Engine.Scenes;
using Xunit;

namespace Engine.Tests.Scenes;

public sealed class SceneInstantiatorTests
{
    [Fact]
    public void InstantiateScene_CreatesEntitiesAndAppliesComponents()
    {
        var world = new World();
        var scene = new SceneAsset(
            [new SceneEntityDefinition(10, "A"), new SceneEntityDefinition(20, "B")],
            [
                new SceneComponentEntry(10, "Tag", Encoding.UTF8.GetBytes("Player")),
                new SceneComponentEntry(20, "Tag", Encoding.UTF8.GetBytes("Camera"))
            ]);

        var applied = new List<(EntityId Entity, string TypeId, byte[] Payload)>();

        var map = SceneInstantiator.Instantiate(
            world,
            scene,
            (_, entity, typeId, payload) =>
            {
                applied.Add((entity, typeId, payload.ToArray()));
            });

        Assert.Equal(2, map.Count);
        Assert.True(map.ContainsKey(10));
        Assert.True(map.ContainsKey(20));
        Assert.All(map.Values, entity => Assert.True(world.IsAlive(entity)));

        Assert.Equal(2, applied.Count);
        Assert.Contains(applied, x =>
            ResolveStableId(map, x.Entity) == 10 &&
            x.TypeId == "Tag" &&
            x.Payload.SequenceEqual(Encoding.UTF8.GetBytes("Player")));
        Assert.Contains(applied, x =>
            ResolveStableId(map, x.Entity) == 20 &&
            x.TypeId == "Tag" &&
            x.Payload.SequenceEqual(Encoding.UTF8.GetBytes("Camera")));
    }

    [Fact]
    public void InstantiatePrefab_CreatesEntitiesAndAppliesComponents()
    {
        var world = new World();
        var prefab = new PrefabAsset(
            [new SceneEntityDefinition(1, "Root")],
            [new SceneComponentEntry(1, "Transform", [1, 2, 3])]);

        EntityId appliedEntity = default;
        string? appliedType = null;

        var map = SceneInstantiator.Instantiate(
            world,
            prefab,
            (_, entity, typeId, _) =>
            {
                appliedEntity = entity;
                appliedType = typeId;
            });

        Assert.Single(map);
        Assert.True(world.IsAlive(appliedEntity));
        Assert.Equal("Transform", appliedType);
    }

    [Fact]
    public void Instantiate_RejectsNullArguments()
    {
        var world = new World();
        var scene = new SceneAsset([new SceneEntityDefinition(1, "A")], []);
        var prefab = new PrefabAsset([new SceneEntityDefinition(1, "A")], []);

        Assert.Throws<ArgumentNullException>(() => SceneInstantiator.Instantiate(null!, scene, (_, _, _, _) => { }));
        Assert.Throws<ArgumentNullException>(() => SceneInstantiator.Instantiate(world, (SceneAsset)null!, (_, _, _, _) => { }));
        Assert.Throws<ArgumentNullException>(() => SceneInstantiator.Instantiate(world, scene, null!));

        Assert.Throws<ArgumentNullException>(() => SceneInstantiator.Instantiate(null!, prefab, (_, _, _, _) => { }));
        Assert.Throws<ArgumentNullException>(() => SceneInstantiator.Instantiate(world, (PrefabAsset)null!, (_, _, _, _) => { }));
        Assert.Throws<ArgumentNullException>(() => SceneInstantiator.Instantiate(world, prefab, null!));
    }

    private static uint ResolveStableId(IReadOnlyDictionary<uint, EntityId> map, EntityId entity)
    {
        foreach (var pair in map)
        {
            if (pair.Value == entity)
            {
                return pair.Key;
            }
        }

        throw new InvalidOperationException($"Entity {entity} was not found in scene map.");
    }
}
