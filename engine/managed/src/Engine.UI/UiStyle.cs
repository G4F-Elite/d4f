using System.Numerics;

namespace Engine.UI;

public sealed class UiStyle
{
    private string? _fontFamily;
    private float? _fontSize;
    private Vector4? _foregroundColor;
    private Vector4? _backgroundColor;
    private float? _borderRadius;
    private UiShadowStyle? _shadow;
    private float? _spacing;

    public string? FontFamily
    {
        get => _fontFamily;
        set
        {
            if (value is not null && string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Font family cannot be empty.", nameof(value));
            }

            _fontFamily = value;
        }
    }

    public float? FontSize
    {
        get => _fontSize;
        set
        {
            if (value is not null && (!float.IsFinite(value.Value) || value.Value <= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Font size must be finite and greater than zero.");
            }

            _fontSize = value;
        }
    }

    public Vector4? ForegroundColor
    {
        get => _foregroundColor;
        set
        {
            if (value is not null)
            {
                ValidateColor(value.Value, nameof(value));
            }

            _foregroundColor = value;
        }
    }

    public Vector4? BackgroundColor
    {
        get => _backgroundColor;
        set
        {
            if (value is not null)
            {
                ValidateColor(value.Value, nameof(value));
            }

            _backgroundColor = value;
        }
    }

    public float? BorderRadius
    {
        get => _borderRadius;
        set
        {
            if (value is not null && (!float.IsFinite(value.Value) || value.Value < 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Border radius must be finite and non-negative.");
            }

            _borderRadius = value;
        }
    }

    public UiShadowStyle? Shadow
    {
        get => _shadow;
        set => _shadow = value;
    }

    public float? Spacing
    {
        get => _spacing;
        set
        {
            if (value is not null && (!float.IsFinite(value.Value) || value.Value < 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Spacing must be finite and non-negative.");
            }

            _spacing = value;
        }
    }

    private static void ValidateColor(Vector4 color, string paramName)
    {
        if (!float.IsFinite(color.X) || !float.IsFinite(color.Y) || !float.IsFinite(color.Z) || !float.IsFinite(color.W))
        {
            throw new ArgumentOutOfRangeException(paramName, "Color components must be finite.");
        }
    }
}
