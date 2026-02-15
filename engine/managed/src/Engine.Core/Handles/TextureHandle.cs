namespace Engine.Core.Handles;

public readonly record struct TextureHandle
{
    public static TextureHandle Invalid => default;

    public TextureHandle(uint value)
    {
        HandleGuards.RequireNonZero(nameof(value), value);
        Value = value;
    }

    public uint Value { get; }

    public bool IsValid => Value != 0;

    public override string ToString() => IsValid ? $"TextureHandle({Value})" : "TextureHandle(Invalid)";
}
