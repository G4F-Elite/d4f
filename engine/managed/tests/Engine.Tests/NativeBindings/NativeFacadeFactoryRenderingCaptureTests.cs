using Engine.NativeBindings;
using Engine.Rendering;

namespace Engine.Tests.NativeBindings;

public sealed class NativeFacadeFactoryRenderingCaptureTests
{
    [Fact]
    public void RenderingFacadeCaptureFrameRgba8_UsesNativeRenderingApi()
    {
        IRenderingFacade rendering = NativeFacadeFactory.CreateRenderingFacade();

        byte[] rgba = rendering.CaptureFrameRgba8(3u, 2u, includeAlpha: false);

        Assert.Equal(3 * 2 * 4, rgba.Length);
        Assert.Equal((byte)255, rgba[3]);
    }
}
