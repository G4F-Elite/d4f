using System;

namespace Engine.Core.Handles;

public readonly record struct EntityId
{
    public static EntityId Invalid => default;

    public EntityId(int index, uint generation)
    {
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Entity index must be non-negative.");
        }

        HandleGuards.RequireNonZero(nameof(generation), generation);

        Index = index;
        Generation = generation;
    }

    public int Index { get; }

    public uint Generation { get; }

    public bool IsValid => Index >= 0 && Generation != 0;

    public override string ToString() => IsValid ? $"EntityId({Index}:{Generation})" : "EntityId(Invalid)";
}
