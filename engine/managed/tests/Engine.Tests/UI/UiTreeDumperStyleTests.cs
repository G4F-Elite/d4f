using System.Numerics;
using Engine.Core.Handles;
using Engine.UI;

namespace Engine.Tests.UI;

public sealed class UiTreeDumperStyleTests
{
    [Fact]
    public void Dump_WithResolvedStyles_IncludesMergedThemeAndOverrides()
    {
        UiResolvedStyle baseStyle = new(
            fontFamily: "BaseFont",
            fontSize: 14f,
            foregroundColor: new Vector4(0.1f, 0.2f, 0.3f, 1f),
            backgroundColor: new Vector4(0f, 0f, 0f, 0.5f),
            borderRadius: 2f,
            shadow: new UiShadowStyle(new Vector2(1f, 2f), 3f, new Vector4(0f, 0f, 0f, 0.4f)),
            spacing: 5f);
        var document = new UiDocument
        {
            Theme = new UiTheme(baseStyle)
        };
        var root = new UiPanel("root", new TextureHandle(1))
        {
            Width = 100,
            Height = 80,
            StyleOverride = new UiStyle
            {
                BackgroundColor = new Vector4(0.5f, 0.6f, 0.7f, 1f),
                Spacing = 7f
            }
        };
        var child = new UiText("child", new TextureHandle(2), "Hi")
        {
            Width = 20,
            Height = 10,
            StyleOverride = new UiStyle
            {
                FontFamily = "ChildFont",
                BorderRadius = 9f
            }
        };
        root.AddChild(child);
        document.AddRoot(root);

        string dump = new RetainedUiFacade(document).DumpTree(includeResolvedStyles: true);

        Assert.Contains("UiPanel id=\"root\"", dump);
        Assert.Contains("style={font=\"BaseFont\",size=14", dump);
        Assert.Contains("bg=(0.5", dump);
        Assert.Contains("spacing=7", dump);
        Assert.Contains("UiText id=\"child\"", dump);
        Assert.Contains("style={font=\"ChildFont\",size=14", dump);
        Assert.Contains("radius=9", dump);
        Assert.Contains("shadow=(offset=(1,2),blur=3,color=(0,0,0,", dump);
    }

    [Fact]
    public void Dump_WithoutResolvedStyles_DoesNotIncludeStyleSection()
    {
        var document = new UiDocument();
        document.AddRoot(new UiPanel("root", new TextureHandle(10))
        {
            Width = 10,
            Height = 10
        });

        string dump = UiTreeDumper.Dump(document);

        Assert.DoesNotContain("style={", dump, StringComparison.Ordinal);
    }
}
