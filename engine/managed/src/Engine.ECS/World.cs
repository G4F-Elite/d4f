using System;
using System.Collections.Generic;
using Engine.Core.Handles;
using Engine.Core.Timing;

namespace Engine.ECS;

public sealed partial class World
{
    private readonly List<EntitySlot> _entitySlots = [];
    private readonly Stack<int> _freeIndices = [];
    private readonly Dictionary<SystemStage, StageSystems> _systems = CreateStageSystemMap();
    private readonly ComponentStorage _componentStorage = new();
    private int _nextRegistrationOrder;
    private int _aliveCount;

    public int AliveEntityCount => _aliveCount;

    public EntityId CreateEntity()
    {
        if (_freeIndices.Count == 0)
        {
            var newIndex = _entitySlots.Count;
            _entitySlots.Add(new EntitySlot { Generation = 1, IsAlive = true });
            _aliveCount++;
            return new EntityId(newIndex, 1);
        }

        var reusedIndex = _freeIndices.Pop();
        var slot = _entitySlots[reusedIndex];

        if (slot.IsAlive)
        {
            throw new InvalidOperationException($"Entity slot {reusedIndex} is already alive.");
        }

        slot.IsAlive = true;
        slot.Generation = slot.Generation == 0 ? 1 : slot.Generation;
        _entitySlots[reusedIndex] = slot;
        _aliveCount++;

        return new EntityId(reusedIndex, slot.Generation);
    }

    public bool IsAlive(EntityId entity)
    {
        if (!entity.IsValid)
        {
            return false;
        }

        if (entity.Index >= _entitySlots.Count)
        {
            return false;
        }

        var slot = _entitySlots[entity.Index];
        return slot.IsAlive && slot.Generation == entity.Generation;
    }

    public void DestroyEntity(EntityId entity)
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

        slot.IsAlive = false;
        slot.Generation = NextGeneration(slot.Generation);
        _entitySlots[entity.Index] = slot;
        _componentStorage.RemoveAll(entity.Index);
        _freeIndices.Push(entity.Index);
        _aliveCount--;
    }

    public IEnumerable<EntityId> EnumerateAliveEntities()
    {
        for (var i = 0; i < _entitySlots.Count; i++)
        {
            var slot = _entitySlots[i];
            if (!slot.IsAlive)
            {
                continue;
            }

            yield return new EntityId(i, slot.Generation);
        }
    }

    public void RegisterSystem(SystemStage stage, IWorldSystem system, int priority = 0)
    {
        ArgumentNullException.ThrowIfNull(system);

        var stageSystems = GetStageSystems(stage);
        stageSystems.Add(system, priority, _nextRegistrationOrder);
        _nextRegistrationOrder++;
    }

    public void RunStage(SystemStage stage, in FrameTiming timing)
    {
        var stageSystems = GetStageSystems(stage);
        stageSystems.Execute(this, timing);
    }

    public int GetSystemCount(SystemStage stage)
    {
        var stageSystems = GetStageSystems(stage);
        return stageSystems.Count;
    }

    private static Dictionary<SystemStage, StageSystems> CreateStageSystemMap() =>
        new()
        {
            [SystemStage.PrePhysics] = new StageSystems(),
            [SystemStage.PostPhysics] = new StageSystems(),
            [SystemStage.PreRender] = new StageSystems(),
            [SystemStage.UI] = new StageSystems()
        };

    private StageSystems GetStageSystems(SystemStage stage)
    {
        if (_systems.TryGetValue(stage, out var stageSystems))
        {
            return stageSystems;
        }

        throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unknown system stage.");
    }

    private static uint NextGeneration(uint currentGeneration)
    {
        var nextGeneration = currentGeneration + 1;
        return nextGeneration == 0 ? 1u : nextGeneration;
    }

    private struct EntitySlot
    {
        public uint Generation;
        public bool IsAlive;
    }

    private sealed class StageSystems
    {
        private readonly List<SystemRegistration> _registrations = [];
        private bool _isSorted = true;

        public int Count => _registrations.Count;

        public void Add(IWorldSystem system, int priority, int registrationOrder)
        {
            _registrations.Add(new SystemRegistration(system, priority, registrationOrder));
            _isSorted = false;
        }

        public void Execute(World world, in FrameTiming timing)
        {
            EnsureSorted();

            foreach (var registration in _registrations)
            {
                registration.System.Update(world, timing);
            }
        }

        private void EnsureSorted()
        {
            if (_isSorted || _registrations.Count <= 1)
            {
                _isSorted = true;
                return;
            }

            _registrations.Sort(SystemRegistrationComparer.Instance);
            _isSorted = true;
        }
    }

    private readonly record struct SystemRegistration(IWorldSystem System, int Priority, int RegistrationOrder);

    private sealed class SystemRegistrationComparer : IComparer<SystemRegistration>
    {
        public static readonly SystemRegistrationComparer Instance = new();

        public int Compare(SystemRegistration x, SystemRegistration y)
        {
            var byPriority = x.Priority.CompareTo(y.Priority);
            return byPriority != 0 ? byPriority : x.RegistrationOrder.CompareTo(y.RegistrationOrder);
        }
    }
}
