using System;
using System.Globalization;
using System.Text;

namespace Engine.UI;

public static class UiTreeDumper
{
    public static string Dump(UiDocument document)
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
            AppendElement(root, builder, depth: 0, ref wroteAnyLine);
        }

        return builder.ToString();
    }

    private static void AppendElement(UiElement element, StringBuilder builder, int depth, ref bool wroteAnyLine)
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

        switch (element)
        {
            case UiText text:
                AppendStringField(builder, "text", text.Content);
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
        }

        foreach (UiElement child in element.Children)
        {
            AppendElement(child, builder, depth + 1, ref wroteAnyLine);
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
}
