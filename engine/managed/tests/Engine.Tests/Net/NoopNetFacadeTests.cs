using Engine.Net;
using Xunit;

namespace Engine.Tests.Net;

public sealed class NoopNetFacadeTests
{
    [Fact]
    public void Send_ShouldEnqueueMessageEvent()
    {
        var facade = new NoopNetFacade();

        facade.Send(21u, NetworkChannel.Unreliable, [4, 5, 6]);
        IReadOnlyList<NetEvent> events = facade.Pump();

        NetEvent message = Assert.Single(events);
        Assert.Equal(NetEventKind.Message, message.Kind);
        Assert.Equal(NetworkChannel.Unreliable, message.Channel);
        Assert.Equal(21u, message.PeerId);
        Assert.Equal([4, 5, 6], message.Payload);
        Assert.Empty(facade.Pump());
    }

    [Fact]
    public void Send_ShouldRejectInvalidArguments()
    {
        var facade = new NoopNetFacade();

        Assert.Throws<ArgumentOutOfRangeException>(() => facade.Send(0u, NetworkChannel.ReliableOrdered, [1]));
        Assert.Throws<InvalidDataException>(() => facade.Send(3u, (NetworkChannel)255, [1]));
    }
}
