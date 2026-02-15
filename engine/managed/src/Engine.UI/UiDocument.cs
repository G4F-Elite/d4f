using System;
using System.Collections.Generic;

namespace Engine.UI;

public sealed class UiDocument
{
    private readonly List<UiElement> _roots = [];

    public IReadOnlyList<UiElement> Roots => _roots;

    public void AddRoot(UiElement root)
    {
        ArgumentNullException.ThrowIfNull(root);

        if (root.Parent is not null)
        {
            throw new InvalidOperationException($"Element '{root.Id}' already has a parent.");
        }

        _roots.Add(root);
    }

    public bool RemoveRoot(UiElement root)
    {
        ArgumentNullException.ThrowIfNull(root);
        return _roots.Remove(root);
    }

    public void Clear()
    {
        _roots.Clear();
    }
}
