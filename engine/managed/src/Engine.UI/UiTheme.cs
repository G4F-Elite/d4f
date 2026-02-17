namespace Engine.UI;

public sealed record UiTheme(UiResolvedStyle BaseStyle)
{
    public static UiTheme Default { get; } = new(UiResolvedStyle.Default);
}
