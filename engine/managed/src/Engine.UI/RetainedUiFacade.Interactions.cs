using System;
using Engine.Core.Geometry;

namespace Engine.UI;

public sealed partial class RetainedUiFacade
{
    private void DispatchQueuedInteractions()
    {
        while (_queuedInteractions.Count > 0)
        {
            UiQueuedInteraction interaction = _queuedInteractions.Dequeue();
            switch (interaction.Kind)
            {
                case UiInteractionKind.ElementClick:
                    DispatchClick(interaction.ElementId);
                    break;
                case UiInteractionKind.PointerClick:
                    DispatchPointerClick(interaction.PointerX, interaction.PointerY);
                    break;
                case UiInteractionKind.PointerScroll:
                    DispatchPointerScroll(interaction.PointerX, interaction.PointerY, interaction.WheelDelta);
                    break;
                case UiInteractionKind.TextInput:
                    DispatchTextInput(interaction.Text);
                    break;
                case UiInteractionKind.Backspace:
                    DispatchBackspace();
                    break;
                default:
                    throw new InvalidOperationException($"Unknown interaction kind '{interaction.Kind}'.");
            }
        }
    }

    private void DispatchClick(string elementId)
    {
        UiInputField? clickedInput = null;
        bool handled = false;

        foreach (UiElement root in _document.Roots)
        {
            if (TryDispatchElementClick(root, elementId, out clickedInput))
            {
                handled = true;
                break;
            }
        }

        if (clickedInput is not null)
        {
            FocusInput(clickedInput);
        }
        else if (handled)
        {
            BlurFocusedInput();
        }
    }

    private void DispatchPointerClick(float pointerX, float pointerY)
    {
        UiInputField? clickedInput = null;

        for (int i = _document.Roots.Count - 1; i >= 0; i--)
        {
            UiElement root = _document.Roots[i];
            if (TryDispatchPointerClick(root, pointerX, pointerY, 0.0f, out clickedInput))
            {
                break;
            }
        }

        if (clickedInput is not null)
        {
            FocusInput(clickedInput);
            return;
        }

        BlurFocusedInput();
    }

    private void DispatchPointerScroll(float pointerX, float pointerY, float wheelDelta)
    {
        for (int i = _document.Roots.Count - 1; i >= 0; i--)
        {
            UiElement root = _document.Roots[i];
            if (TryDispatchPointerScroll(root, pointerX, pointerY, wheelDelta, 0.0f))
            {
                return;
            }
        }
    }

    private void DispatchTextInput(string text)
    {
        UiInputField? focused = GetFocusedInputIfAttached();
        focused?.AppendText(text);
    }

    private void DispatchBackspace()
    {
        UiInputField? focused = GetFocusedInputIfAttached();
        focused?.Backspace();
    }

    private bool TryDispatchElementClick(UiElement element, string elementId, out UiInputField? clickedInput)
    {
        clickedInput = null;
        if (!element.Visible)
        {
            return false;
        }

        if (element.Id == elementId)
        {
            switch (element)
            {
                case UiButton button:
                    button.InvokeClick();
                    return true;
                case UiToggle toggle:
                    toggle.Toggle();
                    return true;
                case UiInputField inputField:
                    clickedInput = inputField;
                    return true;
            }
        }

        foreach (UiElement child in element.Children)
        {
            if (TryDispatchElementClick(child, elementId, out clickedInput))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryDispatchPointerClick(
        UiElement element,
        float pointerX,
        float pointerY,
        float inheritedScrollY,
        out UiInputField? clickedInput)
    {
        clickedInput = null;
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
            if (TryDispatchPointerClick(element.Children[i], pointerX, pointerY, childInheritedScrollY, out clickedInput))
            {
                return true;
            }
        }

        if (!pointerInsideElement)
        {
            return false;
        }

        switch (element)
        {
            case UiButton button:
                button.InvokeClick();
                return true;
            case UiToggle toggle:
                toggle.Toggle();
                return true;
            case UiSlider slider:
                SetSliderValueFromPointer(slider, pointerX, elementBounds);
                return true;
            case UiInputField inputField:
                clickedInput = inputField;
                return true;
            case UiVirtualizedList list:
                if (!TryGetVirtualizedItemIndex(list, elementBounds, pointerX, pointerY, out int index))
                {
                    return false;
                }

                list.InvokeItemClick(index);
                return true;
            default:
                return false;
        }
    }

    private bool TryDispatchPointerScroll(
        UiElement element,
        float pointerX,
        float pointerY,
        float wheelDelta,
        float inheritedScrollY)
    {
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
            if (TryDispatchPointerScroll(element.Children[i], pointerX, pointerY, wheelDelta, childInheritedScrollY))
            {
                return true;
            }
        }

        if (!pointerInsideElement)
        {
            return false;
        }

        switch (element)
        {
            case UiVirtualizedList list:
                list.ScrollBy(wheelDelta);
                return true;
            case UiScrollView view:
                view.ScrollBy(wheelDelta);
                return true;
            default:
                return false;
        }
    }

    private static void SetSliderValueFromPointer(UiSlider slider, float pointerX, RectF bounds)
    {
        float normalized = bounds.Width <= 0.0f
            ? 0.0f
            : (pointerX - bounds.X) / bounds.Width;
        slider.Value = normalized;
    }

    private static bool TryGetVirtualizedItemIndex(
        UiVirtualizedList list,
        RectF listBounds,
        float pointerX,
        float pointerY,
        out int index)
    {
        index = -1;
        if (!listBounds.Contains(pointerX, pointerY) || list.Items.Count == 0)
        {
            return false;
        }

        float localY = pointerY - listBounds.Y + list.ScrollOffsetY;
        int resolved = (int)MathF.Floor(localY / list.ItemHeight);
        if (resolved < 0 || resolved >= list.Items.Count)
        {
            return false;
        }

        index = resolved;
        return true;
    }

    private void FocusInput(UiInputField inputField)
    {
        if (ReferenceEquals(_focusedInput, inputField))
        {
            return;
        }

        BlurFocusedInput();
        inputField.Focus();
        _focusedInput = inputField;
    }

    private void BlurFocusedInput()
    {
        if (_focusedInput is null)
        {
            return;
        }

        _focusedInput.Blur();
        _focusedInput = null;
    }

    private UiInputField? GetFocusedInputIfAttached()
    {
        if (_focusedInput is null)
        {
            return null;
        }

        if (!IsElementAttached(_focusedInput) || !_focusedInput.Visible)
        {
            BlurFocusedInput();
            return null;
        }

        return _focusedInput;
    }

    private bool IsElementAttached(UiElement target)
    {
        for (int i = 0; i < _document.Roots.Count; i++)
        {
            if (IsElementAttached(_document.Roots[i], target))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsElementAttached(UiElement current, UiElement target)
    {
        if (ReferenceEquals(current, target))
        {
            return true;
        }

        foreach (UiElement child in current.Children)
        {
            if (IsElementAttached(child, target))
            {
                return true;
            }
        }

        return false;
    }

    private enum UiInteractionKind
    {
        ElementClick = 0,
        PointerClick = 1,
        PointerScroll = 2,
        TextInput = 3,
        Backspace = 4
    }

    private readonly record struct UiQueuedInteraction(
        UiInteractionKind Kind,
        string ElementId,
        float PointerX,
        float PointerY,
        float WheelDelta,
        string Text)
    {
        public static UiQueuedInteraction CreateElementClick(string elementId) =>
            new(UiInteractionKind.ElementClick, elementId, 0.0f, 0.0f, 0.0f, string.Empty);

        public static UiQueuedInteraction CreatePointerClick(float x, float y) =>
            new(UiInteractionKind.PointerClick, string.Empty, x, y, 0.0f, string.Empty);

        public static UiQueuedInteraction CreatePointerScroll(float x, float y, float wheelDelta) =>
            new(UiInteractionKind.PointerScroll, string.Empty, x, y, wheelDelta, string.Empty);

        public static UiQueuedInteraction CreateTextInput(string text) =>
            new(UiInteractionKind.TextInput, string.Empty, 0.0f, 0.0f, 0.0f, text);

        public static UiQueuedInteraction CreateBackspace() =>
            new(UiInteractionKind.Backspace, string.Empty, 0.0f, 0.0f, 0.0f, string.Empty);
    }
}
