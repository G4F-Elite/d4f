using System;
using System.Collections.Generic;
using Engine.Core.Handles;

namespace Engine.ECS;

public sealed partial class World
{
    public void AddComponent<T>(EntityId entity, T component)
        where T : struct
    {
        EnsureEntityIsAlive(entity);
        _componentStorage.Add(entity, component);
    }

    public void SetComponent<T>(EntityId entity, T component)
        where T : struct
    {
        EnsureEntityIsAlive(entity);
        _componentStorage.Set(entity, component);
    }

    public bool TryGetComponent<T>(EntityId entity, out T component)
        where T : struct
    {
        EnsureEntityIsAlive(entity);
        return _componentStorage.TryGet(entity, out component);
    }

    public bool RemoveComponent<T>(EntityId entity)
        where T : struct
    {
        EnsureEntityIsAlive(entity);
        return _componentStorage.Remove<T>(entity);
    }

    public int GetComponentCount<T>()
        where T : struct
    {
        return _componentStorage.GetCount<T>();
    }

    public IEnumerable<(EntityId Entity, T Component)> Query<T>()
        where T : struct
    {
        if (!_componentStorage.TryGetPool<T>(out var pool))
        {
            yield break;
        }

        foreach (var entry in pool.EnumerateEntries())
        {
            if (!TryCreateAliveEntity(entry.EntityIndex, entry.Generation, out var entity))
            {
                continue;
            }

            yield return (entity, entry.Value);
        }
    }

    public void QueryNonAlloc<T>(List<(EntityId Entity, T Component)> destination)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(destination);
        destination.Clear();

        if (!_componentStorage.TryGetPool<T>(out var pool))
        {
            return;
        }

        foreach (ComponentEntry<T> entry in pool.EnumerateEntriesNoAlloc())
        {
            if (!TryCreateAliveEntity(entry.EntityIndex, entry.Generation, out EntityId entity))
            {
                continue;
            }

            destination.Add((entity, entry.Value));
        }
    }

    public IEnumerable<(EntityId Entity, T1 Component1, T2 Component2)> Query<T1, T2>()
        where T1 : struct
        where T2 : struct
    {
        if (!_componentStorage.TryGetPool<T1>(out var firstPool) || !_componentStorage.TryGetPool<T2>(out var secondPool))
        {
            yield break;
        }

        if (firstPool.Count <= secondPool.Count)
        {
            foreach (var entry in firstPool.EnumerateEntries())
            {
                if (!secondPool.TryGet(entry.EntityIndex, entry.Generation, out var secondComponent))
                {
                    continue;
                }

                if (!TryCreateAliveEntity(entry.EntityIndex, entry.Generation, out var entity))
                {
                    continue;
                }

                yield return (entity, entry.Value, secondComponent);
            }

            yield break;
        }

        foreach (var entry in secondPool.EnumerateEntries())
        {
            if (!firstPool.TryGet(entry.EntityIndex, entry.Generation, out var firstComponent))
            {
                continue;
            }

            if (!TryCreateAliveEntity(entry.EntityIndex, entry.Generation, out var entity))
            {
                continue;
            }

            yield return (entity, firstComponent, entry.Value);
        }
    }

    private void EnsureEntityIsAlive(EntityId entity)
    {
        if (!entity.IsValid)
        {
            throw new ArgumentException("Entity id is invalid.", nameof(entity));
        }

        if (entity.Index >= _entitySlots.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(entity), "Entity id points outside of the world range.");
        }

        var slot = _entitySlots[entity.Index];

        if (!slot.IsAlive)
        {
            throw new InvalidOperationException($"Entity {entity} was already destroyed.");
        }

        if (slot.Generation != entity.Generation)
        {
            throw new InvalidOperationException($"Entity {entity} is stale; current generation is {slot.Generation}.");
        }
    }

    private bool TryCreateAliveEntity(int entityIndex, uint generation, out EntityId entity)
    {
        if ((uint)entityIndex >= (uint)_entitySlots.Count)
        {
            entity = default;
            return false;
        }

        var slot = _entitySlots[entityIndex];
        if (!slot.IsAlive || slot.Generation != generation)
        {
            entity = default;
            return false;
        }

        entity = new EntityId(entityIndex, slot.Generation);
        return true;
    }
}
