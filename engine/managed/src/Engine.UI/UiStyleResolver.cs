using System.Collections.Generic;

namespace Engine.UI;

public static class UiStyleResolver
{
    public static UiResolvedStyle Resolve(UiDocument document, UiElement element)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(element);

        UiResolvedStyle resolved = document.Theme.BaseStyle;
        var stack = new Stack<UiElement>();
        for (UiElement? current = element; current is not null; current = current.Parent)
        {
            stack.Push(current);
        }

        while (stack.Count > 0)
        {
            UiElement current = stack.Pop();
            ApplyOverride(ref resolved, current.StyleOverride);
        }

        return resolved;
    }

    private static void ApplyOverride(ref UiResolvedStyle resolved, UiStyle? style)
    {
        if (style is null)
        {
            return;
        }

        string fontFamily = style.FontFamily ?? resolved.FontFamily;
        float fontSize = style.FontSize ?? resolved.FontSize;
        var foreground = style.ForegroundColor ?? resolved.ForegroundColor;
        var background = style.BackgroundColor ?? resolved.BackgroundColor;
        float borderRadius = style.BorderRadius ?? resolved.BorderRadius;
        UiShadowStyle shadow = style.Shadow ?? resolved.Shadow;
        float spacing = style.Spacing ?? resolved.Spacing;

        resolved = new UiResolvedStyle(fontFamily, fontSize, foreground, background, borderRadius, shadow, spacing);
    }
}
