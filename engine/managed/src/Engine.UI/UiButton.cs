using System;
using Engine.Core.Handles;

namespace Engine.UI;

public sealed class UiButton : UiElement
{
    private TextureHandle _texture;
    private string _text = string.Empty;

    public UiButton(string id, TextureHandle texture, string text, Action? onClick = null)
        : base(id)
    {
        BackgroundTexture = texture;
        Text = text;
        OnClick = onClick;
    }

    public TextureHandle BackgroundTexture
    {
        get => _texture;
        set
        {
            if (!value.IsValid)
            {
                throw new ArgumentException("Background texture handle must be valid.", nameof(value));
            }

            _texture = value;
        }
    }

    public string Text
    {
        get => _text;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _text = value;
        }
    }

    public Action? OnClick { get; set; }

    internal void InvokeClick()
    {
        OnClick?.Invoke();
    }
}
