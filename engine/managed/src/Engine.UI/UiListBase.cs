using System;
using Engine.Core.Geometry;
using Engine.Core.Handles;

namespace Engine.UI;

public abstract class UiListBase : UiElement
{
    private TextureHandle _itemTexture;
    private TextureHandle _fontTexture;
    private IReadOnlyList<string> _items = Array.Empty<string>();
    private float _itemHeight = 20f;
    private float _scrollOffsetY;

    protected UiListBase(string id, TextureHandle itemTexture, TextureHandle fontTexture)
        : base(id)
    {
        ItemTexture = itemTexture;
        FontTexture = fontTexture;
    }

    public TextureHandle ItemTexture
    {
        get => _itemTexture;
        set
        {
            if (!value.IsValid)
            {
                throw new ArgumentException("Item texture handle must be valid.", nameof(value));
            }

            _itemTexture = value;
        }
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

    public IReadOnlyList<string> Items => _items;

    public float ItemHeight
    {
        get => _itemHeight;
        set
        {
            if (value <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Item height must be greater than zero.");
            }

            _itemHeight = value;
            _scrollOffsetY = Math.Clamp(_scrollOffsetY, 0f, MaxScrollOffsetY());
        }
    }

    public float ScrollOffsetY => _scrollOffsetY;

    public float ScrollStep { get; set; } = 24f;

    public Action<int, string>? OnItemClick { get; set; }

    public void SetItems(IReadOnlyList<string> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = items.ToArray();
        _scrollOffsetY = Math.Clamp(_scrollOffsetY, 0f, MaxScrollOffsetY());
    }

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

    internal bool TryGetItemIndexAt(RectF listBounds, float pointerX, float pointerY, out int itemIndex)
    {
        itemIndex = -1;
        if (!listBounds.Contains(pointerX, pointerY) || _items.Count == 0)
        {
            return false;
        }

        float localY = pointerY - listBounds.Y + _scrollOffsetY;
        int index = (int)MathF.Floor(localY / _itemHeight);
        if (index < 0 || index >= _items.Count)
        {
            return false;
        }

        itemIndex = index;
        return true;
    }

    internal void InvokeItemClick(int index)
    {
        if (index < 0 || index >= _items.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Item index is out of range.");
        }

        OnItemClick?.Invoke(index, _items[index]);
    }

    protected (int StartIndex, int Count) GetVisibleRangeCore()
    {
        if (_items.Count == 0 || LayoutBounds.Height <= 0f)
        {
            return (0, 0);
        }

        int start = Math.Clamp((int)MathF.Floor(_scrollOffsetY / _itemHeight), 0, _items.Count - 1);
        int capacity = Math.Max(1, (int)MathF.Ceiling(LayoutBounds.Height / _itemHeight));
        int count = Math.Min(capacity + 1, _items.Count - start);
        return (start, count);
    }

    private float MaxScrollOffsetY()
    {
        float contentHeight = _items.Count * _itemHeight;
        return Math.Max(0f, contentHeight - LayoutBounds.Height);
    }
}
