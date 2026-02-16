namespace Engine.Testing;

public readonly record struct GoldenImageBuffer
{
    public GoldenImageBuffer(int width, int height, ReadOnlyMemory<byte> rgbaBytes)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Image width must be greater than zero.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Image height must be greater than zero.");
        }

        int requiredBytes = checked(width * height * 4);
        if (rgbaBytes.Length != requiredBytes)
        {
            throw new ArgumentException(
                $"RGBA payload length must be exactly {requiredBytes} bytes for {width}x{height}.",
                nameof(rgbaBytes));
        }

        Width = width;
        Height = height;
        RgbaBytes = rgbaBytes;
    }

    public int Width { get; }

    public int Height { get; }

    public ReadOnlyMemory<byte> RgbaBytes { get; }
}
