using Engine.Core.Geometry;

namespace Engine.UI;

public sealed partial class RetainedUiFacade
{
    private void DispatchPointerMove(float pointerX, float pointerY)
    {
        UiElement? hoveredElement = FindTopmostElement(pointerX, pointerY);
        SetHoveredElement(hoveredElement);
        hoveredElement?.InvokePointerMove(pointerX, pointerY);
    }

    private UiElement? FindTopmostElement(float pointerX, float pointerY)
    {
        for (int i = _document.Roots.Count - 1; i >= 0; i--)
        {
            UiElement root = _document.Roots[i];
            if (TryFindTopmostElement(root, pointerX, pointerY, 0.0f, out UiElement? hitElement))
            {
                return hitElement;
            }
        }

        return null;
    }

    private static bool TryFindTopmostElement(
        UiElement element,
        float pointerX,
        float pointerY,
        float inheritedScrollY,
        out UiElement? hitElement)
    {
        hitElement = null;
        if (!element.Visible)
        {
            return false;
        }

        RectF elementBounds = TranslateBounds(element.LayoutBounds, inheritedScrollY);
        bool pointerInsideElement = elementBounds.Contains(pointerX, pointerY);
        float childInheritedScrollY = inheritedScrollY;

        if (element is UiScrollView scrollView)
        {
            if (!pointerInsideElement)
            {
                return false;
            }

            childInheritedScrollY += scrollView.ScrollOffsetY;
        }

        for (int i = element.Children.Count - 1; i >= 0; i--)
        {
            if (TryFindTopmostElement(element.Children[i], pointerX, pointerY, childInheritedScrollY, out hitElement))
            {
                return true;
            }
        }

        if (!pointerInsideElement)
        {
            return false;
        }

        hitElement = element;
        return true;
    }

    private void InvalidateHoveredElementIfDetached()
    {
        if (_hoveredElement is null)
        {
            return;
        }

        if (IsElementAttached(_hoveredElement) && _hoveredElement.Visible)
        {
            return;
        }

        SetHoveredElement(null);
    }

    private void SetHoveredElement(UiElement? hoveredElement)
    {
        if (ReferenceEquals(_hoveredElement, hoveredElement))
        {
            return;
        }

        _hoveredElement?.SetHoverState(false);
        _hoveredElement = hoveredElement;
        _hoveredElement?.SetHoverState(true);
    }
}
