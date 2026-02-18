namespace Engine.UI;

public sealed partial class RetainedUiFacade
{
    private enum UiInteractionKind
    {
        ElementClick = 0,
        PointerClick = 1,
        PointerDown = 2,
        PointerUp = 3,
        PointerMove = 4,
        PointerScroll = 5,
        TextInput = 6,
        Backspace = 7
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

        public static UiQueuedInteraction CreatePointerDown(float x, float y) =>
            new(UiInteractionKind.PointerDown, string.Empty, x, y, 0.0f, string.Empty);

        public static UiQueuedInteraction CreatePointerUp(float x, float y) =>
            new(UiInteractionKind.PointerUp, string.Empty, x, y, 0.0f, string.Empty);

        public static UiQueuedInteraction CreatePointerMove(float x, float y) =>
            new(UiInteractionKind.PointerMove, string.Empty, x, y, 0.0f, string.Empty);

        public static UiQueuedInteraction CreatePointerScroll(float x, float y, float wheelDelta) =>
            new(UiInteractionKind.PointerScroll, string.Empty, x, y, wheelDelta, string.Empty);

        public static UiQueuedInteraction CreateTextInput(string text) =>
            new(UiInteractionKind.TextInput, string.Empty, 0.0f, 0.0f, 0.0f, text);

        public static UiQueuedInteraction CreateBackspace() =>
            new(UiInteractionKind.Backspace, string.Empty, 0.0f, 0.0f, 0.0f, string.Empty);
    }
}
