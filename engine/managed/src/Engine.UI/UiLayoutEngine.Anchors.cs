namespace Engine.UI;

internal static partial class UiLayoutEngine
{
    private static float ResolveAnchoredWidth(
        UiElement element,
        float x,
        float configuredWidth,
        float? parentContentWidth)
    {
        bool isLeftAnchored = (element.Anchors & UiAnchor.Left) != 0;
        bool isRightAnchored = (element.Anchors & UiAnchor.Right) != 0;
        if (!parentContentWidth.HasValue || !isLeftAnchored || !isRightAnchored || configuredWidth > 0f)
        {
            return configuredWidth;
        }

        float stretched = parentContentWidth.Value - (x * 2f) - element.Margin.Left - element.Margin.Right;
        return Math.Max(0f, stretched);
    }

    private static float ResolveAnchoredHeight(
        UiElement element,
        float y,
        float configuredHeight,
        float? parentContentHeight)
    {
        bool isTopAnchored = (element.Anchors & UiAnchor.Top) != 0;
        bool isBottomAnchored = (element.Anchors & UiAnchor.Bottom) != 0;
        if (!parentContentHeight.HasValue || !isTopAnchored || !isBottomAnchored || configuredHeight > 0f)
        {
            return configuredHeight;
        }

        float stretched = parentContentHeight.Value - (y * 2f) - element.Margin.Top - element.Margin.Bottom;
        return Math.Max(0f, stretched);
    }

    private static float ResolveAnchoredX(UiElement element, float x, float width, float? parentContentWidth)
    {
        bool isRightAnchored = (element.Anchors & UiAnchor.Right) != 0;
        bool isLeftAnchored = (element.Anchors & UiAnchor.Left) != 0;
        if (!parentContentWidth.HasValue || !isRightAnchored || isLeftAnchored)
        {
            return x + element.Margin.Left;
        }

        return parentContentWidth.Value - width - x - element.Margin.Right;
    }

    private static float ResolveAnchoredY(UiElement element, float y, float height, float? parentContentHeight)
    {
        bool isBottomAnchored = (element.Anchors & UiAnchor.Bottom) != 0;
        bool isTopAnchored = (element.Anchors & UiAnchor.Top) != 0;
        if (!parentContentHeight.HasValue || !isBottomAnchored || isTopAnchored)
        {
            return y + element.Margin.Top;
        }

        return parentContentHeight.Value - height - y - element.Margin.Bottom;
    }
}
