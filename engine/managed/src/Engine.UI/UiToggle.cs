using System;
using Engine.Core.Handles;

namespace Engine.UI;

public sealed class UiToggle : UiElement
{
    private TextureHandle _texture;
    private bool _isOn;

    public UiToggle(string id, TextureHandle texture, bool isOn = false, Action<bool>? onChanged = null)
        : base(id)
    {
        BackgroundTexture = texture;
        _isOn = isOn;
        OnChanged = onChanged;
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

    public bool IsOn
    {
        get => _isOn;
        set => SetState(value, invokeCallback: true);
    }

    public Action<bool>? OnChanged { get; set; }

    internal void Toggle()
    {
        SetState(!_isOn, invokeCallback: true);
    }

    private void SetState(bool value, bool invokeCallback)
    {
        if (_isOn == value)
        {
            return;
        }

        _isOn = value;
        if (invokeCallback)
        {
            OnChanged?.Invoke(_isOn);
        }
    }
}
