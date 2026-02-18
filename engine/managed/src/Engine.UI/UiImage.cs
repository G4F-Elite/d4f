using System;
using Engine.Core.Handles;

namespace Engine.UI;

public sealed class UiImage : UiElement
{
    private TextureHandle _texture;

    public UiImage(string id, TextureHandle texture)
        : base(id)
    {
        Texture = texture;
    }

    public TextureHandle Texture
    {
        get => _texture;
        set
        {
            if (!value.IsValid)
            {
                throw new ArgumentException("Image texture handle must be valid.", nameof(value));
            }

            _texture = value;
        }
    }

    public bool PreserveAspectRatio { get; set; } = true;
}
