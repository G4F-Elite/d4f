using System;
using Engine.Core.Handles;

namespace Engine.UI;

public sealed class UiSlider : UiElement
{
    private TextureHandle _trackTexture;
    private TextureHandle _fillTexture;
    private float _value;

    public UiSlider(string id, TextureHandle trackTexture, TextureHandle fillTexture, float value = 0f, Action<float>? onChanged = null)
        : base(id)
    {
        TrackTexture = trackTexture;
        FillTexture = fillTexture;
        Value = value;
        OnChanged = onChanged;
    }

    public TextureHandle TrackTexture
    {
        get => _trackTexture;
        set
        {
            if (!value.IsValid)
            {
                throw new ArgumentException("Track texture handle must be valid.", nameof(value));
            }

            _trackTexture = value;
        }
    }

    public TextureHandle FillTexture
    {
        get => _fillTexture;
        set
        {
            if (!value.IsValid)
            {
                throw new ArgumentException("Fill texture handle must be valid.", nameof(value));
            }

            _fillTexture = value;
        }
    }

    public float Value
    {
        get => _value;
        set => SetValue(value, invokeCallback: true);
    }

    public Action<float>? OnChanged { get; set; }

    internal void SetValueFromPointer(float pointerX)
    {
        float normalized = LayoutBounds.Width <= 0f
            ? 0f
            : (pointerX - LayoutBounds.X) / LayoutBounds.Width;
        SetValue(normalized, invokeCallback: true);
    }

    private void SetValue(float value, bool invokeCallback)
    {
        float clamped = Math.Clamp(value, 0f, 1f);
        if (Math.Abs(_value - clamped) <= 0.0001f)
        {
            return;
        }

        _value = clamped;
        if (invokeCallback)
        {
            OnChanged?.Invoke(_value);
        }
    }
}
