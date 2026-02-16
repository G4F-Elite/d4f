using System.Collections.Generic;
using Engine.Core.Geometry;

namespace Engine.UI;

internal static class UiLayoutEngine
{
    public static void Apply(UiDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        foreach (UiElement root in document.Roots)
        {
            ApplyElementLayout(root, 0.0f, 0.0f, null, null, null, null);
        }
    }

    private static void ApplyElementLayout(
        UiElement element,
        float parentContentX,
        float parentContentY,
        float? localX,
        float? localY,
        float? widthOverride,
        float? heightOverride)
    {
        if (!element.Visible)
        {
            element.SetLayoutBounds(RectF.Empty);
            return;
        }

        float effectiveX = localX ?? element.X;
        float effectiveY = localY ?? element.Y;
        float effectiveWidth = widthOverride ?? element.Width;
        float effectiveHeight = heightOverride ?? element.Height;
        float clampedWidth = ClampDimension(effectiveWidth, element.MinWidth, element.MaxWidth);
        float clampedHeight = ClampDimension(effectiveHeight, element.MinHeight, element.MaxHeight);
        float absoluteX = parentContentX + effectiveX + element.Margin.Left;
        float absoluteY = parentContentY + effectiveY + element.Margin.Top;
        var bounds = new RectF(absoluteX, absoluteY, clampedWidth, clampedHeight);
        element.SetLayoutBounds(bounds);

        float contentX = bounds.X + element.Padding.Left;
        float contentY = bounds.Y + element.Padding.Top;
        float contentWidth = Math.Max(0.0f, bounds.Width - element.Padding.Left - element.Padding.Right);
        float contentHeight = Math.Max(0.0f, bounds.Height - element.Padding.Top - element.Padding.Bottom);

        switch (element.LayoutMode)
        {
            case UiLayoutMode.VerticalStack:
                ApplyVerticalLayout(element.Children, contentX, contentY, contentWidth, contentHeight, element.LayoutGap);
                return;
            case UiLayoutMode.HorizontalStack:
                ApplyHorizontalLayout(element.Children, contentX, contentY, contentWidth, contentHeight, element.LayoutGap);
                return;
            default:
                foreach (UiElement child in element.Children)
                {
                    ApplyElementLayout(child, contentX, contentY, null, null, null, null);
                }

                return;
        }
    }

    private static void ApplyVerticalLayout(
        IReadOnlyList<UiElement> children,
        float contentX,
        float contentY,
        float contentWidth,
        float contentHeight,
        float gap)
    {
        float cursorY = contentY;
        for (int i = 0; i < children.Count; i++)
        {
            UiElement child = children[i];
            float childWidth = ResolveAutoDimension(child.Width, child.MinWidth, child.MaxWidth, contentWidth - child.Margin.Left - child.Margin.Right);
            float childHeight = ClampDimension(child.Height, child.MinHeight, child.MaxHeight);
            float childX = child.X;
            float childY = cursorY - contentY + child.Y;

            ApplyElementLayout(child, contentX, contentY, childX, childY, childWidth, childHeight);

            if (child.Visible)
            {
                cursorY = child.LayoutBounds.Bottom + child.Margin.Bottom + gap;
            }
        }
    }

    private static void ApplyHorizontalLayout(
        IReadOnlyList<UiElement> children,
        float contentX,
        float contentY,
        float contentWidth,
        float contentHeight,
        float gap)
    {
        float cursorX = contentX;
        for (int i = 0; i < children.Count; i++)
        {
            UiElement child = children[i];
            float childWidth = ClampDimension(child.Width, child.MinWidth, child.MaxWidth);
            float childHeight = ResolveAutoDimension(child.Height, child.MinHeight, child.MaxHeight, contentHeight - child.Margin.Top - child.Margin.Bottom);
            float childX = cursorX - contentX + child.X;
            float childY = child.Y;

            ApplyElementLayout(child, contentX, contentY, childX, childY, childWidth, childHeight);

            if (child.Visible)
            {
                cursorX = child.LayoutBounds.Right + child.Margin.Right + gap;
            }
        }
    }

    private static float ResolveAutoDimension(float configured, float min, float? max, float available)
    {
        if (configured > 0.0f)
        {
            return ClampDimension(configured, min, max);
        }

        return ClampDimension(Math.Max(0.0f, available), min, max);
    }

    private static float ClampDimension(float value, float min, float? max)
    {
        float clamped = Math.Max(value, min);
        if (max.HasValue)
        {
            clamped = Math.Min(clamped, max.Value);
        }

        return clamped;
    }
}
