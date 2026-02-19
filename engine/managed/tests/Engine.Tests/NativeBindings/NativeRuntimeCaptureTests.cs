using System;
using Engine.NativeBindings.Internal;
using Engine.NativeBindings.Internal.Interop;
using Engine.Rendering;
using Xunit;

namespace Engine.Tests.NativeBindings;

public sealed class NativeRuntimeCaptureTests
{
    [Fact]
    public void CaptureFrameRgba8_UsesCaptureInteropAndFreesResult()
    {
        var backend = new FakeNativeInteropApi
        {
            CaptureRequestIdToReturn = 41u,
            CaptureResultWidthToReturn = 2u,
            CaptureResultHeightToReturn = 2u,
            CaptureResultStrideToReturn = 8u,
            CaptureResultFormatToReturn = (uint)EngineNativeCaptureFormat.Rgba8Unorm,
            CapturePixelsToReturn =
            [
                1, 2, 3, 4,
                5, 6, 7, 8,
                9, 10, 11, 12,
                13, 14, 15, 16
            ]
        };

        using var runtime = new NativeRuntime(backend);

        byte[] pixels = runtime.CaptureFrameRgba8(2u, 2u, includeAlpha: true);

        Assert.Equal(backend.CapturePixelsToReturn, pixels);
        Assert.True(backend.LastCaptureRequest.HasValue);
        Assert.Equal((uint)2, backend.LastCaptureRequest.Value.Width);
        Assert.Equal((uint)2, backend.LastCaptureRequest.Value.Height);
        Assert.Equal((byte)1, backend.LastCaptureRequest.Value.IncludeAlpha);
        Assert.Equal((ulong)41, backend.LastCapturePollRequestId);
        Assert.True(backend.CaptureResultFreed);
        Assert.Equal(1, backend.CountCall("capture_request"));
        Assert.Equal(1, backend.CountCall("capture_poll"));
        Assert.Equal(1, backend.CountCall("capture_free_result"));
    }

    [Fact]
    public void CaptureFrameRgba8_ThrowsWhenCaptureIsNotReady()
    {
        var backend = new FakeNativeInteropApi
        {
            CapturePollReadyToReturn = 0
        };

        using var runtime = new NativeRuntime(backend);

        var exception = Assert.Throws<InvalidOperationException>(() => runtime.CaptureFrameRgba8(1u, 1u));

        Assert.Contains("not ready", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, backend.CountCall("capture_request"));
        Assert.True(backend.CountCall("capture_poll") > 1);
        Assert.Equal(0, backend.CountCall("capture_free_result"));
    }

    [Fact]
    public void CaptureFrameRgba8_RetriesPollUntilCaptureBecomesReady()
    {
        var backend = new FakeNativeInteropApi
        {
            CapturePollsBeforeReady = 2,
            CaptureResultWidthToReturn = 1u,
            CaptureResultHeightToReturn = 1u,
            CaptureResultStrideToReturn = 4u,
            CaptureResultFormatToReturn = (uint)EngineNativeCaptureFormat.Rgba8Unorm,
            CapturePixelsToReturn = [17, 33, 65, 255]
        };

        using var runtime = new NativeRuntime(backend);

        byte[] pixels = runtime.CaptureFrameRgba8(1u, 1u);

        Assert.Equal([17, 33, 65, 255], pixels);
        Assert.Equal(3, backend.CountCall("capture_poll"));
        Assert.Equal(1, backend.CountCall("capture_free_result"));
    }

    [Fact]
    public void CaptureFrameRgba8_ThrowsWhenCapturePayloadIsInvalid_AndStillFreesNativeMemory()
    {
        var backend = new FakeNativeInteropApi
        {
            CaptureResultWidthToReturn = 2u,
            CaptureResultHeightToReturn = 2u,
            CaptureResultStrideToReturn = 8u,
            CaptureResultFormatToReturn = (uint)EngineNativeCaptureFormat.Rgba8Unorm,
            CapturePixelsToReturn = [1, 2, 3]
        };

        using var runtime = new NativeRuntime(backend);

        var exception = Assert.Throws<InvalidOperationException>(() => runtime.CaptureFrameRgba8(2u, 2u));

        Assert.Contains("expected", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, backend.CountCall("capture_request"));
        Assert.Equal(1, backend.CountCall("capture_poll"));
        Assert.Equal(1, backend.CountCall("capture_free_result"));
        Assert.True(backend.CaptureResultFreed);
    }

    [Fact]
    public void CaptureFrameRgba8_ValidatesArguments()
    {
        var backend = new FakeNativeInteropApi();
        using var runtime = new NativeRuntime(backend);

        Assert.Throws<ArgumentOutOfRangeException>(() => runtime.CaptureFrameRgba8(0u, 1u));
        Assert.Throws<ArgumentOutOfRangeException>(() => runtime.CaptureFrameRgba8(1u, 0u));
    }

    [Fact]
    public void CaptureFrameRgba8_CompactsRows_WhenNativeStrideHasPadding()
    {
        var backend = new FakeNativeInteropApi
        {
            CaptureResultWidthToReturn = 2u,
            CaptureResultHeightToReturn = 2u,
            CaptureResultStrideToReturn = 12u,
            CaptureResultFormatToReturn = (uint)EngineNativeCaptureFormat.Rgba8Unorm,
            CapturePixelsToReturn =
            [
                1, 2, 3, 4, 5, 6, 7, 8, 99, 98, 97, 96,
                9, 10, 11, 12, 13, 14, 15, 16, 95, 94, 93, 92
            ]
        };

        using var runtime = new NativeRuntime(backend);

        byte[] packed = runtime.CaptureFrameRgba8(2u, 2u);

        Assert.Equal(
        [
            1, 2, 3, 4, 5, 6, 7, 8,
            9, 10, 11, 12, 13, 14, 15, 16
        ],
            packed);
    }

    [Fact]
    public void CaptureFrameRgba16Float_UsesRequestedFormatAndRoughnessSemantic()
    {
        var backend = new FakeNativeInteropApi
        {
            CaptureResultWidthToReturn = 1u,
            CaptureResultHeightToReturn = 1u,
            CaptureResultStrideToReturn = 8u,
            CaptureResultFormatToReturn = (uint)EngineNativeCaptureFormat.Rgba16Float,
            CapturePixelsToReturn = [1, 0, 2, 0, 3, 0, 255, 59]
        };

        using var runtime = new NativeRuntime(backend);
        using var frameArena = runtime.BeginFrame(512, 16);
        runtime.Submit(new RenderPacket(
            0,
            Array.Empty<DrawCommand>(),
            Array.Empty<UiDrawCommand>(),
            RenderDebugViewMode.Roughness));

        byte[] rgba16 = runtime.CaptureFrameRgba16Float(1u, 1u, includeAlpha: false);

        Assert.Equal(backend.CapturePixelsToReturn, rgba16);
        Assert.True(backend.LastCaptureRequest.HasValue);
        Assert.Equal((byte)0, backend.LastCaptureRequest.Value.IncludeAlpha);
        Assert.Equal((byte)EngineNativeCaptureFormat.Rgba16Float, backend.LastCaptureRequest.Value.Reserved1);
        Assert.Equal((byte)EngineNativeCaptureSemantic.Roughness, backend.LastCaptureRequest.Value.Reserved0);
        Assert.Equal(1, backend.CountCall("capture_free_result"));
    }

    [Fact]
    public void CaptureFrameRgba16Float_ThrowsWhenNativeFormatMismatches_AndStillFreesNativeMemory()
    {
        var backend = new FakeNativeInteropApi
        {
            CaptureResultWidthToReturn = 1u,
            CaptureResultHeightToReturn = 1u,
            CaptureResultStrideToReturn = 8u,
            CaptureResultFormatToReturn = (uint)EngineNativeCaptureFormat.Rgba8Unorm,
            CapturePixelsToReturn = [1, 2, 3, 4, 5, 6, 7, 8]
        };

        using var runtime = new NativeRuntime(backend);

        var exception = Assert.Throws<InvalidOperationException>(() => runtime.CaptureFrameRgba16Float(1u, 1u));

        Assert.Contains("Unsupported capture format", exception.Message, StringComparison.Ordinal);
        Assert.Equal(1, backend.CountCall("capture_request"));
        Assert.Equal(1, backend.CountCall("capture_poll"));
        Assert.Equal(1, backend.CountCall("capture_free_result"));
        Assert.True(backend.CaptureResultFreed);
    }
}
