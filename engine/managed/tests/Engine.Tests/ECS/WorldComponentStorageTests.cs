using System;
using System.Linq;
using Engine.Core.Handles;
using Engine.ECS;
using Xunit;

namespace Engine.Tests.ECS;

public sealed class WorldComponentStorageTests
{
    [Fact]
    public void AddComponent_StoresValueAndTryGetReturnsIt()
    {
        var world = new World();
        var entity = world.CreateEntity();
        var expected = new Position(3, 7);

        world.AddComponent(entity, expected);

        var found = world.TryGetComponent(entity, out Position actual);

        Assert.True(found);
        Assert.Equal(expected, actual);
        Assert.Equal(1, world.GetComponentCount<Position>());
    }

    [Fact]
    public void SetComponent_UpsertsAndOverridesExistingValue()
    {
        var world = new World();
        var entity = world.CreateEntity();

        world.SetComponent(entity, new Position(1, 1));
        world.SetComponent(entity, new Position(5, 8));

        Assert.True(world.TryGetComponent(entity, out Position actual));
        Assert.Equal(new Position(5, 8), actual);
        Assert.Equal(1, world.GetComponentCount<Position>());
    }

    [Fact]
    public void AddComponent_RejectsDuplicateForSameEntityGeneration()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Position(1, 1));

        var exception = Assert.Throws<InvalidOperationException>(() => world.AddComponent(entity, new Position(2, 2)));

        Assert.Contains("already exists", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryGetAndRemoveComponent_ReturnExpectedFlagsForMissingComponent()
    {
        var world = new World();
        var entity = world.CreateEntity();

        Assert.False(world.TryGetComponent(entity, out Position _));
        Assert.False(world.RemoveComponent<Position>(entity));
    }

    [Fact]
    public void RemoveComponent_RemovesOnlyRequestedType()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Position(1, 1));
        world.AddComponent(entity, new Velocity(2, 2));

        var removed = world.RemoveComponent<Position>(entity);

        Assert.True(removed);
        Assert.False(world.TryGetComponent(entity, out Position _));
        Assert.True(world.TryGetComponent(entity, out Velocity velocity));
        Assert.Equal(new Velocity(2, 2), velocity);
        Assert.Equal(0, world.GetComponentCount<Position>());
        Assert.Equal(1, world.GetComponentCount<Velocity>());
    }

    [Fact]
    public void ComponentOperations_RejectInvalidAndOutOfRangeHandles()
    {
        var world = new World();

        Assert.Throws<ArgumentException>(() => world.AddComponent(EntityId.Invalid, new Position(1, 1)));
        Assert.Throws<ArgumentException>(() => world.SetComponent(EntityId.Invalid, new Position(1, 1)));
        Assert.Throws<ArgumentException>(() => world.TryGetComponent(EntityId.Invalid, out Position _));
        Assert.Throws<ArgumentException>(() => world.RemoveComponent<Position>(EntityId.Invalid));

        var outOfRange = new EntityId(99, 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => world.AddComponent(outOfRange, new Position(1, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => world.SetComponent(outOfRange, new Position(1, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => world.TryGetComponent(outOfRange, out Position _));
        Assert.Throws<ArgumentOutOfRangeException>(() => world.RemoveComponent<Position>(outOfRange));
    }

    [Fact]
    public void ComponentOperations_RejectStaleHandle()
    {
        var world = new World();
        var stale = world.CreateEntity();
        world.AddComponent(stale, new Position(4, 4));
        world.DestroyEntity(stale);
        world.CreateEntity();

        var addException = Assert.Throws<InvalidOperationException>(() => world.AddComponent(stale, new Position(5, 5)));
        var setException = Assert.Throws<InvalidOperationException>(() => world.SetComponent(stale, new Position(5, 5)));
        var getException = Assert.Throws<InvalidOperationException>(() => world.TryGetComponent(stale, out Position _));
        var removeException = Assert.Throws<InvalidOperationException>(() => world.RemoveComponent<Position>(stale));

        Assert.Contains("stale", addException.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("stale", setException.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("stale", getException.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("stale", removeException.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DestroyEntity_ClearsAllEntityComponents()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Position(1, 2));
        world.AddComponent(entity, new Velocity(3, 4));

        world.DestroyEntity(entity);
        var reused = world.CreateEntity();

        Assert.Equal(entity.Index, reused.Index);
        Assert.False(world.TryGetComponent(reused, out Position _));
        Assert.False(world.TryGetComponent(reused, out Velocity _));
        Assert.Equal(0, world.GetComponentCount<Position>());
        Assert.Equal(0, world.GetComponentCount<Velocity>());
    }

    [Fact]
    public void QuerySingle_ReturnsAliveEntitiesWithComponent()
    {
        var world = new World();
        var first = world.CreateEntity();
        var second = world.CreateEntity();
        world.AddComponent(first, new Position(1, 1));
        world.AddComponent(second, new Position(2, 2));
        world.DestroyEntity(first);

        var result = world.Query<Position>().ToArray();

        var item = Assert.Single(result);
        Assert.Equal(second, item.Entity);
        Assert.Equal(new Position(2, 2), item.Component);
    }

    [Fact]
    public void QueryTwo_ReturnsIntersectionWithValues()
    {
        var world = new World();
        var both = world.CreateEntity();
        var onlyPosition = world.CreateEntity();
        var onlyVelocity = world.CreateEntity();

        world.AddComponent(both, new Position(10, 20));
        world.AddComponent(both, new Velocity(1, 2));
        world.AddComponent(onlyPosition, new Position(30, 40));
        world.AddComponent(onlyVelocity, new Velocity(3, 4));

        var result = world.Query<Position, Velocity>().ToArray();

        var item = Assert.Single(result);
        Assert.Equal(both, item.Entity);
        Assert.Equal(new Position(10, 20), item.Component1);
        Assert.Equal(new Velocity(1, 2), item.Component2);
    }

    [Fact]
    public void QueryTwo_SupportsSameComponentType()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Position(8, 9));

        var result = world.Query<Position, Position>().ToArray();

        var item = Assert.Single(result);
        Assert.Equal(entity, item.Entity);
        Assert.Equal(new Position(8, 9), item.Component1);
        Assert.Equal(new Position(8, 9), item.Component2);
    }

    private readonly record struct Position(int X, int Y);
    private readonly record struct Velocity(int X, int Y);
}
