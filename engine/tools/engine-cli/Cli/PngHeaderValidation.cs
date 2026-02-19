namespace Engine.Cli;

internal static class PngHeaderValidation
{
    private static readonly byte[] Signature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];

    public static PngHeaderInfo ValidateRgba8(byte[] payload, string context)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(context);

        if (payload.Length < 24)
        {
            throw new InvalidDataException($"{context} payload is too small.");
        }

        for (int i = 0; i < Signature.Length; i++)
        {
            if (payload[i] != Signature[i])
            {
                throw new InvalidDataException($"{context} has invalid PNG signature.");
            }
        }

        if (payload[12] != (byte)'I' || payload[13] != (byte)'H' || payload[14] != (byte)'D' || payload[15] != (byte)'R')
        {
            throw new InvalidDataException($"{context} is missing IHDR chunk.");
        }

        uint width = ReadBigEndianUInt32(payload, 16);
        uint height = ReadBigEndianUInt32(payload, 20);
        if (width == 0u || height == 0u)
        {
            throw new InvalidDataException($"{context} has invalid non-positive dimensions {width}x{height}.");
        }

        return new PngHeaderInfo((int)width, (int)height);
    }

    private static uint ReadBigEndianUInt32(byte[] payload, int offset)
    {
        return ((uint)payload[offset] << 24) |
               ((uint)payload[offset + 1] << 16) |
               ((uint)payload[offset + 2] << 8) |
               payload[offset + 3];
    }
}

internal readonly record struct PngHeaderInfo(int Width, int Height);
