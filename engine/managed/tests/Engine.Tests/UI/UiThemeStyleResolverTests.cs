using System.Numerics;
using Engine.Core.Handles;
using Engine.UI;

namespace Engine.Tests.UI;

public sealed class UiThemeStyleResolverTests
{
    [Fact]
    public void Resolve_ReturnsGlobalThemeStyle_WhenNoOverrides()
    {
        UiResolvedStyle baseStyle = new(
            fontFamily: "TestFont",
            fontSize: 22f,
            foregroundColor: new Vector4(0.9f, 0.8f, 0.7f, 1f),
            backgroundColor: new Vector4(0.1f, 0.2f, 0.3f, 1f),
            borderRadius: 4f,
            shadow: new UiShadowStyle(new Vector2(2f, 3f), 5f, new Vector4(0f, 0f, 0f, 0.5f)),
            spacing: 6f);
        var document = new UiDocument
        {
            Theme = new UiTheme(baseStyle)
        };
        var root = new UiPanel("root", new TextureHandle(1));
        document.AddRoot(root);

        UiResolvedStyle resolved = UiStyleResolver.Resolve(document, root);

        Assert.Equal(baseStyle, resolved);
    }

    [Fact]
    public void Resolve_MergesParentAndChildOverrides()
    {
        UiResolvedStyle baseStyle = new(
            fontFamily: "Global",
            fontSize: 16f,
            foregroundColor: new Vector4(0.8f, 0.8f, 0.8f, 1f),
            backgroundColor: new Vector4(0.1f, 0.1f, 0.1f, 1f),
            borderRadius: 1f,
            shadow: new UiShadowStyle(Vector2.Zero, 0f, Vector4.Zero),
            spacing: 2f);
        var document = new UiDocument
        {
            Theme = new UiTheme(baseStyle)
        };
        var parent = new UiPanel("parent", new TextureHandle(2))
        {
            StyleOverride = new UiStyle
            {
                FontSize = 20f,
                BackgroundColor = new Vector4(0.2f, 0.3f, 0.4f, 1f)
            }
        };
        var child = new UiText("child", new TextureHandle(3), "Hello")
        {
            StyleOverride = new UiStyle
            {
                ForegroundColor = new Vector4(1f, 0f, 0f, 1f),
                Spacing = 9f
            }
        };
        parent.AddChild(child);
        document.AddRoot(parent);

        UiResolvedStyle resolved = UiStyleResolver.Resolve(document, child);

        Assert.Equal("Global", resolved.FontFamily);
        Assert.Equal(20f, resolved.FontSize);
        Assert.Equal(new Vector4(1f, 0f, 0f, 1f), resolved.ForegroundColor);
        Assert.Equal(new Vector4(0.2f, 0.3f, 0.4f, 1f), resolved.BackgroundColor);
        Assert.Equal(1f, resolved.BorderRadius);
        Assert.Equal(new UiShadowStyle(Vector2.Zero, 0f, Vector4.Zero), resolved.Shadow);
        Assert.Equal(9f, resolved.Spacing);
    }

    [Fact]
    public void Document_RejectsNullTheme()
    {
        var document = new UiDocument();
        Assert.Throws<ArgumentNullException>(() => document.Theme = null!);
    }

    [Fact]
    public void StyleAndResolvedStyle_ValidateValues()
    {
        var style = new UiStyle();

        Assert.Throws<ArgumentException>(() => style.FontFamily = "   ");
        Assert.Throws<ArgumentOutOfRangeException>(() => style.FontSize = 0f);
        Assert.Throws<ArgumentOutOfRangeException>(() => style.FontSize = float.NaN);
        Assert.Throws<ArgumentOutOfRangeException>(() => style.BorderRadius = -1f);
        Assert.Throws<ArgumentOutOfRangeException>(() => style.Spacing = -0.1f);
        Assert.Throws<ArgumentOutOfRangeException>(() => style.ForegroundColor = new Vector4(float.NaN, 0f, 0f, 1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => style.BackgroundColor = new Vector4(0f, 0f, float.PositiveInfinity, 1f));

        Assert.Throws<ArgumentOutOfRangeException>(() => new UiShadowStyle(Vector2.Zero, -1f, Vector4.One));
        Assert.Throws<ArgumentOutOfRangeException>(() => new UiShadowStyle(new Vector2(float.NaN, 0f), 1f, Vector4.One));
        Assert.Throws<ArgumentOutOfRangeException>(() => new UiShadowStyle(Vector2.Zero, 1f, new Vector4(0f, 0f, 0f, float.NaN)));

        Assert.Throws<ArgumentException>(() => new UiResolvedStyle(
            fontFamily: "",
            fontSize: 10f,
            foregroundColor: Vector4.One,
            backgroundColor: Vector4.Zero,
            borderRadius: 0f,
            shadow: UiShadowStyle.None,
            spacing: 0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => new UiResolvedStyle(
            fontFamily: "X",
            fontSize: -1f,
            foregroundColor: Vector4.One,
            backgroundColor: Vector4.Zero,
            borderRadius: 0f,
            shadow: UiShadowStyle.None,
            spacing: 0f));
    }

    [Fact]
    public void Resolve_AcceptsDetachedElementWithParentChain()
    {
        var document = new UiDocument();
        var parent = new UiPanel("parent", new TextureHandle(10))
        {
            StyleOverride = new UiStyle
            {
                FontFamily = "LocalFont"
            }
        };
        var child = new UiButton("child", new TextureHandle(11), "Button");
        parent.AddChild(child);

        UiResolvedStyle resolved = UiStyleResolver.Resolve(document, child);

        Assert.Equal("LocalFont", resolved.FontFamily);
    }
}
