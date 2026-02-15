using System;
using Engine.Core.Handles;

namespace Engine.UI;

public sealed class UiPanel : UiElement
{
    private TextureHandle _texture;

    public UiPanel(string id, TextureHandle texture)
        : base(id)
    {
        BackgroundTexture = texture;
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
}
