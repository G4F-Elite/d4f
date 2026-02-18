using System;
using System.Collections.Generic;
using Engine.Core.Geometry;

namespace Engine.UI;

public abstract class UiElement
{
    private readonly List<UiElement> _children = [];
    private UiThickness _margin = UiThickness.Zero;
    private UiThickness _padding = UiThickness.Zero;
    private float _width;
    private float _height;
    private float _minWidth;
    private float _minHeight;
    private float? _maxWidth;
    private float? _maxHeight;
    private float _layoutGap;
    private UiFlexDirection _flexDirection;
    private UiJustifyContent _justifyContent;
    private UiAlignItems _alignItems;
    private UiAnchor _anchors = UiAnchor.Left | UiAnchor.Top;

    protected UiElement(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Element id cannot be empty.", nameof(id));
        }

        Id = id;
    }

    public string Id { get; }

    public bool Visible { get; set; } = true;

    public UiLayoutMode LayoutMode { get; set; } = UiLayoutMode.Absolute;

    public UiFlexDirection FlexDirection
    {
        get => _flexDirection;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported flex direction.");
            }

            _flexDirection = value;
        }
    }

    public UiJustifyContent JustifyContent
    {
        get => _justifyContent;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported justify mode.");
            }

            _justifyContent = value;
        }
    }

    public UiAlignItems AlignItems
    {
        get => _alignItems;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported align mode.");
            }

            _alignItems = value;
        }
    }

    public bool Wrap { get; set; }

    public UiAnchor Anchors
    {
        get => _anchors;
        set
        {
            const UiAnchor allowed =
                UiAnchor.Left |
                UiAnchor.Right |
                UiAnchor.Top |
                UiAnchor.Bottom;
            if ((value & ~allowed) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported anchor combination.");
            }

            _anchors = value;
        }
    }

    public float LayoutGap
    {
        get => _layoutGap;
        set
        {
            if (value < 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Layout gap cannot be negative.");
            }

            _layoutGap = value;
        }
    }

    public float X { get; set; }

    public float Y { get; set; }

    public float Width
    {
        get => _width;
        set
        {
            if (value < 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Width cannot be negative.");
            }

            _width = value;
        }
    }

    public float Height
    {
        get => _height;
        set
        {
            if (value < 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Height cannot be negative.");
            }

            _height = value;
        }
    }

    public float MinWidth
    {
        get => _minWidth;
        set
        {
            if (value < 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Minimum width cannot be negative.");
            }

            if (_maxWidth.HasValue && value > _maxWidth.Value)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Minimum width cannot exceed maximum width.");
            }

            _minWidth = value;
        }
    }

    public float MinHeight
    {
        get => _minHeight;
        set
        {
            if (value < 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Minimum height cannot be negative.");
            }

            if (_maxHeight.HasValue && value > _maxHeight.Value)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Minimum height cannot exceed maximum height.");
            }

            _minHeight = value;
        }
    }

    public float? MaxWidth
    {
        get => _maxWidth;
        set
        {
            if (value is not null && value < 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Maximum width cannot be negative.");
            }

            if (value is not null && value < _minWidth)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Maximum width cannot be smaller than minimum width.");
            }

            _maxWidth = value;
        }
    }

    public float? MaxHeight
    {
        get => _maxHeight;
        set
        {
            if (value is not null && value < 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Maximum height cannot be negative.");
            }

            if (value is not null && value < _minHeight)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Maximum height cannot be smaller than minimum height.");
            }

            _maxHeight = value;
        }
    }

    public UiThickness Margin
    {
        get => _margin;
        set => _margin = value;
    }

    public UiThickness Padding
    {
        get => _padding;
        set => _padding = value;
    }

    public UiStyle? StyleOverride { get; set; }

    public bool IsHovered { get; private set; }

    public Action? OnPointerEnter { get; set; }

    public Action? OnPointerLeave { get; set; }

    public Action<float, float>? OnPointerDown { get; set; }

    public Action<float, float>? OnPointerUp { get; set; }

    public Action<float, float>? OnPointerMove { get; set; }

    public Action<UiKey>? OnKeyDown { get; set; }

    public Action<UiKey>? OnKeyUp { get; set; }

    public RectF LayoutBounds { get; private set; } = RectF.Empty;

    public UiElement? Parent { get; private set; }

    public IReadOnlyList<UiElement> Children => _children;

    public bool ContainsPoint(float x, float y)
    {
        return Visible && LayoutBounds.Contains(x, y);
    }

    public void AddChild(UiElement child)
    {
        ArgumentNullException.ThrowIfNull(child);

        if (ReferenceEquals(child, this))
        {
            throw new InvalidOperationException("An element cannot be a child of itself.");
        }

        if (child.Parent is not null)
        {
            throw new InvalidOperationException($"Element '{child.Id}' already has a parent.");
        }

        child.Parent = this;
        _children.Add(child);
    }

    public bool RemoveChild(UiElement child)
    {
        ArgumentNullException.ThrowIfNull(child);

        if (!_children.Remove(child))
        {
            return false;
        }

        child.Parent = null;
        return true;
    }

    internal void SetLayoutBounds(RectF bounds)
    {
        LayoutBounds = bounds;
    }

    internal void SetHoverState(bool isHovered)
    {
        if (IsHovered == isHovered)
        {
            return;
        }

        IsHovered = isHovered;
        if (isHovered)
        {
            OnPointerEnter?.Invoke();
            return;
        }

        OnPointerLeave?.Invoke();
    }

    internal void InvokePointerMove(float pointerX, float pointerY)
    {
        OnPointerMove?.Invoke(pointerX, pointerY);
    }

    internal void InvokePointerDown(float pointerX, float pointerY)
    {
        OnPointerDown?.Invoke(pointerX, pointerY);
    }

    internal void InvokePointerUp(float pointerX, float pointerY)
    {
        OnPointerUp?.Invoke(pointerX, pointerY);
    }

    internal void InvokeKeyDown(UiKey key)
    {
        OnKeyDown?.Invoke(key);
    }

    internal void InvokeKeyUp(UiKey key)
    {
        OnKeyUp?.Invoke(key);
    }
}
