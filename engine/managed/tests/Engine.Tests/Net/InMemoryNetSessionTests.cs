using System.IO;
using Engine.Net;

namespace Engine.Tests.Net;

public sealed class InMemoryNetSessionTests
{
    [Fact]
    public void RegisterReplicatedComponent_ShouldUseAttributeId()
    {
        InMemoryNetSession session = CreateSession();

        session.RegisterReplicatedComponent<TransformComponent>();

        Assert.True(session.IsComponentReplicated("transform"));
    }

    [Fact]
    public void UpsertServerEntity_ShouldFail_WhenComponentIsNotWhitelisted()
    {
        InMemoryNetSession session = CreateSession();

        NetEntityState entity = CreateEntity(1u, ownerClientId: null, componentId: "transform", payload: [1, 2]);

        Assert.Throws<InvalidDataException>(() => session.UpsertServerEntity(entity));
    }

    [Fact]
    public void UpsertServerEntity_ShouldFail_WhenOwnerClientIsNotConnected()
    {
        InMemoryNetSession session = CreateSession();
        session.RegisterReplicatedComponent("transform");

        NetEntityState entity = CreateEntity(1u, ownerClientId: 404u, componentId: "transform", payload: [1, 2]);

        Assert.Throws<KeyNotFoundException>(() => session.UpsertServerEntity(entity));
    }

    [Fact]
    public void Pump_ShouldReplicateDeterministicSnapshotOrdering()
    {
        InMemoryNetSession session = CreateSession();
        session.RegisterReplicatedComponent("transform");

        uint firstClient = session.ConnectClient();
        uint secondClient = session.ConnectClient();

        session.UpsertServerEntity(CreateEntity(2u, ownerClientId: null, componentId: "transform", payload: [2]));
        session.UpsertServerEntity(CreateEntity(1u, ownerClientId: null, componentId: "transform", payload: [1]));

        long tick = session.Pump();

        NetSnapshot first = session.GetClientSnapshot(firstClient);
        NetSnapshot second = session.GetClientSnapshot(secondClient);

        Assert.Equal(1L, tick);
        Assert.Equal(1L, first.Tick);
        Assert.Equal(1L, second.Tick);
        Assert.Equal([1u, 2u], first.Entities.Select(static x => x.EntityId).ToArray());
        Assert.Equal([1u, 2u], second.Entities.Select(static x => x.EntityId).ToArray());
    }

    [Fact]
    public void QueueClientRpc_ShouldFail_WhenEntityIsOwnedByAnotherClient()
    {
        InMemoryNetSession session = CreateSession();
        session.RegisterReplicatedComponent("transform");

        uint ownerClientId = session.ConnectClient();
        uint foreignClientId = session.ConnectClient();

        session.UpsertServerEntity(CreateEntity(10u, ownerClientId, "transform", [1]));

        NetRpcMessage rpc = new(10u, "move", [7], NetworkChannel.ReliableOrdered);

        Assert.Throws<InvalidOperationException>(() => session.QueueClientRpc(foreignClientId, rpc));
    }

    [Fact]
    public void TrySetEntityOwner_ShouldUpdateSnapshotAndRpcAuthorization()
    {
        InMemoryNetSession session = CreateSession();
        session.RegisterReplicatedComponent("transform");

        uint firstOwnerClientId = session.ConnectClient();
        uint nextOwnerClientId = session.ConnectClient();
        uint observerClientId = session.ConnectClient();

        session.UpsertServerEntity(CreateEntity(10u, firstOwnerClientId, "transform", [1]));
        bool updated = session.TrySetEntityOwner(10u, nextOwnerClientId);

        Assert.True(updated);

        session.Pump();
        NetSnapshot snapshot = session.GetClientSnapshot(observerClientId);
        NetEntityState entity = Assert.Single(snapshot.Entities);
        Assert.Equal(nextOwnerClientId, entity.OwnerClientId);

        NetRpcMessage move = new(10u, "move", [7], NetworkChannel.ReliableOrdered);
        Assert.Throws<InvalidOperationException>(() => session.QueueClientRpc(firstOwnerClientId, move));

        session.QueueClientRpc(nextOwnerClientId, move);
        session.Pump();

        NetRpcEnvelope envelope = Assert.Single(session.DrainServerInbox());
        Assert.Equal(nextOwnerClientId, envelope.SourceClientId);
        Assert.Equal("move", envelope.Message.RpcName);
    }

    [Fact]
    public void TrySetEntityOwner_ShouldReturnFalse_WhenEntityIsUnknown()
    {
        InMemoryNetSession session = CreateSession();
        uint ownerClientId = session.ConnectClient();

        bool changed = session.TrySetEntityOwner(999u, ownerClientId);

        Assert.False(changed);
    }

    [Fact]
    public void TrySetEntityOwner_ShouldFail_WhenOwnerClientIsNotConnected()
    {
        InMemoryNetSession session = CreateSession();
        session.RegisterReplicatedComponent("transform");

        uint ownerClientId = session.ConnectClient();
        session.UpsertServerEntity(CreateEntity(10u, ownerClientId, "transform", [1]));

        Assert.Throws<KeyNotFoundException>(() => session.TrySetEntityOwner(10u, ownerClientId: 1000u));
    }

    [Fact]
    public void DisconnectClient_ShouldClearEntityOwnership()
    {
        InMemoryNetSession session = CreateSession();
        session.RegisterReplicatedComponent("transform");

        uint ownerClientId = session.ConnectClient();
        uint observerClientId = session.ConnectClient();
        session.UpsertServerEntity(CreateEntity(8u, ownerClientId, "transform", [9]));

        bool disconnected = session.DisconnectClient(ownerClientId);

        Assert.True(disconnected);

        session.Pump();
        NetSnapshot snapshot = session.GetClientSnapshot(observerClientId);
        NetEntityState entity = Assert.Single(snapshot.Entities);
        Assert.Null(entity.OwnerClientId);
    }

    [Fact]
    public void QueueClientRpc_ShouldFail_WhenEntityIsUnknown()
    {
        InMemoryNetSession session = CreateSession();
        uint clientId = session.ConnectClient();

        NetRpcMessage rpc = new(77u, "move", [1], NetworkChannel.ReliableOrdered);

        Assert.Throws<KeyNotFoundException>(() => session.QueueClientRpc(clientId, rpc));

        NetPeerStats clientStats = session.GetClientStats(clientId);
        Assert.Equal(0, clientStats.MessagesSent);
        Assert.Equal(1, clientStats.MessagesDropped);
        Assert.Equal(1, clientStats.MessagesAttempted);
        Assert.Equal(100d, clientStats.LossPercent, 6);
    }

    [Fact]
    public void QueueClientRpc_ShouldEnforcePerTickRateLimit()
    {
        InMemoryNetSession session = CreateSession(new NetworkConfig(TickRateHz: 20, MaxPayloadBytes: 64, MaxRpcPerTickPerClient: 1, MaxEntitiesPerSnapshot: 64));
        uint clientId = session.ConnectClient();

        NetRpcMessage rpc = new(0u, "ping", [1], NetworkChannel.Unreliable);

        session.QueueClientRpc(clientId, rpc);

        Assert.Throws<InvalidOperationException>(() => session.QueueClientRpc(clientId, rpc));

        session.Pump();

        session.QueueClientRpc(clientId, rpc);
        session.Pump();

        IReadOnlyList<NetRpcEnvelope> inbox = session.DrainServerInbox();
        Assert.Equal(2, inbox.Count);
    }

    [Fact]
    public void QueueRpc_ShouldFail_WhenPayloadExceedsLimit()
    {
        InMemoryNetSession session = CreateSession(new NetworkConfig(TickRateHz: 20, MaxPayloadBytes: 2, MaxRpcPerTickPerClient: 8, MaxEntitiesPerSnapshot: 64));
        uint clientId = session.ConnectClient();

        NetRpcMessage rpc = new(0u, "big", [1, 2, 3], NetworkChannel.ReliableOrdered);

        Assert.Throws<InvalidDataException>(() => session.QueueClientRpc(clientId, rpc));
        Assert.Throws<InvalidDataException>(() => session.QueueServerRpc(rpc));
    }

    [Fact]
    public void QueueRpc_ShouldFail_WhenChannelIsDisabledByConfig()
    {
        InMemoryNetSession session = CreateSession(new NetworkConfig(
            TickRateHz: 20,
            MaxPayloadBytes: 64,
            MaxRpcPerTickPerClient: 8,
            MaxEntitiesPerSnapshot: 64,
            AllowedRpcChannels: NetworkChannelMask.ReliableOrdered));
        uint clientId = session.ConnectClient();

        NetRpcMessage reliable = new(0u, "ok", [1], NetworkChannel.ReliableOrdered);
        NetRpcMessage unreliable = new(0u, "drop", [2], NetworkChannel.Unreliable);

        session.QueueClientRpc(clientId, reliable);
        Assert.Throws<InvalidDataException>(() => session.QueueClientRpc(clientId, unreliable));
        Assert.Throws<InvalidDataException>(() => session.QueueServerRpc(unreliable));
    }

    [Fact]
    public void QueueServerRpc_ShouldSupportTargetAndBroadcast()
    {
        InMemoryNetSession session = CreateSession();
        uint firstClient = session.ConnectClient();
        uint secondClient = session.ConnectClient();

        session.QueueServerRpc(new NetRpcMessage(0u, "private", [1], NetworkChannel.ReliableOrdered, targetClientId: firstClient));
        session.QueueServerRpc(new NetRpcMessage(0u, "broadcast", [2], NetworkChannel.Unreliable));

        session.Pump();

        IReadOnlyList<NetRpcEnvelope> firstInbox = session.DrainClientInbox(firstClient);
        IReadOnlyList<NetRpcEnvelope> secondInbox = session.DrainClientInbox(secondClient);

        Assert.Equal(2, firstInbox.Count);
        Assert.Single(secondInbox);
        Assert.Equal("private", firstInbox[0].Message.RpcName);
        Assert.Equal("broadcast", firstInbox[1].Message.RpcName);
        Assert.Equal("broadcast", secondInbox[0].Message.RpcName);
        Assert.All(firstInbox, static envelope => Assert.Equal(0u, envelope.SourceClientId));
    }

    [Fact]
    public void TryGetClientInterpolationWindow_ShouldReturnPreviousAndCurrentSnapshots()
    {
        InMemoryNetSession session = CreateSession();
        session.RegisterReplicatedComponent("transform");
        uint clientId = session.ConnectClient();

        session.UpsertServerEntity(CreateEntity(1u, null, "transform", [1]));
        session.Pump();

        session.UpsertServerEntity(CreateEntity(1u, null, "transform", [2]));
        session.Pump();

        bool hasWindow = session.TryGetClientInterpolationWindow(clientId, out NetSnapshot from, out NetSnapshot to);

        Assert.True(hasWindow);
        Assert.Equal(1L, from.Tick);
        Assert.Equal(2L, to.Tick);
        Assert.Equal(0f, ClientInterpolationBuffer.ComputeAlpha(0d, from, to));
        Assert.Equal(0f, ClientInterpolationBuffer.ComputeAlpha(1d, from, to));
        Assert.Equal(0.5f, ClientInterpolationBuffer.ComputeAlpha(1.5d, from, to));
        Assert.Equal(1f, ClientInterpolationBuffer.ComputeAlpha(2d, from, to));
        Assert.Equal(1f, ClientInterpolationBuffer.ComputeAlpha(3d, from, to));
    }

    [Fact]
    public void TrySampleClientInterpolation_ShouldReturnAlphaForRenderTick()
    {
        InMemoryNetSession session = CreateSession();
        session.RegisterReplicatedComponent("transform");
        uint clientId = session.ConnectClient();

        session.UpsertServerEntity(CreateEntity(1u, null, "transform", [1]));
        session.Pump();
        session.UpsertServerEntity(CreateEntity(1u, null, "transform", [2]));
        session.Pump();

        bool ok = session.TrySampleClientInterpolation(clientId, 1.25d, out NetInterpolationSample? sample);

        Assert.True(ok);
        NetInterpolationSample typed = Assert.IsType<NetInterpolationSample>(sample);
        Assert.Equal(1L, typed.From.Tick);
        Assert.Equal(2L, typed.To.Tick);
        Assert.Equal(0.25f, typed.Alpha);
    }

    [Fact]
    public void TrySampleClientInterpolation_ShouldReturnFalseWithoutWindow()
    {
        InMemoryNetSession session = CreateSession();
        session.RegisterReplicatedComponent("transform");
        uint clientId = session.ConnectClient();

        session.UpsertServerEntity(CreateEntity(1u, null, "transform", [1]));
        session.Pump();

        bool ok = session.TrySampleClientInterpolation(clientId, 1.0d, out NetInterpolationSample? sample);

        Assert.False(ok);
        Assert.Null(sample);
    }

    [Fact]
    public void TrySampleClientInterpolation_ShouldRejectNonFiniteRenderTick()
    {
        InMemoryNetSession session = CreateSession();
        session.RegisterReplicatedComponent("transform");
        uint clientId = session.ConnectClient();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            session.TrySampleClientInterpolation(clientId, double.NaN, out _));
    }

    [Fact]
    public void Snapshot_ShouldPreserveProceduralSeedAndAssetKey()
    {
        InMemoryNetSession session = CreateSession();
        session.RegisterReplicatedComponent("transform");
        uint clientId = session.ConnectClient();

        session.UpsertServerEntity(new NetEntityState(
            entityId: 55u,
            ownerClientId: null,
            proceduralSeed: 9123u,
            assetKey: "PROC:ROOM:A",
            components: [new NetComponentState("transform", [4, 2])]));

        session.Pump();
        NetSnapshot snapshot = session.GetClientSnapshot(clientId);

        NetEntityState entity = Assert.Single(snapshot.Entities);
        Assert.Equal(9123u, entity.ProceduralSeed);
        Assert.Equal("PROC:ROOM:A", entity.AssetKey);
    }

    [Fact]
    public void Stats_ShouldTrackServerAndClientTraffic()
    {
        InMemoryNetSession session = CreateSession();
        session.RegisterReplicatedComponent("transform");
        uint clientId = session.ConnectClient();

        session.UpsertServerEntity(CreateEntity(1u, clientId, "transform", [1, 2, 3]));
        session.QueueClientRpc(clientId, new NetRpcMessage(1u, "input", [9], NetworkChannel.Unreliable));
        session.QueueServerRpc(new NetRpcMessage(1u, "ack", [8], NetworkChannel.ReliableOrdered, targetClientId: clientId));

        session.Pump();

        NetPeerStats serverStats = session.GetServerStats();
        NetPeerStats clientStats = session.GetClientStats(clientId);

        Assert.True(serverStats.BytesSent > 0);
        Assert.True(serverStats.BytesReceived > 0);
        Assert.True(clientStats.BytesSent > 0);
        Assert.True(clientStats.BytesReceived > 0);
        Assert.Equal(0, serverStats.MessagesDropped);
        Assert.True(clientStats.MessagesReceived >= 2);
    }

    [Fact]
    public void LossPercent_ShouldIncludeDroppedMessagesInDenominator()
    {
        InMemoryNetSession session = CreateSession();
        session.RegisterReplicatedComponent("transform");
        uint clientId = session.ConnectClient();
        session.UpsertServerEntity(CreateEntity(1u, clientId, "transform", [1]));

        session.QueueClientRpc(clientId, new NetRpcMessage(1u, "ok", [1], NetworkChannel.Unreliable));
        Assert.Throws<KeyNotFoundException>(() =>
            session.QueueClientRpc(clientId, new NetRpcMessage(99u, "bad", [2], NetworkChannel.Unreliable)));

        NetPeerStats stats = session.GetClientStats(clientId);
        Assert.Equal(1, stats.MessagesSent);
        Assert.Equal(1, stats.MessagesDropped);
        Assert.Equal(2, stats.MessagesAttempted);
        Assert.Equal(50d, stats.LossPercent, 6);
    }

    private static InMemoryNetSession CreateSession(NetworkConfig? config = null)
    {
        return new InMemoryNetSession(config ?? new NetworkConfig());
    }

    private static NetEntityState CreateEntity(
        uint entityId,
        uint? ownerClientId,
        string componentId,
        byte[] payload)
    {
        return new NetEntityState(
            entityId,
            ownerClientId,
            proceduralSeed: 42u,
            assetKey: $"asset:{entityId}",
            components: [new NetComponentState(componentId, payload)]);
    }

    [ReplicatedComponent("transform")]
    private readonly struct TransformComponent;
}
