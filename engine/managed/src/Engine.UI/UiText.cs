using System;
using Engine.Core.Handles;

namespace Engine.UI;

public sealed class UiText : UiElement
{
    private TextureHandle _fontTexture;
    private string _content = string.Empty;

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
}
