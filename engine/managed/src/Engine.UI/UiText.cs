using System;
using Engine.Core.Handles;

namespace Engine.UI;

public sealed class UiText : UiElement
{
    private TextureHandle _fontTexture;
    private string _content = string.Empty;
    private UiTextWrapMode _wrapMode;
    private UiTextHorizontalAlignment _horizontalAlignment;
    private UiTextVerticalAlignment _verticalAlignment;

    public UiText(string id, TextureHandle fontTexture, string content)
        : base(id)
    {
        FontTexture = fontTexture;
        Content = content;
    }

    public TextureHandle FontTexture
    {
        get => _fontTexture;
        set
        {
            if (!value.IsValid)
            {
                throw new ArgumentException("Font texture handle must be valid.", nameof(value));
            }

            _fontTexture = value;
        }
    }

    public string Content
    {
        get => _content;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _content = value;
        }
    }

    public UiTextWrapMode WrapMode
    {
        get => _wrapMode;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new InvalidDataException($"Unsupported text wrap mode: {value}.");
            }

            _wrapMode = value;
        }
    }

    public UiTextHorizontalAlignment HorizontalAlignment
    {
        get => _horizontalAlignment;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new InvalidDataException($"Unsupported text horizontal alignment: {value}.");
            }

            _horizontalAlignment = value;
        }
    }

    public UiTextVerticalAlignment VerticalAlignment
    {
        get => _verticalAlignment;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new InvalidDataException($"Unsupported text vertical alignment: {value}.");
            }

            _verticalAlignment = value;
        }
    }
}
