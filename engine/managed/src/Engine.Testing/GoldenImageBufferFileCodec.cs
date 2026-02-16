using System.Text;

namespace Engine.Testing;

public static class GoldenImageBufferFileCodec
{
    private const uint Magic = 0x49464644; // DFFI
    private const uint Version = 1;

    public static void Write(string outputPath, in GoldenImageBuffer image)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        string? directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using FileStream stream = File.Create(outputPath);
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: false);
        writer.Write(Magic);
        writer.Write(Version);
        writer.Write(image.Width);
        writer.Write(image.Height);
        writer.Write(image.RgbaBytes.Length);
        writer.Write(image.RgbaBytes.Span);
    }

    public static GoldenImageBuffer Read(string inputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);

        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException($"Golden image buffer file was not found: {inputPath}", inputPath);
        }

        using FileStream stream = File.OpenRead(inputPath);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);
        uint magic = reader.ReadUInt32();
        if (magic != Magic)
        {
            throw new InvalidDataException($"Invalid golden image magic 0x{magic:X8}. Expected 0x{Magic:X8}.");
        }

        uint version = reader.ReadUInt32();
        if (version != Version)
        {
            throw new InvalidDataException($"Unsupported golden image version {version}. Expected {Version}.");
        }

        int width = reader.ReadInt32();
        int height = reader.ReadInt32();
        int length = reader.ReadInt32();
        if (length <= 0)
        {
            throw new InvalidDataException("Golden image payload length must be positive.");
        }

        byte[] bytes = reader.ReadBytes(length);
        if (bytes.Length != length)
        {
            throw new InvalidDataException(
                $"Golden image payload is truncated. Expected {length} bytes, got {bytes.Length}.");
        }

        return new GoldenImageBuffer(width, height, bytes);
    }
}
