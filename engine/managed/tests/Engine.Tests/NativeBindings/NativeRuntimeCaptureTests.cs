using System;
using Engine.NativeBindings.Internal;
using Engine.NativeBindings.Internal.Interop;
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
        Assert.Equal(1, backend.CountCall("capture_poll"));
        Assert.Equal(0, backend.CountCall("capture_free_result"));
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
}
