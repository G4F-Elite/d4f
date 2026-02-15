namespace Engine.Core.Handles;

public readonly record struct BodyHandle
{
    public static BodyHandle Invalid => default;

    public BodyHandle(uint value)
    {
        HandleGuards.RequireNonZero(nameof(value), value);
        Value = value;
    }

    public uint Value { get; }

    public bool IsValid => Value != 0;

    public override string ToString() => IsValid ? $"BodyHandle({Value})" : "BodyHandle(Invalid)";
}
