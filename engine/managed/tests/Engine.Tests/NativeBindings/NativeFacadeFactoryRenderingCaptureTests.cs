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

    [Fact]
    public void NativeRenderingFacadeAdvancedCapture_PropagatesRoughnessSemanticForRgba16Float()
    {
        var backend = new FakeNativeInteropApi
        {
            CaptureResultWidthToReturn = 1u,
            CaptureResultHeightToReturn = 1u,
            CaptureResultStrideToReturn = 8u,
            CaptureResultFormatToReturn = (uint)Engine.NativeBindings.Internal.Interop.EngineNativeCaptureFormat.Rgba16Float,
            CapturePixelsToReturn = [0, 0, 0, 0, 0, 0, 0, 60]
        };

        using var nativeSet = NativeFacadeFactory.CreateNativeFacadeSet(backend);
        IAdvancedCaptureRenderingFacade advanced = Assert.IsAssignableFrom<IAdvancedCaptureRenderingFacade>(nativeSet.Rendering);

        using (nativeSet.Rendering.BeginFrame(512, 16))
        {
            nativeSet.Rendering.Submit(new RenderPacket(
                0,
                Array.Empty<DrawCommand>(),
                Array.Empty<UiDrawCommand>(),
                RenderDebugViewMode.Roughness));
        }

        bool ok = advanced.TryCaptureFrameRgba16Float(1u, 1u, out byte[] rgba16, includeAlpha: true);

        Assert.True(ok);
        Assert.Equal(8, rgba16.Length);
        Assert.True(backend.LastCaptureRequest.HasValue);
        Assert.Equal((byte)Engine.NativeBindings.Internal.Interop.EngineNativeCaptureSemantic.Roughness, backend.LastCaptureRequest.Value.Reserved0);
        Assert.Equal((byte)Engine.NativeBindings.Internal.Interop.EngineNativeCaptureFormat.Rgba16Float, backend.LastCaptureRequest.Value.Reserved1);
    }
}
