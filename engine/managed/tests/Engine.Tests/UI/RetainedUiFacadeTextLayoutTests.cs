using System;
using System.Collections.Generic;
using Engine.Core.Geometry;
using Engine.Core.Handles;
using Engine.Core.Timing;
using Engine.ECS;
using Engine.Rendering;
using Engine.UI;
using Xunit;

namespace Engine.Tests.UI;

public sealed class RetainedUiFacadeTextLayoutTests
{
    [Fact]
    public void Update_ShouldApplyWordWrapLayoutForText()
    {
        var document = new UiDocument();
        var text = new UiText("text", new TextureHandle(10), "alpha beta")
        {
            Width = 40,
            Height = 40,
            WrapMode = UiTextWrapMode.WordWrap
        };
        document.AddRoot(text);

        UiDrawCommand command = RenderSingleTextCommand(document);

        Assert.Equal(new RectF(0, 0, 40, 32), command.Bounds);
        Assert.Equal((uint)9, command.VertexCount / 4u);
    }

    [Fact]
    public void Update_ShouldApplyHorizontalAndVerticalAlignmentForText()
    {
        var document = new UiDocument();
        var text = new UiText("text", new TextureHandle(11), "ABCD")
        {
            Width = 80,
            Height = 40,
            HorizontalAlignment = UiTextHorizontalAlignment.Right,
            VerticalAlignment = UiTextVerticalAlignment.Bottom
        };
        document.AddRoot(text);

        UiDrawCommand command = RenderSingleTextCommand(document);

        Assert.Equal(new RectF(48, 24, 32, 16), command.Bounds);
    }

    [Fact]
    public void UiText_ShouldRejectUnsupportedLayoutEnumValues()
    {
        var text = new UiText("text", new TextureHandle(12), "content");

        Assert.Throws<InvalidDataException>(() => text.WrapMode = (UiTextWrapMode)777);
        Assert.Throws<InvalidDataException>(() => text.HorizontalAlignment = (UiTextHorizontalAlignment)777);
        Assert.Throws<InvalidDataException>(() => text.VerticalAlignment = (UiTextVerticalAlignment)777);
    }

    [Fact]
    public void Update_ShouldUseFontAtlasAdvanceAndLineHeight_WhenProvided()
    {
        var document = new UiDocument();
        var text = new UiText("text", new TextureHandle(13), "WWW")
        {
            Width = 100,
            Height = 50,
            HorizontalAlignment = UiTextHorizontalAlignment.Right,
            VerticalAlignment = UiTextVerticalAlignment.Bottom,
            FontAtlas = new UiFontAtlas(
                lineHeight: 20f,
                defaultAdvance: 8f,
                glyphs: new Dictionary<char, UiGlyphMetrics>
                {
                    ['W'] = new UiGlyphMetrics(12f)
                })
        };
        document.AddRoot(text);

        UiDrawCommand command = RenderSingleTextCommand(document);

        Assert.Equal(new RectF(64, 30, 36, 20), command.Bounds);
    }

    [Fact]
    public void Update_ShouldUseFontAtlasAdvanceForWordWrap()
    {
        var document = new UiDocument();
        var text = new UiText("text", new TextureHandle(14), "WW WW")
        {
            Width = 24,
            Height = 80,
            WrapMode = UiTextWrapMode.WordWrap,
            FontAtlas = new UiFontAtlas(
                lineHeight: 20f,
                defaultAdvance: 8f,
                glyphs: new Dictionary<char, UiGlyphMetrics>
                {
                    ['W'] = new UiGlyphMetrics(12f),
                    [' '] = new UiGlyphMetrics(6f)
                })
        };
        document.AddRoot(text);

        UiDrawCommand command = RenderSingleTextCommand(document);

        Assert.Equal(new RectF(0, 0, 24, 40), command.Bounds);
    }

    private static UiDrawCommand RenderSingleTextCommand(UiDocument document)
    {
        var facade = new RetainedUiFacade(document);
        var world = new World();
        facade.Update(world, new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero));
        UiRenderBatch batch = Assert.Single(world.Query<UiRenderBatch>()).Component;
        return Assert.Single(batch.Commands);
    }
}
