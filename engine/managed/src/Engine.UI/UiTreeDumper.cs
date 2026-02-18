using System;
using System.Globalization;
using System.Text;

namespace Engine.UI;

public static class UiTreeDumper
{
    public static string Dump(UiDocument document, bool includeResolvedStyles = false)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.Roots.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        bool wroteAnyLine = false;
        foreach (UiElement root in document.Roots)
        {
            AppendElement(document, root, builder, depth: 0, includeResolvedStyles, ref wroteAnyLine);
        }

        return builder.ToString();
    }

    private static void AppendElement(
        UiDocument document,
        UiElement element,
        StringBuilder builder,
        int depth,
        bool includeResolvedStyles,
        ref bool wroteAnyLine)
    {
        if (wroteAnyLine)
        {
            builder.Append('\n');
        }

        wroteAnyLine = true;

        if (depth > 0)
        {
            builder.Append(' ', depth * 2);
        }

        builder.Append(element.GetType().Name);
        builder.Append(" id=\"");
        AppendEscaped(builder, element.Id);
        builder.Append("\" visible=");
        builder.Append(element.Visible ? "true" : "false");
        builder.Append(" bounds=(");
        AppendFloat(builder, element.LayoutBounds.X);
        builder.Append(',');
        AppendFloat(builder, element.LayoutBounds.Y);
        builder.Append(',');
        AppendFloat(builder, element.LayoutBounds.Width);
        builder.Append(',');
        AppendFloat(builder, element.LayoutBounds.Height);
        builder.Append(')');
        builder.Append(" layout=");
        builder.Append(element.LayoutMode);
        if (element.Anchors != (UiAnchor.Left | UiAnchor.Top))
        {
            AppendAnchors(builder, element.Anchors);
        }

        switch (element)
        {
            case UiText text:
                AppendStringField(builder, "text", text.Content);
                break;
            case UiImage image:
                builder.Append(" texture=");
                builder.Append(image.Texture.Value.ToString(CultureInfo.InvariantCulture));
                builder.Append(" preserveAspect=");
                builder.Append(image.PreserveAspectRatio ? "true" : "false");
                break;
            case UiButton button:
                AppendStringField(builder, "text", button.Text);
                break;
            case UiToggle toggle:
                builder.Append(" isOn=");
                builder.Append(toggle.IsOn ? "true" : "false");
                break;
            case UiSlider slider:
                builder.Append(" value=");
                AppendFloat(builder, slider.Value);
                break;
            case UiInputField inputField:
                AppendStringField(builder, "text", inputField.Text);
                AppendStringField(builder, "placeholder", inputField.Placeholder);
                builder.Append(" focused=");
                builder.Append(inputField.IsFocused ? "true" : "false");
                break;
            case UiScrollView scrollView:
                builder.Append(" scrollY=");
                AppendFloat(builder, scrollView.ScrollOffsetY);
                builder.Append(" contentHeight=");
                AppendFloat(builder, scrollView.ContentHeight);
                break;
            case UiVirtualizedList list:
                (int startIndex, int visibleCount) = list.GetVisibleRange();
                builder.Append(" items=");
                builder.Append(list.Items.Count);
                builder.Append(" itemHeight=");
                AppendFloat(builder, list.ItemHeight);
                builder.Append(" scrollY=");
                AppendFloat(builder, list.ScrollOffsetY);
                builder.Append(" visibleStart=");
                builder.Append(startIndex);
                builder.Append(" visibleCount=");
                builder.Append(visibleCount);
                break;
            case UiList list:
                (int listStartIndex, int listVisibleCount) = list.GetVisibleRange();
                builder.Append(" items=");
                builder.Append(list.Items.Count);
                builder.Append(" itemHeight=");
                AppendFloat(builder, list.ItemHeight);
                builder.Append(" scrollY=");
                AppendFloat(builder, list.ScrollOffsetY);
                builder.Append(" visibleStart=");
                builder.Append(listStartIndex);
                builder.Append(" visibleCount=");
                builder.Append(listVisibleCount);
                break;
        }

        if (includeResolvedStyles)
        {
            AppendResolvedStyle(document, element, builder);
        }

        foreach (UiElement child in element.Children)
        {
            AppendElement(document, child, builder, depth + 1, includeResolvedStyles, ref wroteAnyLine);
        }
    }

    private static void AppendStringField(StringBuilder builder, string fieldName, string value)
    {
        builder.Append(' ');
        builder.Append(fieldName);
        builder.Append("=\"");
        AppendEscaped(builder, value);
        builder.Append('"');
    }

    private static void AppendEscaped(StringBuilder builder, string value)
    {
        for (int i = 0; i < value.Length; i++)
        {
            switch (value[i])
            {
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    builder.Append(value[i]);
                    break;
            }
        }
    }

    private static void AppendFloat(StringBuilder builder, float value)
    {
        float normalized = value == 0f ? 0f : value;
        builder.Append(normalized.ToString("G9", CultureInfo.InvariantCulture));
    }

    private static void AppendAnchors(StringBuilder builder, UiAnchor anchors)
    {
        builder.Append(" anchors=");
        if (anchors == UiAnchor.None)
        {
            builder.Append("None");
            return;
        }

        bool wroteAny = false;
        AppendAnchorFlag(builder, anchors, UiAnchor.Left, "Left", ref wroteAny);
        AppendAnchorFlag(builder, anchors, UiAnchor.Right, "Right", ref wroteAny);
        AppendAnchorFlag(builder, anchors, UiAnchor.Top, "Top", ref wroteAny);
        AppendAnchorFlag(builder, anchors, UiAnchor.Bottom, "Bottom", ref wroteAny);
    }

    private static void AppendAnchorFlag(
        StringBuilder builder,
        UiAnchor anchors,
        UiAnchor flag,
        string name,
        ref bool wroteAny)
    {
        if ((anchors & flag) == 0)
        {
            return;
        }

        if (wroteAny)
        {
            builder.Append('|');
        }

        builder.Append(name);
        wroteAny = true;
    }

    private static void AppendResolvedStyle(UiDocument document, UiElement element, StringBuilder builder)
    {
        UiResolvedStyle style = UiStyleResolver.Resolve(document, element);
        builder.Append(" style={font=\"");
        AppendEscaped(builder, style.FontFamily);
        builder.Append("\",size=");
        AppendFloat(builder, style.FontSize);
        builder.Append(",fg=");
        AppendColor(builder, style.ForegroundColor);
        builder.Append(",bg=");
        AppendColor(builder, style.BackgroundColor);
        builder.Append(",radius=");
        AppendFloat(builder, style.BorderRadius);
        builder.Append(",shadow=");
        AppendShadow(builder, style.Shadow);
        builder.Append(",spacing=");
        AppendFloat(builder, style.Spacing);
        builder.Append('}');
    }

    private static void AppendColor(StringBuilder builder, System.Numerics.Vector4 color)
    {
        builder.Append('(');
        AppendFloat(builder, color.X);
        builder.Append(',');
        AppendFloat(builder, color.Y);
        builder.Append(',');
        AppendFloat(builder, color.Z);
        builder.Append(',');
        AppendFloat(builder, color.W);
        builder.Append(')');
    }

    private static void AppendShadow(StringBuilder builder, UiShadowStyle shadow)
    {
        builder.Append('(');
        builder.Append("offset=");
        builder.Append('(');
        AppendFloat(builder, shadow.Offset.X);
        builder.Append(',');
        AppendFloat(builder, shadow.Offset.Y);
        builder.Append(')');
        builder.Append(",blur=");
        AppendFloat(builder, shadow.BlurRadius);
        builder.Append(",color=");
        AppendColor(builder, shadow.Color);
        builder.Append(')');
    }
}
