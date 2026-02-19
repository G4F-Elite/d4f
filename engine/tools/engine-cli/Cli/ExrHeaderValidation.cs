using System.Text;

namespace Engine.Cli;

internal static class ExrHeaderValidation
{
    private const uint ExrMagic = 20000630u;
    private const uint ExrBaseVersion = 2u;

    public static ExrHeaderInfo ValidateRgba16Float(byte[] payload, string context)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(context);

        ExrHeaderInfo header = ReadHeader(payload, context);
        if (header.Compression != 0)
        {
            throw new InvalidDataException($"{context} has unsupported compression {header.Compression}; expected 0 (none).");
        }

        if (header.LineOrder != 0)
        {
            throw new InvalidDataException($"{context} has unsupported lineOrder {header.LineOrder}; expected 0 (increasing Y).");
        }

        string[] expectedChannels = ["R", "G", "B", "A"];
        if (header.Channels.Count != expectedChannels.Length || !header.Channels.SequenceEqual(expectedChannels, StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                $"{context} has invalid channels [{string.Join(", ", header.Channels)}]; expected [R, G, B, A].");
        }

        if (header.Width <= 0 || header.Height <= 0)
        {
            throw new InvalidDataException($"{context} has invalid non-positive dimensions {header.Width}x{header.Height}.");
        }

        return header;
    }

    private static ExrHeaderInfo ReadHeader(byte[] payload, string context)
    {
        if (payload.Length < 9)
        {
            throw new InvalidDataException($"{context} payload is too small.");
        }

        int offset = 0;
        uint magic = ReadUInt32(payload, ref offset, context, "magic");
        if (magic != ExrMagic)
        {
            throw new InvalidDataException($"{context} has invalid magic value {magic}.");
        }

        uint version = ReadUInt32(payload, ref offset, context, "version");
        uint baseVersion = version & 0xFFu;
        if (baseVersion != ExrBaseVersion)
        {
            throw new InvalidDataException($"{context} has unsupported base version {baseVersion}.");
        }

        var channels = new List<string>();
        int width = -1;
        int height = -1;
        byte? compression = null;
        byte? lineOrder = null;

        while (offset < payload.Length)
        {
            string attributeName = ReadNullTerminatedAscii(payload, ref offset, context, "attribute name");
            if (attributeName.Length == 0)
            {
                break;
            }

            string attributeType = ReadNullTerminatedAscii(payload, ref offset, context, $"attribute type for '{attributeName}'");
            int valueSize = ReadInt32(payload, ref offset, context, $"attribute size for '{attributeName}'");
            if (valueSize < 0 || offset + valueSize > payload.Length)
            {
                throw new InvalidDataException($"{context} has invalid size {valueSize} for attribute '{attributeName}'.");
            }

            if (attributeName == "channels" && attributeType == "chlist")
            {
                ParseChannels(payload, offset, valueSize, context, channels);
            }
            else if (attributeName == "compression" && attributeType == "compression")
            {
                if (valueSize < 1)
                {
                    throw new InvalidDataException($"{context} has invalid compression attribute size {valueSize}.");
                }

                compression = payload[offset];
            }
            else if (attributeName == "dataWindow" && attributeType == "box2i")
            {
                if (valueSize < 16)
                {
                    throw new InvalidDataException($"{context} has invalid dataWindow attribute size {valueSize}.");
                }

                int local = offset;
                int minX = ReadInt32(payload, ref local, context, "dataWindow.minX");
                int minY = ReadInt32(payload, ref local, context, "dataWindow.minY");
                int maxX = ReadInt32(payload, ref local, context, "dataWindow.maxX");
                int maxY = ReadInt32(payload, ref local, context, "dataWindow.maxY");
                if (maxX < minX || maxY < minY)
                {
                    throw new InvalidDataException($"{context} has invalid dataWindow bounds.");
                }

                width = checked(maxX - minX + 1);
                height = checked(maxY - minY + 1);
            }
            else if (attributeName == "lineOrder" && attributeType == "lineOrder")
            {
                if (valueSize < 1)
                {
                    throw new InvalidDataException($"{context} has invalid lineOrder attribute size {valueSize}.");
                }

                lineOrder = payload[offset];
            }

            offset += valueSize;
        }

        if (width <= 0 || height <= 0)
        {
            throw new InvalidDataException($"{context} is missing valid dataWindow dimensions.");
        }

        if (!compression.HasValue)
        {
            throw new InvalidDataException($"{context} is missing compression attribute.");
        }

        if (!lineOrder.HasValue)
        {
            throw new InvalidDataException($"{context} is missing lineOrder attribute.");
        }

        return new ExrHeaderInfo(width, height, compression.Value, lineOrder.Value, channels);
    }

    private static void ParseChannels(byte[] payload, int start, int valueSize, string context, List<string> channels)
    {
        int local = start;
        int limit = start + valueSize;
        while (local < limit)
        {
            string channelName = ReadNullTerminatedAscii(payload, ref local, context, "channel name");
            if (channelName.Length == 0)
            {
                break;
            }

            channels.Add(channelName);
            if (local + 16 > limit)
            {
                throw new InvalidDataException($"{context} has truncated channel entry '{channelName}'.");
            }

            local += 16;
        }
    }

    private static uint ReadUInt32(byte[] payload, ref int offset, string context, string field)
    {
        if (offset + 4 > payload.Length)
        {
            throw new InvalidDataException($"{context} is truncated while reading {field}.");
        }

        uint value = BitConverter.ToUInt32(payload, offset);
        offset += 4;
        return value;
    }

    private static int ReadInt32(byte[] payload, ref int offset, string context, string field)
    {
        if (offset + 4 > payload.Length)
        {
            throw new InvalidDataException($"{context} is truncated while reading {field}.");
        }

        int value = BitConverter.ToInt32(payload, offset);
        offset += 4;
        return value;
    }

    private static string ReadNullTerminatedAscii(byte[] payload, ref int offset, string context, string field)
    {
        int start = offset;
        while (offset < payload.Length && payload[offset] != 0)
        {
            offset++;
        }

        if (offset >= payload.Length)
        {
            throw new InvalidDataException($"{context} has unterminated ASCII string while reading {field}.");
        }

        string value = Encoding.ASCII.GetString(payload, start, offset - start);
        offset++;
        return value;
    }
}

internal readonly record struct ExrHeaderInfo(
    int Width,
    int Height,
    byte Compression,
    byte LineOrder,
    IReadOnlyList<string> Channels);
