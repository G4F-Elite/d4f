namespace Engine.Core.Geometry;

public readonly record struct RectF
{
    public RectF(float x, float y, float width, float height)
    {
        if (width < 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width cannot be negative.");
        }

        if (height < 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height cannot be negative.");
        }

        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public static RectF Empty => default;

    public float X { get; }

    public float Y { get; }

    public float Width { get; }

    public float Height { get; }

    public float Right => X + Width;

    public float Bottom => Y + Height;

    public bool Contains(float x, float y)
    {
        return x >= X && x <= Right && y >= Y && y <= Bottom;
    }
}
