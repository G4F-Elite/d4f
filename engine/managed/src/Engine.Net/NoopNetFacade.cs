namespace Engine.Net;

public sealed class NoopNetFacade : INetFacade
{
    private readonly Queue<NetEvent> _events = new();

    public IReadOnlyList<NetEvent> Pump()
    {
        if (_events.Count == 0)
        {
            return Array.Empty<NetEvent>();
        }

        var events = new NetEvent[_events.Count];
        for (var i = 0; i < events.Length; i++)
        {
            events[i] = _events.Dequeue();
        }

        return events;
    }

    public void Send(uint peerId, NetworkChannel channel, ReadOnlySpan<byte> payload)
    {
        if (peerId == 0u)
        {
            throw new ArgumentOutOfRangeException(nameof(peerId), "Peer id must be greater than zero.");
        }

        if (!Enum.IsDefined(channel))
        {
            throw new InvalidDataException($"Unsupported network channel value: {channel}.");
        }

        _events.Enqueue(new NetEvent(NetEventKind.Message, channel, peerId, payload.ToArray()));
    }
}
