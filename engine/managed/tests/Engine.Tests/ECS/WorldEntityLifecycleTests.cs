using System;
using Engine.Core.Handles;
using Engine.ECS;
using Xunit;

namespace Engine.Tests.ECS;

public sealed class WorldEntityLifecycleTests
{
    [Fact]
    public void CreateDestroyReuse_ReusesIndexWithNewGeneration()
    {
        var world = new World();

        var first = world.CreateEntity();
        world.DestroyEntity(first);
        var second = world.CreateEntity();

        Assert.Equal(first.Index, second.Index);
        Assert.NotEqual(first.Generation, second.Generation);
        Assert.False(world.IsAlive(first));
        Assert.True(world.IsAlive(second));
        Assert.Equal(1, world.AliveEntityCount);
    }

    [Fact]
    public void DestroyEntity_RejectsStaleHandle()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.DestroyEntity(entity);
        world.CreateEntity();

        var exception = Assert.Throws<InvalidOperationException>(() => world.DestroyEntity(entity));
        Assert.Contains("stale", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DestroyEntity_RejectsInvalidAndOutOfRangeHandles()
    {
        var world = new World();

        Assert.Throws<ArgumentException>(() => world.DestroyEntity(EntityId.Invalid));
        Assert.Throws<ArgumentOutOfRangeException>(() => world.DestroyEntity(new EntityId(100, 1)));
    }

    [Fact]
    public void EnumerateAliveEntities_ReturnsOnlyAliveEntities()
    {
        var world = new World();
        var first = world.CreateEntity();
        var second = world.CreateEntity();
        world.DestroyEntity(first);

        var alive = world.EnumerateAliveEntities();

        Assert.Single(alive);
        Assert.Contains(second, alive);
    }
}
