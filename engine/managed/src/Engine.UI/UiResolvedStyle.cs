using System.Numerics;

namespace Engine.UI;

public readonly record struct UiResolvedStyle
{
    public UiResolvedStyle(
        string fontFamily,
        float fontSize,
        Vector4 foregroundColor,
        Vector4 backgroundColor,
        float borderRadius,
        UiShadowStyle shadow,
        float spacing)
    {
        if (string.IsNullOrWhiteSpace(fontFamily))
        {
            throw new ArgumentException("Font family cannot be empty.", nameof(fontFamily));
        }

        if (!float.IsFinite(fontSize) || fontSize <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(fontSize), "Font size must be finite and greater than zero.");
        }

        ValidateColor(foregroundColor, nameof(foregroundColor));
        ValidateColor(backgroundColor, nameof(backgroundColor));

        if (!float.IsFinite(borderRadius) || borderRadius < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(borderRadius), "Border radius must be finite and non-negative.");
        }

        if (!float.IsFinite(spacing) || spacing < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(spacing), "Spacing must be finite and non-negative.");
        }

        FontFamily = fontFamily;
        FontSize = fontSize;
        ForegroundColor = foregroundColor;
        BackgroundColor = backgroundColor;
        BorderRadius = borderRadius;
        Shadow = shadow;
        Spacing = spacing;
    }

    public static UiResolvedStyle Default => new(
        fontFamily: "DffEngineDefault",
        fontSize: 16f,
        foregroundColor: new Vector4(1f, 1f, 1f, 1f),
        backgroundColor: new Vector4(0f, 0f, 0f, 0f),
        borderRadius: 0f,
        shadow: UiShadowStyle.None,
        spacing: 0f);

    public string FontFamily { get; }

    public float FontSize { get; }

    public Vector4 ForegroundColor { get; }

    public Vector4 BackgroundColor { get; }

    public float BorderRadius { get; }

    public UiShadowStyle Shadow { get; }

    public float Spacing { get; }

    private static void ValidateColor(Vector4 color, string paramName)
    {
        if (!float.IsFinite(color.X) || !float.IsFinite(color.Y) || !float.IsFinite(color.Z) || !float.IsFinite(color.W))
        {
            throw new ArgumentOutOfRangeException(paramName, "Color components must be finite.");
        }
    }
}
