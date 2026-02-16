namespace Engine.UI;

public readonly record struct UiThickness
{
    public UiThickness(float left, float top, float right, float bottom)
    {
        if (left < 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(left), "Thickness components cannot be negative.");
        }

        if (top < 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(top), "Thickness components cannot be negative.");
        }

        if (right < 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(right), "Thickness components cannot be negative.");
        }

        if (bottom < 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(bottom), "Thickness components cannot be negative.");
        }

        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }

    public static UiThickness Zero => default;

    public float Left { get; }

    public float Top { get; }

    public float Right { get; }

    public float Bottom { get; }
}
