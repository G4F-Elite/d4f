using Engine.Net;

namespace Engine.Tests.Net;

public sealed class InMemoryNetSessionTransportSimulationTests
{
    [Fact]
    public void NetworkConfig_ShouldRejectInvalidTransportSimulationValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new NetworkConfig(SimulatedRttMs: -0.1).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new NetworkConfig(SimulatedPacketLossPercent: -1.0).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new NetworkConfig(SimulatedPacketLossPercent: 101.0).Validate());
    }

    [Fact]
    public void Pump_ShouldApplyConfiguredRttToServerAndClients()
    {
        InMemoryNetSession session = CreateSession(new NetworkConfig(
            TickRateHz: 30,
            MaxPayloadBytes: 256,
            MaxRpcPerTickPerClient: 16,
            MaxEntitiesPerSnapshot: 128,
            SimulatedRttMs: 42.5,
            SimulatedPacketLossPercent: 0.0));

        session.RegisterReplicatedComponent("transform");
        uint clientId = session.ConnectClient();
        session.UpsertServerEntity(CreateEntity(1u));

        session.Pump();

        NetPeerStats server = session.GetServerStats();
        NetPeerStats client = session.GetClientStats(clientId);
        Assert.Equal(42.5, server.RoundTripTimeMs, 6);
        Assert.Equal(42.5, client.RoundTripTimeMs, 6);
    }

    [Fact]
    public void QueueClientRpc_ShouldDropPackets_WhenLossIsHundredPercent()
    {
        InMemoryNetSession session = CreateSession(new NetworkConfig(
            TickRateHz: 30,
            MaxPayloadBytes: 256,
            MaxRpcPerTickPerClient: 16,
            MaxEntitiesPerSnapshot: 128,
            SimulatedPacketLossPercent: 100.0));
        uint clientId = session.ConnectClient();

        session.QueueClientRpc(clientId, new NetRpcMessage(0u, "ping", [1], NetworkChannel.Unreliable));
        session.Pump();

        Assert.Empty(session.DrainServerInbox());
        NetPeerStats clientStats = session.GetClientStats(clientId);
        NetPeerStats serverStats = session.GetServerStats();
        Assert.Equal(1, clientStats.MessagesSent);
        Assert.True(clientStats.MessagesDropped >= 1);
        Assert.True(serverStats.MessagesDropped >= 1);
    }

    [Fact]
    public void QueueServerRpc_ShouldDropBroadcastPackets_WhenLossIsHundredPercent()
    {
        InMemoryNetSession session = CreateSession(new NetworkConfig(
            TickRateHz: 30,
            MaxPayloadBytes: 256,
            MaxRpcPerTickPerClient: 16,
            MaxEntitiesPerSnapshot: 128,
            SimulatedPacketLossPercent: 100.0));
        uint firstClient = session.ConnectClient();
        uint secondClient = session.ConnectClient();
        session.QueueServerRpc(new NetRpcMessage(0u, "broadcast", [1, 2], NetworkChannel.ReliableOrdered));

        session.Pump();

        Assert.Empty(session.DrainClientInbox(firstClient));
        Assert.Empty(session.DrainClientInbox(secondClient));
        Assert.True(session.GetClientStats(firstClient).MessagesDropped >= 1);
        Assert.True(session.GetClientStats(secondClient).MessagesDropped >= 1);
        Assert.True(session.GetServerStats().MessagesDropped >= 2);
    }

    [Fact]
    public void Pump_ShouldDropSnapshots_WhenLossIsHundredPercent()
    {
        InMemoryNetSession session = CreateSession(new NetworkConfig(
            TickRateHz: 30,
            MaxPayloadBytes: 256,
            MaxRpcPerTickPerClient: 16,
            MaxEntitiesPerSnapshot: 128,
            SimulatedPacketLossPercent: 100.0));
        session.RegisterReplicatedComponent("transform");
        uint clientId = session.ConnectClient();
        session.UpsertServerEntity(CreateEntity(7u));

        session.Pump();

        Assert.Throws<InvalidOperationException>(() => session.GetClientSnapshot(clientId));
        NetPeerStats clientStats = session.GetClientStats(clientId);
        Assert.True(clientStats.MessagesDropped >= 1);
        Assert.Equal(0, clientStats.MessagesReceived);
    }

    private static InMemoryNetSession CreateSession(NetworkConfig config)
    {
        return new InMemoryNetSession(config);
    }

    private static NetEntityState CreateEntity(uint id)
    {
        return new NetEntityState(
            entityId: id,
            ownerClientId: null,
            proceduralSeed: 99u,
            assetKey: $"asset:{id}",
            components: [new NetComponentState("transform", [1, 2, 3])]);
    }
}
