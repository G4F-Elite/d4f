using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Engine.Testing;

namespace Engine.Cli;

internal static class RgbaPngCodec
{
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly byte[] IhdrChunkType = Encoding.ASCII.GetBytes("IHDR");
    private static readonly byte[] IdatChunkType = Encoding.ASCII.GetBytes("IDAT");
    private static readonly byte[] IendChunkType = Encoding.ASCII.GetBytes("IEND");

    public static void Write(string path, GoldenImageBuffer image)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = File.Create(path);
        stream.Write(PngSignature);
        WriteChunk(stream, IhdrChunkType, BuildIhdrPayload(image.Width, image.Height));
        WriteChunk(stream, IdatChunkType, Deflate(BuildScanlinePayload(image)));
        WriteChunk(stream, IendChunkType, []);
    }

    private static byte[] BuildIhdrPayload(int width, int height)
    {
        var payload = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(0, 4), checked((uint)width));
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(4, 4), checked((uint)height));
        payload[8] = 8; // bit depth
        payload[9] = 6; // color type: RGBA
        payload[10] = 0; // compression method
        payload[11] = 0; // filter method
        payload[12] = 0; // interlace method
        return payload;
    }

    private static byte[] BuildScanlinePayload(GoldenImageBuffer image)
    {
        int stride = checked(image.Width * 4);
        byte[] scanlines = new byte[checked((stride + 1) * image.Height)];
        ReadOnlySpan<byte> src = image.RgbaBytes.Span;

        for (int y = 0; y < image.Height; y++)
        {
            int srcOffset = y * stride;
            int dstOffset = y * (stride + 1);
            scanlines[dstOffset] = 0; // filter: none
            src.Slice(srcOffset, stride).CopyTo(scanlines.AsSpan(dstOffset + 1, stride));
        }

        return scanlines;
    }

    private static byte[] Deflate(byte[] rawData)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            zlib.Write(rawData, 0, rawData.Length);
        }

        return output.ToArray();
    }

    private static void WriteChunk(Stream output, byte[] chunkType, byte[] payload)
    {
        Span<byte> lengthBuffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(lengthBuffer, checked((uint)payload.Length));
        output.Write(lengthBuffer);
        output.Write(chunkType);
        output.Write(payload);

        uint crc = ComputeCrc(chunkType, payload);
        Span<byte> crcBuffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBuffer, crc);
        output.Write(crcBuffer);
    }

    private static uint ComputeCrc(ReadOnlySpan<byte> chunkType, ReadOnlySpan<byte> payload)
    {
        uint crc = 0xFFFFFFFFu;
        crc = UpdateCrc(crc, chunkType);
        crc = UpdateCrc(crc, payload);
        return crc ^ 0xFFFFFFFFu;
    }

    private static uint UpdateCrc(uint crc, ReadOnlySpan<byte> bytes)
    {
        foreach (byte value in bytes)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++)
            {
                bool carry = (crc & 1u) != 0u;
                crc >>= 1;
                if (carry)
                {
                    crc ^= 0xEDB88320u;
                }
            }
        }

        return crc;
    }
}
