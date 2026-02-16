using Engine.Rendering;

namespace Engine.Tests.Rendering;

public sealed class NoopRenderingFacadeCaptureTests
{
    [Fact]
    public void CaptureFrameRgba8_ReturnsDeterministicRgbaPayload()
    {
        byte[] first = NoopRenderingFacade.Instance.CaptureFrameRgba8(4u, 3u, includeAlpha: true);
        byte[] second = NoopRenderingFacade.Instance.CaptureFrameRgba8(4u, 3u, includeAlpha: true);

        Assert.Equal(4 * 3 * 4, first.Length);
        Assert.Equal(first, second);
        Assert.Equal((byte)220, first[3]);
    }

    [Fact]
    public void CaptureFrameRgba8_UsesOpaqueAlpha_WhenDisabled()
    {
        byte[] rgba = NoopRenderingFacade.Instance.CaptureFrameRgba8(2u, 2u, includeAlpha: false);

        Assert.Equal((byte)255, rgba[3]);
        Assert.Equal((byte)255, rgba[7]);
        Assert.Equal((byte)255, rgba[11]);
        Assert.Equal((byte)255, rgba[15]);
    }

    [Fact]
    public void CaptureFrameRgba8_Throws_WhenSizeIsInvalid()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NoopRenderingFacade.Instance.CaptureFrameRgba8(0u, 1u));
        Assert.Throws<ArgumentOutOfRangeException>(() => NoopRenderingFacade.Instance.CaptureFrameRgba8(1u, 0u));
    }
}
