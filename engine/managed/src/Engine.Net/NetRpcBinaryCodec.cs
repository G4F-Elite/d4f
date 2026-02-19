using System.Text;

namespace Engine.Net;

public static class NetRpcBinaryCodec
{
    private const uint Magic = 0x4350524Eu; // NRPC
    private const uint Version = 1u;
    private const int MaxStringBytes = 1_048_576;
    private const int MaxPayloadBytes = 4 * 1_048_576;

    public static byte[] Encode(NetRpcMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(Magic);
        writer.Write(Version);
        writer.Write(message.EntityId);
        WriteString(writer, message.RpcName);
        writer.Write((byte)message.Channel);
        writer.Write(message.TargetClientId.HasValue);
        if (message.TargetClientId.HasValue)
        {
            writer.Write(message.TargetClientId.Value);
        }

        writer.Write(message.Payload.Length);
        writer.Write(message.Payload);
        writer.Flush();
        return stream.ToArray();
    }

    public static NetRpcMessage Decode(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        using var stream = new MemoryStream(data, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        uint magic = reader.ReadUInt32();
        if (magic != Magic)
        {
            throw new InvalidDataException($"RPC binary magic mismatch: expected 0x{Magic:X8}, got 0x{magic:X8}.");
        }

        uint version = reader.ReadUInt32();
        if (version != Version)
        {
            throw new InvalidDataException($"RPC binary version mismatch: expected {Version}, got {version}.");
        }

        uint entityId = reader.ReadUInt32();
        string rpcName = ReadString(reader);
        byte channelRaw = reader.ReadByte();
        if (!Enum.IsDefined(typeof(NetworkChannel), (int)channelRaw))
        {
            throw new InvalidDataException($"RPC binary contains unsupported network channel value: {channelRaw}.");
        }

        bool hasTargetClientId = reader.ReadBoolean();
        uint? targetClientId = hasTargetClientId ? reader.ReadUInt32() : null;

        int payloadLength = reader.ReadInt32();
        if (payloadLength < 0 || payloadLength > MaxPayloadBytes)
        {
            throw new InvalidDataException($"RPC payload length is out of range: {payloadLength}.");
        }

        byte[] payload = reader.ReadBytes(payloadLength);
        if (payload.Length != payloadLength)
        {
            throw new InvalidDataException("Unexpected end of data while reading RPC payload.");
        }

        if (stream.Position != stream.Length)
        {
            throw new InvalidDataException("Unexpected trailing bytes in RPC binary payload.");
        }

        return new NetRpcMessage(entityId, rpcName, payload, (NetworkChannel)channelRaw, targetClientId);
    }

    private static void WriteString(BinaryWriter writer, string value)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        byte[] bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.Length > MaxStringBytes)
        {
            throw new InvalidDataException($"String exceeds maximum UTF-8 byte length of {MaxStringBytes}.");
        }

        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static string ReadString(BinaryReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        int length = reader.ReadInt32();
        if (length < 0 || length > MaxStringBytes)
        {
            throw new InvalidDataException($"String UTF-8 byte length is out of range: {length}.");
        }

        byte[] bytes = reader.ReadBytes(length);
        if (bytes.Length != length)
        {
            throw new InvalidDataException("Unexpected end of data while reading UTF-8 string.");
        }

        return Encoding.UTF8.GetString(bytes);
    }
}
