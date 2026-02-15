using System;
using System.Collections.Generic;

namespace Engine.UI;

public abstract class UiElement
{
    private readonly List<UiElement> _children = [];
    private float _width;
    private float _height;

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

    public UiElement? Parent { get; private set; }

    public IReadOnlyList<UiElement> Children => _children;

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
}
