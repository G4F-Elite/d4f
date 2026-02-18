using System;
using System.Collections.Generic;
using Engine.Core.Geometry;
using Engine.Core.Handles;
using Engine.Rendering;

namespace Engine.UI;

public sealed partial class RetainedUiFacade
{
    private IReadOnlyList<UiDrawCommand> BuildCommands()
    {
        var commands = new List<UiDrawCommand>();
        uint vertexOffset = 0u;
        uint indexOffset = 0u;

        foreach (UiElement root in _document.Roots)
        {
            CollectCommands(root, commands, ref vertexOffset, ref indexOffset, 0.0f, null);
        }

        return commands.Count == 0
            ? Array.Empty<UiDrawCommand>()
            : commands.ToArray();
    }

    private static void CollectCommands(
        UiElement element,
        List<UiDrawCommand> commands,
        ref uint vertexOffset,
        ref uint indexOffset,
        float inheritedScrollY,
        RectF? clipBounds)
    {
        if (!element.Visible)
        {
            return;
        }

        RectF elementBounds = TranslateBounds(element.LayoutBounds, inheritedScrollY);
        bool selfVisible = TryClipBounds(elementBounds, clipBounds, out RectF selfBounds);
        bool canDrawSelf = selfVisible || !clipBounds.HasValue;

        switch (element)
        {
            case UiPanel panel:
                if (canDrawSelf)
                {
                    AppendCommand(panel.BackgroundTexture, 1u, selfBounds, commands, ref vertexOffset, ref indexOffset);
                }

                break;
            case UiImage image:
                if (canDrawSelf)
                {
                    AppendCommand(image.Texture, 1u, selfBounds, commands, ref vertexOffset, ref indexOffset);
                }

                break;
            case UiButton button:
                if (canDrawSelf)
                {
                    AppendCommand(button.BackgroundTexture, 1u, selfBounds, commands, ref vertexOffset, ref indexOffset);
                }

                break;
            case UiToggle toggle:
                if (canDrawSelf)
                {
                    AppendCommand(toggle.BackgroundTexture, 1u, selfBounds, commands, ref vertexOffset, ref indexOffset);
                }

                break;
            case UiSlider slider:
                if (canDrawSelf)
                {
                    AppendCommand(slider.TrackTexture, 1u, selfBounds, commands, ref vertexOffset, ref indexOffset);
                }

                float normalized = Math.Clamp(slider.Value, 0.0f, 1.0f);
                if (normalized > 0.0f)
                {
                    var fillBounds = new RectF(elementBounds.X, elementBounds.Y, elementBounds.Width * normalized, elementBounds.Height);
                    if (TryClipBounds(fillBounds, clipBounds, out RectF clippedFillBounds) || !clipBounds.HasValue)
                    {
                        AppendCommand(slider.FillTexture, 1u, clippedFillBounds, commands, ref vertexOffset, ref indexOffset);
                    }
                }

                break;
            case UiInputField inputField:
                if (canDrawSelf)
                {
                    AppendCommand(inputField.BackgroundTexture, 1u, selfBounds, commands, ref vertexOffset, ref indexOffset);
                }

                string inputText = inputField.DisplayText;
                if (inputText.Length > 0)
                {
                    uint glyphCount = checked((uint)inputText.Length);
                    RectF rawTextBounds = InsetBounds(elementBounds, inputField.Padding);
                    if (TryClipBounds(rawTextBounds, clipBounds, out RectF textBounds) || !clipBounds.HasValue)
                    {
                        AppendCommand(inputField.FontTexture, glyphCount, textBounds, commands, ref vertexOffset, ref indexOffset);
                    }
                }

                break;
            case UiText text:
                if (text.Content.Length == 0 || !canDrawSelf)
                {
                    break;
                }

                if (text.WrapMode == UiTextWrapMode.NoWrap &&
                    text.HorizontalAlignment == UiTextHorizontalAlignment.Left &&
                    text.VerticalAlignment == UiTextVerticalAlignment.Top)
                {
                    uint textGlyphCount = checked((uint)text.Content.Length);
                    AppendCommand(text.FontTexture, textGlyphCount, selfBounds, commands, ref vertexOffset, ref indexOffset);
                    break;
                }

                var layout = ComputeTextLayout(text, selfBounds);
                if (layout.GlyphCount == 0u)
                {
                    break;
                }

                if (TryClipBounds(layout.Bounds, clipBounds, out RectF clippedTextBounds) || !clipBounds.HasValue)
                {
                    AppendCommand(
                        text.FontTexture,
                        layout.GlyphCount,
                        clippedTextBounds,
                        commands,
                        ref vertexOffset,
                        ref indexOffset);
                }

                break;
            case UiScrollView scrollView:
                if (canDrawSelf)
                {
                    AppendCommand(scrollView.BackgroundTexture, 1u, selfBounds, commands, ref vertexOffset, ref indexOffset);
                }

                break;
            case UiVirtualizedList list:
                AppendVirtualizedListCommands(list, elementBounds, clipBounds, commands, ref vertexOffset, ref indexOffset);
                break;
            case UiList uiList:
                AppendListCommands(uiList, elementBounds, clipBounds, commands, ref vertexOffset, ref indexOffset);
                break;
        }

        float childInheritedScrollY = inheritedScrollY;
        RectF? childClipBounds = clipBounds;
        if (element is UiScrollView scrollable)
        {
            childInheritedScrollY += scrollable.ScrollOffsetY;
            childClipBounds = ClipWithViewport(elementBounds, clipBounds);
        }

        foreach (UiElement child in element.Children)
        {
            CollectCommands(child, commands, ref vertexOffset, ref indexOffset, childInheritedScrollY, childClipBounds);
        }
    }

    private static void AppendVirtualizedListCommands(
        UiVirtualizedList list,
        RectF listBounds,
        RectF? clipBounds,
        List<UiDrawCommand> commands,
        ref uint vertexOffset,
        ref uint indexOffset)
    {
        (int startIndex, int count) = list.GetVisibleRange();
        AppendListItemCommands(list, listBounds, clipBounds, startIndex, count, commands, ref vertexOffset, ref indexOffset);
    }

    private static void AppendListCommands(
        UiList list,
        RectF listBounds,
        RectF? clipBounds,
        List<UiDrawCommand> commands,
        ref uint vertexOffset,
        ref uint indexOffset)
    {
        AppendListItemCommands(
            list,
            listBounds,
            clipBounds,
            startIndex: 0,
            count: list.Items.Count,
            commands,
            ref vertexOffset,
            ref indexOffset);
    }

    private static void AppendListItemCommands(
        UiListBase list,
        RectF listBounds,
        RectF? clipBounds,
        int startIndex,
        int count,
        List<UiDrawCommand> commands,
        ref uint vertexOffset,
        ref uint indexOffset)
    {
        if (count <= 0)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            int itemIndex = startIndex + i;
            float itemY = listBounds.Y + (itemIndex * list.ItemHeight) - list.ScrollOffsetY;
            var itemBounds = new RectF(listBounds.X, itemY, listBounds.Width, list.ItemHeight);
            if (!TryClipBounds(itemBounds, clipBounds, out RectF clippedItemBounds) && clipBounds.HasValue)
            {
                continue;
            }

            AppendCommand(list.ItemTexture, 1u, clippedItemBounds, commands, ref vertexOffset, ref indexOffset);

            string content = list.Items[itemIndex];
            if (content.Length == 0)
            {
                continue;
            }

            uint glyphCount = checked((uint)content.Length);
            AppendCommand(list.FontTexture, glyphCount, clippedItemBounds, commands, ref vertexOffset, ref indexOffset);
        }
    }

    private static void AppendCommand(
        TextureHandle texture,
        uint quadCount,
        RectF bounds,
        List<UiDrawCommand> commands,
        ref uint vertexOffset,
        ref uint indexOffset)
    {
        if (quadCount == 0u)
        {
            return;
        }

        uint vertexCount = checked(quadCount * 4u);
        uint indexCount = checked(quadCount * 6u);
        commands.Add(new UiDrawCommand(texture, vertexOffset, vertexCount, indexOffset, indexCount, bounds));
        vertexOffset = checked(vertexOffset + vertexCount);
        indexOffset = checked(indexOffset + indexCount);
    }

    private void UpdateScrollViewMeasurements()
    {
        foreach (UiElement root in _document.Roots)
        {
            UpdateScrollViewMeasurements(root);
        }
    }

    private static void UpdateScrollViewMeasurements(UiElement element)
    {
        if (!element.Visible)
        {
            return;
        }

        if (element is UiScrollView view)
        {
            float contentTop = view.LayoutBounds.Y + view.Padding.Top;
            float maxBottom = contentTop;

            foreach (UiElement child in view.Children)
            {
                if (!child.Visible)
                {
                    continue;
                }

                maxBottom = Math.Max(maxBottom, child.LayoutBounds.Bottom + child.Margin.Bottom);
            }

            float contentHeight = Math.Max(0.0f, maxBottom - contentTop + view.Padding.Bottom);
            view.UpdateMeasuredContentHeight(contentHeight);
        }

        foreach (UiElement child in element.Children)
        {
            UpdateScrollViewMeasurements(child);
        }
    }

    private static RectF TranslateBounds(RectF bounds, float inheritedScrollY)
    {
        if (inheritedScrollY == 0.0f)
        {
            return bounds;
        }

        return new RectF(bounds.X, bounds.Y - inheritedScrollY, bounds.Width, bounds.Height);
    }

    private static RectF InsetBounds(RectF bounds, UiThickness padding)
    {
        float x = bounds.X + padding.Left;
        float y = bounds.Y + padding.Top;
        float width = Math.Max(0.0f, bounds.Width - padding.Left - padding.Right);
        float height = Math.Max(0.0f, bounds.Height - padding.Top - padding.Bottom);
        return new RectF(x, y, width, height);
    }

    private static RectF ClipWithViewport(RectF viewport, RectF? clipBounds)
    {
        if (TryClipBounds(viewport, clipBounds, out RectF clippedViewport))
        {
            return clippedViewport;
        }

        return RectF.Empty;
    }

    private static bool TryClipBounds(RectF bounds, RectF? clipBounds, out RectF clippedBounds)
    {
        if (!clipBounds.HasValue)
        {
            clippedBounds = bounds;
            return true;
        }

        RectF clip = clipBounds.Value;
        float left = Math.Max(bounds.X, clip.X);
        float top = Math.Max(bounds.Y, clip.Y);
        float right = Math.Min(bounds.Right, clip.Right);
        float bottom = Math.Min(bounds.Bottom, clip.Bottom);
        if (right <= left || bottom <= top)
        {
            clippedBounds = RectF.Empty;
            return false;
        }

        clippedBounds = new RectF(left, top, right - left, bottom - top);
        return true;
    }

    private static TextLayoutResult ComputeTextLayout(UiText text, RectF bounds)
    {
        const float glyphWidth = 8f;
        const float lineHeight = 16f;

        if (bounds.Width <= 0f || bounds.Height <= 0f)
        {
            return new TextLayoutResult(0u, RectF.Empty);
        }

        string[] lines = BuildTextLines(text.Content, text.WrapMode, bounds.Width, glyphWidth);
        if (lines.Length == 0)
        {
            return new TextLayoutResult(0u, RectF.Empty);
        }

        int maxCharsInLine = 0;
        int glyphCount = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            maxCharsInLine = Math.Max(maxCharsInLine, line.Length);
            glyphCount += line.Length;
        }

        if (glyphCount == 0)
        {
            return new TextLayoutResult(0u, RectF.Empty);
        }

        float textWidth = Math.Min(bounds.Width, maxCharsInLine * glyphWidth);
        float textHeight = Math.Min(bounds.Height, lines.Length * lineHeight);

        float x = text.HorizontalAlignment switch
        {
            UiTextHorizontalAlignment.Left => bounds.X,
            UiTextHorizontalAlignment.Center => bounds.X + ((bounds.Width - textWidth) * 0.5f),
            UiTextHorizontalAlignment.Right => bounds.Right - textWidth,
            _ => bounds.X
        };
        float y = text.VerticalAlignment switch
        {
            UiTextVerticalAlignment.Top => bounds.Y,
            UiTextVerticalAlignment.Middle => bounds.Y + ((bounds.Height - textHeight) * 0.5f),
            UiTextVerticalAlignment.Bottom => bounds.Bottom - textHeight,
            _ => bounds.Y
        };

        x = Math.Clamp(x, bounds.X, bounds.Right - textWidth);
        y = Math.Clamp(y, bounds.Y, bounds.Bottom - textHeight);

        return new TextLayoutResult(
            checked((uint)glyphCount),
            new RectF(x, y, textWidth, textHeight));
    }

    private static string[] BuildTextLines(
        string content,
        UiTextWrapMode wrapMode,
        float availableWidth,
        float glyphWidth)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.Length == 0)
        {
            return Array.Empty<string>();
        }

        if (wrapMode == UiTextWrapMode.NoWrap)
        {
            return content.Split('\n', StringSplitOptions.None);
        }

        int maxCharsPerLine = Math.Max(1, (int)MathF.Floor(availableWidth / glyphWidth));
        var lines = new List<string>();
        string[] paragraphs = content.Split('\n', StringSplitOptions.None);
        for (int i = 0; i < paragraphs.Length; i++)
        {
            string paragraph = paragraphs[i];
            if (paragraph.Length == 0)
            {
                lines.Add(string.Empty);
                continue;
            }

            string[] words = paragraph.Split(' ', StringSplitOptions.None);
            string current = string.Empty;
            for (int wordIndex = 0; wordIndex < words.Length; wordIndex++)
            {
                string word = words[wordIndex];
                if (word.Length > maxCharsPerLine)
                {
                    if (current.Length > 0)
                    {
                        lines.Add(current);
                        current = string.Empty;
                    }

                    int offset = 0;
                    while (offset < word.Length)
                    {
                        int take = Math.Min(maxCharsPerLine, word.Length - offset);
                        lines.Add(word.Substring(offset, take));
                        offset += take;
                    }

                    continue;
                }

                string candidate = current.Length == 0 ? word : $"{current} {word}";
                if (candidate.Length <= maxCharsPerLine)
                {
                    current = candidate;
                }
                else
                {
                    lines.Add(current);
                    current = word;
                }
            }

            if (current.Length > 0)
            {
                lines.Add(current);
            }
        }

        return lines.Count == 0 ? Array.Empty<string>() : lines.ToArray();
    }

    private readonly record struct TextLayoutResult(uint GlyphCount, RectF Bounds);
}
