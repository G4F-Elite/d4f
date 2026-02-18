namespace Engine.Net;

public interface INetFacade
{
    IReadOnlyList<NetEvent> Pump();

    void Send(uint peerId, NetworkChannel channel, ReadOnlySpan<byte> payload);
}
