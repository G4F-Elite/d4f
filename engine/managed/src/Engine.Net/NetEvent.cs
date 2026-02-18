namespace Engine.Net;

public enum NetEventKind : byte
{
    Connected = 1,
    Disconnected = 2,
    Message = 3
}

public sealed class NetEvent
{
    public NetEvent(NetEventKind kind, NetworkChannel channel, uint peerId, byte[] payload)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new InvalidDataException($"Unsupported net event kind value: {kind}.");
        }

        if (!Enum.IsDefined(channel))
        {
            throw new InvalidDataException($"Unsupported network channel value: {channel}.");
        }

        if (peerId == 0u)
        {
            throw new ArgumentOutOfRangeException(nameof(peerId), "Peer id must be greater than zero.");
        }

        ArgumentNullException.ThrowIfNull(payload);

        Kind = kind;
        Channel = channel;
        PeerId = peerId;
        Payload = payload.Length == 0 ? Array.Empty<byte>() : payload.ToArray();
    }

    public NetEventKind Kind { get; }

    public NetworkChannel Channel { get; }

    public uint PeerId { get; }

    public byte[] Payload { get; }
}
