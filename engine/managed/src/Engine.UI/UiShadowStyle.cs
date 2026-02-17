using System.Numerics;

namespace Engine.UI;

public readonly record struct UiShadowStyle
{
    public UiShadowStyle(Vector2 offset, float blurRadius, Vector4 color)
    {
        if (!float.IsFinite(offset.X) || !float.IsFinite(offset.Y))
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "Shadow offset components must be finite.");
        }

        if (!float.IsFinite(blurRadius) || blurRadius < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(blurRadius), "Shadow blur radius must be finite and non-negative.");
        }

        if (!float.IsFinite(color.X) || !float.IsFinite(color.Y) || !float.IsFinite(color.Z) || !float.IsFinite(color.W))
        {
            throw new ArgumentOutOfRangeException(nameof(color), "Shadow color components must be finite.");
        }

        Offset = offset;
        BlurRadius = blurRadius;
        Color = color;
    }

    public static UiShadowStyle None => new(Vector2.Zero, 0f, Vector4.Zero);

    public Vector2 Offset { get; }

    public float BlurRadius { get; }

    public Vector4 Color { get; }
}
