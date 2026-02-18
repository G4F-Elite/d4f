using Engine.Net;

namespace Engine.Tests.Net;

public sealed class InMemoryNetSessionBandwidthStatsTests
{
    [Fact]
    public void Pump_ComputesAverageBandwidthCounters_WhenTrafficExists()
    {
        InMemoryNetSession session = CreateSession();
        session.RegisterReplicatedComponent("transform");
        uint clientId = session.ConnectClient();
        session.UpsertServerEntity(
            new NetEntityState(
                entityId: 1u,
                ownerClientId: null,
                proceduralSeed: 42u,
                assetKey: "asset:1",
                components: [new NetComponentState("transform", [1, 2, 3, 4])]));
        session.QueueClientRpc(clientId, new NetRpcMessage(0u, "ping", [9, 8, 7], NetworkChannel.Unreliable));

        session.Pump();

        NetPeerStats serverStats = session.GetServerStats();
        NetPeerStats clientStats = session.GetClientStats(clientId);

        Assert.True(serverStats.BytesSent > 0);
        Assert.True(serverStats.BytesReceived > 0);
        Assert.True(serverStats.AverageSendBandwidthKbps > 0d);
        Assert.True(serverStats.AverageReceiveBandwidthKbps > 0d);
        Assert.True(serverStats.PeakSendBandwidthKbps > 0d);
        Assert.True(serverStats.PeakReceiveBandwidthKbps > 0d);
        Assert.True(serverStats.PeakSendBandwidthKbps >= serverStats.AverageSendBandwidthKbps);
        Assert.True(serverStats.PeakReceiveBandwidthKbps >= serverStats.AverageReceiveBandwidthKbps);

        Assert.True(clientStats.BytesSent > 0);
        Assert.True(clientStats.BytesReceived > 0);
        Assert.True(clientStats.AverageSendBandwidthKbps > 0d);
        Assert.True(clientStats.AverageReceiveBandwidthKbps > 0d);
        Assert.True(clientStats.PeakSendBandwidthKbps > 0d);
        Assert.True(clientStats.PeakReceiveBandwidthKbps > 0d);
        Assert.True(clientStats.PeakSendBandwidthKbps >= clientStats.AverageSendBandwidthKbps);
        Assert.True(clientStats.PeakReceiveBandwidthKbps >= clientStats.AverageReceiveBandwidthKbps);
    }

    [Fact]
    public void Pump_LeavesBandwidthAtZero_WhenNoTrafficProduced()
    {
        InMemoryNetSession session = new(new NetworkConfig(
            TickRateHz: 30,
            MaxPayloadBytes: 256,
            MaxRpcPerTickPerClient: 16,
            MaxEntitiesPerSnapshot: 128,
            SimulatedRttMs: 20.0,
            SimulatedPacketLossPercent: 0.0));

        session.Pump();

        NetPeerStats serverStats = session.GetServerStats();
        Assert.Equal(0, serverStats.BytesSent);
        Assert.Equal(0, serverStats.BytesReceived);
        Assert.Equal(0d, serverStats.AverageSendBandwidthKbps, 9);
        Assert.Equal(0d, serverStats.AverageReceiveBandwidthKbps, 9);
        Assert.Equal(0d, serverStats.PeakSendBandwidthKbps, 9);
        Assert.Equal(0d, serverStats.PeakReceiveBandwidthKbps, 9);
    }

    [Fact]
    public void Pump_ShouldRetainPeakBandwidth_WhenTrafficStops()
    {
        InMemoryNetSession session = CreateSession();
        session.RegisterReplicatedComponent("transform");
        uint clientId = session.ConnectClient();
        session.UpsertServerEntity(
            new NetEntityState(
                entityId: 1u,
                ownerClientId: null,
                proceduralSeed: 7u,
                assetKey: "asset:burst",
                components: [new NetComponentState("transform", [5, 4, 3, 2])]));

        session.Pump();
        NetPeerStats afterTraffic = session.GetServerStats();
        Assert.True(afterTraffic.AverageSendBandwidthKbps > 0d);
        Assert.True(afterTraffic.PeakSendBandwidthKbps > 0d);

        bool disconnected = session.DisconnectClient(clientId);
        Assert.True(disconnected);
        session.Pump();

        NetPeerStats afterIdle = session.GetServerStats();
        Assert.True(afterIdle.AverageSendBandwidthKbps < afterTraffic.AverageSendBandwidthKbps);
        Assert.Equal(afterTraffic.PeakSendBandwidthKbps, afterIdle.PeakSendBandwidthKbps, 6);
        Assert.Equal(afterTraffic.PeakReceiveBandwidthKbps, afterIdle.PeakReceiveBandwidthKbps, 6);
    }

    private static InMemoryNetSession CreateSession()
    {
        return new InMemoryNetSession(new NetworkConfig(
            TickRateHz: 20,
            MaxPayloadBytes: 256,
            MaxRpcPerTickPerClient: 16,
            MaxEntitiesPerSnapshot: 128,
            SimulatedRttMs: 30.0,
            SimulatedPacketLossPercent: 0.0));
    }
}
