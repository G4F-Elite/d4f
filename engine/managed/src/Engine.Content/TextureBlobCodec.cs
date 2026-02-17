using System.Text;

namespace Engine.Content;

public enum TextureBlobFormat : uint
{
    Rgba8Unorm = 1u,
    Bc5Unorm = 2u,
    Bc7Unorm = 3u,
    SourcePng = 100u,
    SourceJpeg = 101u,
    SourceBinary = 255u
}

public enum TextureBlobColorSpace : uint
{
    Linear = 0u,
    Srgb = 1u
}

public sealed record TextureBlobMip(
    int Width,
    int Height,
    int RowPitchBytes,
    byte[] Data)
{
    public TextureBlobMip Validate(TextureBlobFormat format)
    {
        if (Width <= 0 || Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Width), "Texture mip dimensions must be greater than zero.");
        }

        ArgumentNullException.ThrowIfNull(Data);
        if (Data.Length == 0)
        {
            throw new InvalidDataException("Texture mip payload cannot be empty.");
        }

        if (format == TextureBlobFormat.Rgba8Unorm)
        {
            int expectedRowPitch = checked(Width * 4);
            if (RowPitchBytes != expectedRowPitch)
            {
                throw new InvalidDataException(
                    $"Texture mip row pitch must be {expectedRowPitch} for RGBA8, actual: {RowPitchBytes}.");
            }

            int expectedLength = checked(RowPitchBytes * Height);
            if (Data.Length != expectedLength)
            {
                throw new InvalidDataException(
                    $"Texture mip payload size {Data.Length} does not match expected RGBA8 size {expectedLength}.");
            }

            return this;
        }

        if (format is TextureBlobFormat.SourcePng or TextureBlobFormat.SourceJpeg or TextureBlobFormat.SourceBinary)
        {
            if (RowPitchBytes != 0)
            {
                throw new InvalidDataException("Source-encoded texture mip must have zero row pitch.");
            }

            return this;
        }

        if (RowPitchBytes < 0)
        {
            throw new InvalidDataException("Texture mip row pitch cannot be negative.");
        }

        return this;
    }
}

public sealed record TextureBlobData(
    TextureBlobFormat Format,
    TextureBlobColorSpace ColorSpace,
    int Width,
    int Height,
    IReadOnlyList<TextureBlobMip> MipChain)
{
    public TextureBlobData Validate()
    {
        if (!Enum.IsDefined(Format))
        {
            throw new InvalidDataException($"Unsupported texture blob format '{Format}'.");
        }

        if (!Enum.IsDefined(ColorSpace))
        {
            throw new InvalidDataException($"Unsupported texture blob color space '{ColorSpace}'.");
        }

        if (Width <= 0 || Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Width), "Texture dimensions must be greater than zero.");
        }

        ArgumentNullException.ThrowIfNull(MipChain);
        if (MipChain.Count == 0)
        {
            throw new InvalidDataException("Texture mip chain cannot be empty.");
        }

        TextureBlobMip baseMip = MipChain[0].Validate(Format);
        if (baseMip.Width != Width || baseMip.Height != Height)
        {
            throw new InvalidDataException("Base texture mip dimensions must match texture dimensions.");
        }

        if (Format is TextureBlobFormat.SourcePng or TextureBlobFormat.SourceJpeg or TextureBlobFormat.SourceBinary)
        {
            if (MipChain.Count != 1)
            {
                throw new InvalidDataException("Source-encoded textures must contain exactly one mip payload.");
            }
        }
        else
        {
            int expectedWidth = Width;
            int expectedHeight = Height;
            for (int i = 1; i < MipChain.Count; i++)
            {
                TextureBlobMip mip = MipChain[i].Validate(Format);
                expectedWidth = Math.Max(1, expectedWidth / 2);
                expectedHeight = Math.Max(1, expectedHeight / 2);
                if (mip.Width != expectedWidth || mip.Height != expectedHeight)
                {
                    throw new InvalidDataException(
                        $"Mip level {i} dimensions ({mip.Width}x{mip.Height}) do not match expected {expectedWidth}x{expectedHeight}.");
                }
            }
        }

        return this;
    }
}

public static class TextureBlobCodec
{
    public const uint Magic = 0x42544644; // DFTB
    public const uint Version = 1u;

    public static byte[] Write(TextureBlobData blobData)
    {
        ArgumentNullException.ThrowIfNull(blobData);
        blobData = blobData.Validate();

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(Magic);
            writer.Write(Version);
            writer.Write((uint)blobData.Format);
            writer.Write((uint)blobData.ColorSpace);
            writer.Write(blobData.Width);
            writer.Write(blobData.Height);
            writer.Write(blobData.MipChain.Count);

            foreach (TextureBlobMip mip in blobData.MipChain)
            {
                writer.Write(mip.Width);
                writer.Write(mip.Height);
                writer.Write(mip.RowPitchBytes);
                writer.Write(mip.Data.Length);
                writer.Write(mip.Data);
            }
        }

        return stream.ToArray();
    }

    public static TextureBlobData Read(ReadOnlySpan<byte> blob)
    {
        if (blob.IsEmpty)
        {
            throw new InvalidDataException("Texture blob payload cannot be empty.");
        }

        using var stream = new MemoryStream(blob.ToArray(), writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);

        uint magic = reader.ReadUInt32();
        if (magic != Magic)
        {
            throw new InvalidDataException($"Invalid texture blob magic 0x{magic:X8}. Expected 0x{Magic:X8}.");
        }

        uint version = reader.ReadUInt32();
        if (version != Version)
        {
            throw new InvalidDataException($"Unsupported texture blob version {version}. Expected {Version}.");
        }

        TextureBlobFormat format = checked((TextureBlobFormat)reader.ReadUInt32());
        TextureBlobColorSpace colorSpace = checked((TextureBlobColorSpace)reader.ReadUInt32());
        int width = reader.ReadInt32();
        int height = reader.ReadInt32();
        int mipCount = reader.ReadInt32();
        if (mipCount <= 0)
        {
            throw new InvalidDataException($"Texture mip count must be greater than zero, actual: {mipCount}.");
        }

        var mips = new TextureBlobMip[mipCount];
        for (int i = 0; i < mipCount; i++)
        {
            int mipWidth = reader.ReadInt32();
            int mipHeight = reader.ReadInt32();
            int rowPitch = reader.ReadInt32();
            int payloadLength = reader.ReadInt32();
            if (payloadLength <= 0)
            {
                throw new InvalidDataException($"Texture mip payload length must be greater than zero, actual: {payloadLength}.");
            }

            byte[] payload = reader.ReadBytes(payloadLength);
            if (payload.Length != payloadLength)
            {
                throw new EndOfStreamException("Unexpected end of stream while reading texture mip payload.");
            }

            mips[i] = new TextureBlobMip(mipWidth, mipHeight, rowPitch, payload);
        }

        if (stream.Position != stream.Length)
        {
            throw new InvalidDataException("Texture blob contains trailing bytes.");
        }

        return new TextureBlobData(format, colorSpace, width, height, mips).Validate();
    }
}
