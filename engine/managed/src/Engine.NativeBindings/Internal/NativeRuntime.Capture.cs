using System;
using System.Runtime.InteropServices;
using Engine.NativeBindings.Internal.Interop;
using Engine.Rendering;

namespace Engine.NativeBindings.Internal;

internal sealed partial class NativeRuntime
{
    private const int MaxCapturePollAttempts = 32;

    public byte[] CaptureFrameRgba8(uint width, uint height, bool includeAlpha = true)
        => CaptureFrameRaw(width, height, includeAlpha, EngineNativeCaptureFormat.Rgba8Unorm, bytesPerPixel: 4);

    public byte[] CaptureFrameRgba16Float(uint width, uint height, bool includeAlpha = true)
        => CaptureFrameRaw(width, height, includeAlpha, EngineNativeCaptureFormat.Rgba16Float, bytesPerPixel: 8);

    private byte[] CaptureFrameRaw(
        uint width,
        uint height,
        bool includeAlpha,
        EngineNativeCaptureFormat requestedFormat,
        int bytesPerPixel)
    {
        if (width == 0u)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Capture width must be greater than zero.");
        }

        if (height == 0u)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Capture height must be greater than zero.");
        }

        ThrowIfDisposed();

        var request = new EngineNativeCaptureRequest
        {
            Width = width,
            Height = height,
            IncludeAlpha = includeAlpha ? (byte)1 : (byte)0,
            Reserved0 = MapCaptureSemantic(_lastSubmittedDebugViewMode),
            Reserved1 = (byte)requestedFormat,
            Reserved2 = 0
        };

        NativeStatusGuard.ThrowIfFailed(
            _interop.CaptureRequest(_renderer, in request, out var requestId),
            "capture_request");

        if (requestId == 0u)
        {
            throw new InvalidOperationException("Native capture_request returned an invalid request identifier.");
        }

        EngineNativeCaptureResult result = default;
        var hasCaptureResult = false;

        try
        {
            for (int attempt = 0; attempt < MaxCapturePollAttempts; attempt++)
            {
                NativeStatusGuard.ThrowIfFailed(
                    _interop.CapturePoll(requestId, out result, out var isReady),
                    "capture_poll");

                if (isReady != 0u)
                {
                    hasCaptureResult = true;
                    break;
                }
            }

            if (!hasCaptureResult)
            {
                throw new InvalidOperationException(
                    $"Capture request '{requestId}' is not ready after {MaxCapturePollAttempts} poll attempts.");
            }

            ValidateCaptureResult(result, bytesPerPixel);

            if (result.Format != (uint)requestedFormat)
            {
                throw new InvalidOperationException(
                    $"Unsupported capture format '{result.Format}' (requested '{requestedFormat}').");
            }

            int rawByteCount = checked((int)result.PixelBytes);
            var rawBytes = new byte[rawByteCount];
            if (rawByteCount > 0)
            {
                Marshal.Copy(result.Pixels, rawBytes, 0, rawByteCount);
            }

            return EnsureTightRows(rawBytes, result, bytesPerPixel);
        }
        finally
        {
            if (hasCaptureResult)
            {
                NativeStatusGuard.ThrowIfFailed(
                    _interop.CaptureFreeResult(ref result),
                    "capture_free_result");
            }
        }
    }

    private static void ValidateCaptureResult(EngineNativeCaptureResult result, int bytesPerPixel)
    {
        if (bytesPerPixel <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bytesPerPixel), "Bytes per pixel must be positive.");
        }

        if (result.Width == 0u || result.Height == 0u)
        {
            throw new InvalidOperationException(
                $"Native capture returned invalid dimensions {result.Width}x{result.Height}.");
        }

        var minStride = checked(result.Width * (uint)bytesPerPixel);
        if (result.Stride < minStride)
        {
            throw new InvalidOperationException(
                $"Native capture returned stride {result.Stride}, expected at least {minStride}.");
        }

        nuint expectedByteCount = checked((nuint)result.Stride * result.Height);
        if (result.PixelBytes != expectedByteCount)
        {
            throw new InvalidOperationException(
                $"Native capture returned {result.PixelBytes} bytes, expected {expectedByteCount}.");
        }

        bool hasPixels = result.Pixels != IntPtr.Zero;
        bool hasBytes = result.PixelBytes > 0u;
        if (hasPixels != hasBytes)
        {
            throw new InvalidOperationException(
                "Native capture returned inconsistent pixel pointer and byte count.");
        }
    }

    private static byte[] EnsureTightRows(byte[] rawBytes, EngineNativeCaptureResult result, int bytesPerPixel)
    {
        int tightStride = checked((int)result.Width * bytesPerPixel);
        int sourceStride = checked((int)result.Stride);
        if (sourceStride == tightStride)
        {
            return rawBytes;
        }

        int rowCount = checked((int)result.Height);
        var compact = new byte[checked(tightStride * rowCount)];
        for (int row = 0; row < rowCount; row++)
        {
            Buffer.BlockCopy(rawBytes, row * sourceStride, compact, row * tightStride, tightStride);
        }

        return compact;
    }

    private static byte MapCaptureSemantic(RenderDebugViewMode debugViewMode)
    {
        return debugViewMode switch
        {
            RenderDebugViewMode.Depth => (byte)EngineNativeCaptureSemantic.Depth,
            RenderDebugViewMode.Normals => (byte)EngineNativeCaptureSemantic.Normals,
            RenderDebugViewMode.Albedo => (byte)EngineNativeCaptureSemantic.Albedo,
            RenderDebugViewMode.Roughness => (byte)EngineNativeCaptureSemantic.Roughness,
            RenderDebugViewMode.AmbientOcclusion => (byte)EngineNativeCaptureSemantic.AmbientOcclusion,
            _ => (byte)EngineNativeCaptureSemantic.Color
        };
    }
}
