namespace Engine.Core.Handles;

public readonly record struct MaterialHandle
{
    public static MaterialHandle Invalid => default;

    public MaterialHandle(uint value)
    {
        HandleGuards.RequireNonZero(nameof(value), value);
        Value = value;
    }

    public uint Value { get; }

    public bool IsValid => Value != 0;

    public override string ToString() => IsValid ? $"MaterialHandle({Value})" : "MaterialHandle(Invalid)";
}
