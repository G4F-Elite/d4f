using System;
using Engine.Core.Handles;

namespace Engine.UI;

public sealed class UiScrollView : UiElement
{
    private TextureHandle _backgroundTexture;
    private float _scrollOffsetY;
    private float _contentHeight;

    public UiScrollView(string id, TextureHandle backgroundTexture)
        : base(id)
    {
        BackgroundTexture = backgroundTexture;
    }

    public TextureHandle BackgroundTexture
    {
        get => _backgroundTexture;
        set
        {
            if (!value.IsValid)
            {
                throw new ArgumentException("Background texture handle must be valid.", nameof(value));
            }

            _backgroundTexture = value;
        }
    }

    public float ScrollOffsetY => _scrollOffsetY;

    public float ContentHeight => _contentHeight;

    public float ScrollStep { get; set; } = 24f;

    internal void ScrollBy(float wheelDelta)
    {
        if (!float.IsFinite(wheelDelta))
        {
            throw new ArgumentOutOfRangeException(nameof(wheelDelta), "Scroll delta must be finite.");
        }

        if (ScrollStep <= 0f)
        {
            throw new InvalidDataException("Scroll step must be greater than zero.");
        }

        float next = _scrollOffsetY - wheelDelta * ScrollStep;
        _scrollOffsetY = Math.Clamp(next, 0f, MaxScrollOffsetY());
    }

    internal void UpdateMeasuredContentHeight(float measuredContentHeight)
    {
        if (!float.IsFinite(measuredContentHeight) || measuredContentHeight < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(measuredContentHeight), "Measured content height must be finite and non-negative.");
        }

        _contentHeight = measuredContentHeight;
        _scrollOffsetY = Math.Clamp(_scrollOffsetY, 0f, MaxScrollOffsetY());
    }

    private float MaxScrollOffsetY()
    {
        return Math.Max(0f, _contentHeight - LayoutBounds.Height);
    }
}
