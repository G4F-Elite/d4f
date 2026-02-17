namespace Engine.Core.Handles;

public readonly record struct MeshHandle
{
    public static MeshHandle Invalid => default;

    public MeshHandle(ulong value)
    {
        HandleGuards.RequireNonZero(nameof(value), value);
        Value = value;
    }

    public ulong Value { get; }

    public bool IsValid => Value != 0;

    public override string ToString() => IsValid ? $"MeshHandle({Value})" : "MeshHandle(Invalid)";
}
