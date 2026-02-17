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
            case UiLayoutMode.Flex:
                ApplyFlexLayout(element, contentX, contentY, contentWidth, contentHeight);
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

    private static void ApplyFlexLayout(
        UiElement container,
        float contentX,
        float contentY,
        float contentWidth,
        float contentHeight)
    {
        bool isRow = container.FlexDirection == UiFlexDirection.Row;
        float contentMain = Math.Max(0.0f, isRow ? contentWidth : contentHeight);
        float contentCross = Math.Max(0.0f, isRow ? contentHeight : contentWidth);
        float gap = container.LayoutGap;
        var lines = new List<FlexLine>();
        var currentLine = new FlexLine();
        lines.Add(currentLine);

        foreach (UiElement child in container.Children)
        {
            if (!child.Visible)
            {
                ApplyElementLayout(child, contentX, contentY, null, null, null, null);
                continue;
            }

            FlexItem item = CreateFlexItem(child, isRow);
            float itemMainWithGap = currentLine.Items.Count == 0
                ? item.TotalMain
                : currentLine.MainUsed + gap + item.TotalMain;

            if (container.Wrap && currentLine.Items.Count > 0 && itemMainWithGap > contentMain)
            {
                currentLine = new FlexLine();
                lines.Add(currentLine);
            }

            if (currentLine.Items.Count > 0)
            {
                currentLine.MainUsed += gap;
            }

            currentLine.Items.Add(item);
            currentLine.MainUsed += item.TotalMain;
            currentLine.CrossExtent = Math.Max(currentLine.CrossExtent, item.TotalCross);
        }

        if (container.AlignItems == UiAlignItems.Stretch)
        {
            int lineCount = lines.Count(static line => line.Items.Count > 0);
            if (lineCount > 0)
            {
                float availableCross = Math.Max(0.0f, contentCross - gap * (lineCount - 1));
                float defaultCrossExtent = container.Wrap
                    ? availableCross / lineCount
                    : contentCross;

                foreach (FlexLine line in lines)
                {
                    if (line.Items.Count == 0)
                    {
                        continue;
                    }

                    line.CrossExtent = Math.Max(line.CrossExtent, defaultCrossExtent);
                }
            }
        }

        float crossCursor = 0.0f;
        for (int lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            FlexLine line = lines[lineIndex];
            if (line.Items.Count == 0)
            {
                continue;
            }

            ResolveMainAxisSpacing(container.JustifyContent, line, contentMain, gap, out float mainCursor, out float spacing);
            float lineCrossExtent = line.CrossExtent;
            if (container.AlignItems == UiAlignItems.Stretch && lineCrossExtent <= 0.0f)
            {
                lineCrossExtent = contentCross;
            }

            for (int itemIndex = 0; itemIndex < line.Items.Count; itemIndex++)
            {
                FlexItem item = line.Items[itemIndex];
                float crossSize = item.CrossSize;

                if (container.AlignItems == UiAlignItems.Stretch && item.IsCrossAuto)
                {
                    crossSize = Math.Max(0.0f, lineCrossExtent - item.CrossMarginStart - item.CrossMarginEnd);
                }

                float crossOffset = ResolveCrossOffset(container.AlignItems, lineCrossExtent, item, crossSize);
                float localMain = mainCursor + item.MainMarginStart;
                float localCross = crossCursor + crossOffset;

                float localX = isRow ? localMain + item.Child.X : localCross + item.Child.X;
                float localY = isRow ? localCross + item.Child.Y : localMain + item.Child.Y;
                float width = isRow ? item.MainSize : crossSize;
                float height = isRow ? crossSize : item.MainSize;
                ApplyElementLayout(item.Child, contentX, contentY, localX, localY, width, height);

                mainCursor += item.TotalMain + spacing;
            }

            crossCursor += lineCrossExtent;
            if (lineIndex < lines.Count - 1)
            {
                crossCursor += gap;
            }
        }
    }

    private static FlexItem CreateFlexItem(UiElement child, bool isRow)
    {
        float mainSize = ResolveFlexSize(
            configured: isRow ? child.Width : child.Height,
            min: isRow ? child.MinWidth : child.MinHeight,
            max: isRow ? child.MaxWidth : child.MaxHeight);
        float crossSize = ResolveFlexSize(
            configured: isRow ? child.Height : child.Width,
            min: isRow ? child.MinHeight : child.MinWidth,
            max: isRow ? child.MaxHeight : child.MaxWidth);

        float mainMarginStart = isRow ? child.Margin.Left : child.Margin.Top;
        float mainMarginEnd = isRow ? child.Margin.Right : child.Margin.Bottom;
        float crossMarginStart = isRow ? child.Margin.Top : child.Margin.Left;
        float crossMarginEnd = isRow ? child.Margin.Bottom : child.Margin.Right;
        bool isCrossAuto = isRow ? child.Height <= 0f : child.Width <= 0f;

        return new FlexItem(
            child,
            mainSize,
            crossSize,
            mainMarginStart,
            mainMarginEnd,
            crossMarginStart,
            crossMarginEnd,
            isCrossAuto);
    }

    private static float ResolveFlexSize(float configured, float min, float? max)
    {
        float candidate = configured <= 0f ? 0f : configured;
        return ClampDimension(candidate, min, max);
    }

    private static void ResolveMainAxisSpacing(
        UiJustifyContent justify,
        FlexLine line,
        float contentMain,
        float baseGap,
        out float mainCursor,
        out float spacing)
    {
        float free = Math.Max(0.0f, contentMain - line.MainUsed);
        spacing = baseGap;
        mainCursor = 0.0f;

        switch (justify)
        {
            case UiJustifyContent.Center:
                mainCursor = free * 0.5f;
                break;
            case UiJustifyContent.End:
                mainCursor = free;
                break;
            case UiJustifyContent.SpaceBetween:
                if (line.Items.Count > 1)
                {
                    spacing = baseGap + free / (line.Items.Count - 1);
                }
                break;
            case UiJustifyContent.SpaceAround:
                if (line.Items.Count > 0)
                {
                    float around = free / line.Items.Count;
                    mainCursor = around * 0.5f;
                    spacing = baseGap + around;
                }

                break;
        }
    }

    private static float ResolveCrossOffset(UiAlignItems align, float lineCrossExtent, FlexItem item, float crossSize)
    {
        float usedCross = item.CrossMarginStart + crossSize + item.CrossMarginEnd;
        float free = Math.Max(0.0f, lineCrossExtent - usedCross);

        return align switch
        {
            UiAlignItems.Center => item.CrossMarginStart + free * 0.5f,
            UiAlignItems.End => item.CrossMarginStart + free,
            _ => item.CrossMarginStart
        };
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

    private sealed class FlexLine
    {
        public List<FlexItem> Items { get; } = [];

        public float MainUsed { get; set; }

        public float CrossExtent { get; set; }
    }

    private readonly record struct FlexItem(
        UiElement Child,
        float MainSize,
        float CrossSize,
        float MainMarginStart,
        float MainMarginEnd,
        float CrossMarginStart,
        float CrossMarginEnd,
        bool IsCrossAuto)
    {
        public float TotalMain => MainMarginStart + MainSize + MainMarginEnd;

        public float TotalCross => CrossMarginStart + CrossSize + CrossMarginEnd;
    }
}
