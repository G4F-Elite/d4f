namespace Engine.UI;

public sealed partial class RetainedUiFacade
{
    private void DispatchKeyDown(UiKey key)
    {
        UiElement? target = ResolveKeyboardTarget();
        target?.InvokeKeyDown(key);
    }

    private void DispatchKeyUp(UiKey key)
    {
        UiElement? target = ResolveKeyboardTarget();
        target?.InvokeKeyUp(key);
    }

    private UiElement? ResolveKeyboardTarget()
    {
        UiInputField? focused = GetFocusedInputIfAttached();
        if (focused is not null)
        {
            return focused;
        }

        if (_hoveredElement is not null && IsElementAttached(_hoveredElement) && _hoveredElement.Visible)
        {
            return _hoveredElement;
        }

        return null;
    }
}
