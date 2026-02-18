using System;
using System.Runtime.InteropServices;
using Engine.NativeBindings.Internal.Interop;
using Engine.Rendering;

namespace Engine.NativeBindings.Internal;

internal sealed partial class NativeRuntime
{
    private const int MaxCapturePollAttempts = 32;

    public byte[] CaptureFrameRgba8(uint width, uint height, bool includeAlpha = true)
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
            Reserved1 = 0,
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

            ValidateCaptureResult(result);

            if (result.Format != (uint)EngineNativeCaptureFormat.Rgba8Unorm)
            {
                throw new InvalidOperationException(
                    $"Unsupported capture format '{result.Format}'.");
            }

            int rawByteCount = checked((int)result.PixelBytes);
            var rawBytes = new byte[rawByteCount];
            if (rawByteCount > 0)
            {
                Marshal.Copy(result.Pixels, rawBytes, 0, rawByteCount);
            }

            return EnsureTightRgbaRows(rawBytes, result);
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

    private static void ValidateCaptureResult(EngineNativeCaptureResult result)
    {
        if (result.Width == 0u || result.Height == 0u)
        {
            throw new InvalidOperationException(
                $"Native capture returned invalid dimensions {result.Width}x{result.Height}.");
        }

        var minStride = checked(result.Width * 4u);
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

    private static byte[] EnsureTightRgbaRows(byte[] rawBytes, EngineNativeCaptureResult result)
    {
        int tightStride = checked((int)result.Width * 4);
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
            RenderDebugViewMode.Depth => 1,
            RenderDebugViewMode.Normals => 2,
            RenderDebugViewMode.Albedo => 3,
            RenderDebugViewMode.Roughness => 4,
            RenderDebugViewMode.AmbientOcclusion => 5,
            _ => 0
        };
    }
}
