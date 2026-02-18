using System;
using System.Collections.Generic;
using Engine.Core.Handles;

namespace Engine.ECS;

internal sealed class ComponentStorage
{
    private readonly Dictionary<Type, IComponentPool> _pools = [];

    public void Add<T>(EntityId entity, T component)
        where T : struct
    {
        GetOrCreatePool<T>().Add(entity.Index, entity.Generation, component);
    }

    public void Set<T>(EntityId entity, T component)
        where T : struct
    {
        GetOrCreatePool<T>().Set(entity.Index, entity.Generation, component);
    }

    public bool TryGet<T>(EntityId entity, out T component)
        where T : struct
    {
        if (TryGetPool<T>(out var pool))
        {
            return pool.TryGet(entity.Index, entity.Generation, out component);
        }

        component = default;
        return false;
    }

    public bool Remove<T>(EntityId entity)
        where T : struct
    {
        return TryGetPool<T>(out var pool) && pool.Remove(entity.Index, entity.Generation);
    }

    public int GetCount<T>()
        where T : struct
    {
        return TryGetPool<T>(out var pool) ? pool.Count : 0;
    }

    public void RemoveAll(int entityIndex)
    {
        foreach (var pool in _pools.Values)
        {
            pool.RemoveEntity(entityIndex);
        }
    }

    public bool TryGetPool<T>(out ComponentPool<T> pool)
        where T : struct
    {
        if (_pools.TryGetValue(typeof(T), out var rawPool))
        {
            pool = (ComponentPool<T>)rawPool;
            return true;
        }

        pool = null!;
        return false;
    }

    private ComponentPool<T> GetOrCreatePool<T>()
        where T : struct
    {
        if (_pools.TryGetValue(typeof(T), out var rawPool))
        {
            return (ComponentPool<T>)rawPool;
        }

        var pool = new ComponentPool<T>();
        _pools.Add(typeof(T), pool);
        return pool;
    }
}

internal interface IComponentPool
{
    void RemoveEntity(int entityIndex);
}

internal readonly record struct ComponentEntry<T>(int EntityIndex, uint Generation, T Value)
    where T : struct;

internal sealed class ComponentPool<T> : IComponentPool
    where T : struct
{
    private readonly Dictionary<int, ComponentRecord> _components = [];

    public int Count => _components.Count;

    public void Add(int entityIndex, uint generation, T component)
    {
        if (_components.TryGetValue(entityIndex, out var existing) && existing.Generation == generation)
        {
            throw new InvalidOperationException($"Component {typeof(T).Name} already exists for entity {entityIndex}:{generation}.");
        }

        _components[entityIndex] = new ComponentRecord(component, generation);
    }

    public void Set(int entityIndex, uint generation, T component)
    {
        _components[entityIndex] = new ComponentRecord(component, generation);
    }

    public bool TryGet(int entityIndex, uint generation, out T component)
    {
        if (_components.TryGetValue(entityIndex, out var record) && record.Generation == generation)
        {
            component = record.Value;
            return true;
        }

        component = default;
        return false;
    }

    public bool Remove(int entityIndex, uint generation)
    {
        if (!_components.TryGetValue(entityIndex, out var record) || record.Generation != generation)
        {
            return false;
        }

        return _components.Remove(entityIndex);
    }

    public IEnumerable<ComponentEntry<T>> EnumerateEntries()
    {
        foreach (var (entityIndex, record) in _components)
        {
            yield return new ComponentEntry<T>(entityIndex, record.Generation, record.Value);
        }
    }

    public EntryEnumerable EnumerateEntriesNoAlloc()
    {
        return new EntryEnumerable(_components);
    }

    public void RemoveEntity(int entityIndex)
    {
        _components.Remove(entityIndex);
    }

    public readonly struct EntryEnumerable
    {
        private readonly Dictionary<int, ComponentRecord> _components;

        internal EntryEnumerable(Dictionary<int, ComponentRecord> components)
        {
            _components = components ?? throw new ArgumentNullException(nameof(components));
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(_components.GetEnumerator());
        }
    }

    public struct Enumerator
    {
        private Dictionary<int, ComponentRecord>.Enumerator _enumerator;

        internal Enumerator(Dictionary<int, ComponentRecord>.Enumerator enumerator)
        {
            _enumerator = enumerator;
        }

        public ComponentEntry<T> Current
        {
            get
            {
                KeyValuePair<int, ComponentRecord> current = _enumerator.Current;
                return new ComponentEntry<T>(current.Key, current.Value.Generation, current.Value.Value);
            }
        }

        public bool MoveNext()
        {
            return _enumerator.MoveNext();
        }
    }

    internal readonly record struct ComponentRecord(T Value, uint Generation);
}
