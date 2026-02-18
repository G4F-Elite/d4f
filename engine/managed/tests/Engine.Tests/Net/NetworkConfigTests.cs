using Engine.Net;

namespace Engine.Tests.Net;

public sealed class NetworkConfigTests
{
    [Fact]
    public void Validate_ShouldFail_WhenAllowedRpcChannelsEmpty()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new NetworkConfig(AllowedRpcChannels: NetworkChannelMask.None).Validate());
    }

    [Fact]
    public void Validate_ShouldFail_WhenAllowedRpcChannelsContainUnsupportedFlags()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new NetworkConfig(AllowedRpcChannels: (NetworkChannelMask)128).Validate());
    }

    [Fact]
    public void IsRpcChannelAllowed_ShouldRespectConfiguredMask()
    {
        NetworkConfig config = new NetworkConfig(
            AllowedRpcChannels: NetworkChannelMask.Unreliable).Validate();

        Assert.True(config.IsRpcChannelAllowed(NetworkChannel.Unreliable));
        Assert.False(config.IsRpcChannelAllowed(NetworkChannel.ReliableOrdered));
    }
}
