using System.Text;

namespace Engine.Cli;

internal static class Rgba16FExrCodec
{
    private const uint ExrMagic = 20000630u;
    private const uint ExrVersion = 2u;
    private const int HalfPixelType = 1;

    public static void Write(string path, int width, int height, ReadOnlySpan<byte> rgba16fBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be greater than zero.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be greater than zero.");
        }

        int expectedLength = checked(width * height * 8);
        if (rgba16fBytes.Length != expectedLength)
        {
            throw new InvalidDataException($"RGBA16F payload length must be {expectedLength} bytes, got {rgba16fBytes.Length}.");
        }

        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        byte[] header = BuildHeader(width, height);
        int scanlinePayloadSize = checked(width * 8);
        int scanlineBlockSize = checked(8 + scanlinePayloadSize);
        long firstScanlineOffset = checked(header.Length + ((long)height * sizeof(ulong)));

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);

        writer.Write(header);
        for (int y = 0; y < height; y++)
        {
            writer.Write(checked(firstScanlineOffset + ((long)y * scanlineBlockSize)));
        }

        for (int y = 0; y < height; y++)
        {
            writer.Write(y);
            writer.Write(scanlinePayloadSize);
            WriteScanlinePlanarData(writer, rgba16fBytes, width, y);
        }
    }

    private static byte[] BuildHeader(int width, int height)
    {
        using var headerStream = new MemoryStream();
        using var writer = new BinaryWriter(headerStream, Encoding.ASCII, leaveOpen: true);

        writer.Write(ExrMagic);
        writer.Write(ExrVersion);
        WriteAttribute(writer, "channels", "chlist", BuildChannelsAttributeValue());
        WriteAttribute(writer, "compression", "compression", [0]);
        WriteAttribute(writer, "dataWindow", "box2i", BuildBox2I(0, 0, width - 1, height - 1));
        WriteAttribute(writer, "displayWindow", "box2i", BuildBox2I(0, 0, width - 1, height - 1));
        WriteAttribute(writer, "lineOrder", "lineOrder", [0]);
        WriteAttribute(writer, "pixelAspectRatio", "float", BitConverter.GetBytes(1.0f));
        WriteAttribute(writer, "screenWindowCenter", "v2f", BuildV2F(0.0f, 0.0f));
        WriteAttribute(writer, "screenWindowWidth", "float", BitConverter.GetBytes(1.0f));
        writer.Write((byte)0);

        writer.Flush();
        return headerStream.ToArray();
    }

    private static byte[] BuildChannelsAttributeValue()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);

        WriteChannel(writer, "R");
        WriteChannel(writer, "G");
        WriteChannel(writer, "B");
        WriteChannel(writer, "A");
        writer.Write((byte)0);

        writer.Flush();
        return stream.ToArray();
    }

    private static void WriteChannel(BinaryWriter writer, string channelName)
    {
        WriteNullTerminatedAscii(writer, channelName);
        writer.Write(HalfPixelType);
        writer.Write((byte)1);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write(1);
        writer.Write(1);
    }

    private static void WriteAttribute(BinaryWriter writer, string name, string type, byte[] value)
    {
        WriteNullTerminatedAscii(writer, name);
        WriteNullTerminatedAscii(writer, type);
        writer.Write(value.Length);
        writer.Write(value);
    }

    private static void WriteNullTerminatedAscii(BinaryWriter writer, string value)
    {
        writer.Write(Encoding.ASCII.GetBytes(value));
        writer.Write((byte)0);
    }

    private static byte[] BuildBox2I(int minX, int minY, int maxX, int maxY)
    {
        var value = new byte[16];
        BitConverter.GetBytes(minX).CopyTo(value, 0);
        BitConverter.GetBytes(minY).CopyTo(value, 4);
        BitConverter.GetBytes(maxX).CopyTo(value, 8);
        BitConverter.GetBytes(maxY).CopyTo(value, 12);
        return value;
    }

    private static byte[] BuildV2F(float x, float y)
    {
        var value = new byte[8];
        BitConverter.GetBytes(x).CopyTo(value, 0);
        BitConverter.GetBytes(y).CopyTo(value, 4);
        return value;
    }

    private static void WriteScanlinePlanarData(BinaryWriter writer, ReadOnlySpan<byte> rgba16fBytes, int width, int y)
    {
        int rowStart = checked(y * width * 8);

        for (int channel = 0; channel < 4; channel++)
        {
            int channelOffset = channel * 2;
            for (int x = 0; x < width; x++)
            {
                int sampleOffset = rowStart + (x * 8) + channelOffset;
                writer.Write(rgba16fBytes[sampleOffset]);
                writer.Write(rgba16fBytes[sampleOffset + 1]);
            }
        }
    }
}
