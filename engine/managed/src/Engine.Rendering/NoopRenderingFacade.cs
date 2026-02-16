using System;

namespace Engine.Rendering;

public sealed class NoopRenderingFacade : IRenderingFacade
{
    public static NoopRenderingFacade Instance { get; } = new();

    private NoopRenderingFacade()
    {
    }

    public FrameArena BeginFrame(int requestedBytes, int alignment)
    {
        return new FrameArena(requestedBytes, alignment);
    }

    public void Submit(RenderPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
    }

    public void Present()
    {
    }

    public RenderingFrameStats GetLastFrameStats() => RenderingFrameStats.Empty;

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

        int rowStride = checked((int)width * 4);
        byte[] rgba = new byte[checked(rowStride * (int)height)];

        int widthDenominator = Math.Max(1, (int)width - 1);
        int heightDenominator = Math.Max(1, (int)height - 1);
        byte alpha = includeAlpha ? (byte)220 : (byte)255;

        for (int y = 0; y < (int)height; y++)
        {
            for (int x = 0; x < (int)width; x++)
            {
                int pixelOffset = y * rowStride + x * 4;
                byte red = (byte)(x * 255 / widthDenominator);
                byte green = (byte)(y * 255 / heightDenominator);
                byte blue = ((x / 8 + y / 8) & 1) == 0 ? (byte)192 : (byte)48;
                rgba[pixelOffset] = red;
                rgba[pixelOffset + 1] = green;
                rgba[pixelOffset + 2] = blue;
                rgba[pixelOffset + 3] = alpha;
            }
        }

        return rgba;
    }
}
