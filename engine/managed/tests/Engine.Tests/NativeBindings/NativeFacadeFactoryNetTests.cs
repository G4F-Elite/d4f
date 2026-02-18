using System;
using Engine.NativeBindings;
using Engine.NativeBindings.Internal.Interop;
using Engine.Net;
using Xunit;

namespace Engine.Tests.NativeBindings;

public sealed class NativeFacadeFactoryNetTests
{
    [Fact]
    public void CreateNetFacade_ShouldRejectUnsupportedChannel()
    {
        INetFacade net = NativeFacadeFactory.CreateNetFacade();

        Assert.Throws<InvalidDataException>(() => net.Send(7u, (NetworkChannel)255, [1]));
    }

    [Fact]
    public void NativeFacadeSetNetSend_ShouldForwardDataToNativeInterop()
    {
        var backend = new FakeNativeInteropApi();
        using var nativeSet = NativeFacadeFactory.CreateNativeFacadeSet(backend);

        nativeSet.Net.Send(11u, NetworkChannel.ReliableOrdered, [9, 8, 7]);

        Assert.Equal(1, backend.CountCall("net_send"));
        EngineNativeNetSendDesc sendDesc = Assert.IsType<EngineNativeNetSendDesc>(backend.LastNetSendDesc);
        Assert.Equal(11u, sendDesc.PeerId);
        Assert.Equal((byte)NetworkChannel.ReliableOrdered, sendDesc.Channel);
        Assert.Equal([9, 8, 7], backend.LastNetSendPayload);
    }

    [Fact]
    public void NativeFacadeSetNetSend_ShouldThrow_WhenNativeReturnsError()
    {
        var backend = new FakeNativeInteropApi
        {
            NetSendStatus = EngineNativeStatus.InternalError
        };
        using var nativeSet = NativeFacadeFactory.CreateNativeFacadeSet(backend);

        NativeCallException exception = Assert.Throws<NativeCallException>(
            () => nativeSet.Net.Send(3u, NetworkChannel.Unreliable, [1, 2]));
        Assert.Equal("net_send", exception.Operation);
        Assert.Equal(EngineNativeStatus.InternalError, exception.Status);
    }

    [Fact]
    public void NativeFacadeSetNetPump_ShouldThrow_WhenNativeReturnsError()
    {
        var backend = new FakeNativeInteropApi
        {
            NetPumpStatus = EngineNativeStatus.InternalError
        };
        using var nativeSet = NativeFacadeFactory.CreateNativeFacadeSet(backend);

        NativeCallException exception = Assert.Throws<NativeCallException>(() => nativeSet.Net.Pump());
        Assert.Equal("net_pump", exception.Operation);
        Assert.Equal(EngineNativeStatus.InternalError, exception.Status);
    }
}
