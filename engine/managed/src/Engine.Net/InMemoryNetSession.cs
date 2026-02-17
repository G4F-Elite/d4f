using System.Reflection;

namespace Engine.Net;

public sealed class InMemoryNetSession
{
    private readonly NetworkConfig _config;
    private readonly DeterministicNetClock _clock;
    private readonly HashSet<string> _replicatedComponentIds = new(StringComparer.Ordinal);
    private readonly SortedDictionary<uint, NetEntityState> _entities = new();
    private readonly SortedDictionary<uint, ClientState> _clients = new();
    private readonly Queue<PendingClientRpc> _pendingClientRpcs = new();
    private readonly Queue<NetRpcMessage> _pendingServerRpcs = new();
    private readonly Queue<NetRpcEnvelope> _serverInbox = new();
    private readonly NetPeerStats _serverStats = new();
    private uint _nextClientId = 1u;

    public InMemoryNetSession(NetworkConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        _config = config.Validate();
        _clock = new DeterministicNetClock(_config.TickRateHz);
    }

    public long CurrentTick => _clock.CurrentTick;

    public int ConnectedClientCount => _clients.Count;

    public IReadOnlyCollection<uint> ConnectedClients => _clients.Keys;

    public void RegisterReplicatedComponent(string componentId)
    {
        if (string.IsNullOrWhiteSpace(componentId))
        {
            throw new ArgumentException("Component id cannot be empty.", nameof(componentId));
        }

        _replicatedComponentIds.Add(componentId.Trim());
    }

    public void RegisterReplicatedComponent<T>()
    {
        ReplicatedComponentAttribute? attribute = typeof(T).GetCustomAttribute<ReplicatedComponentAttribute>();
        if (attribute is null)
        {
            throw new InvalidDataException(
                $"Type '{typeof(T).FullName}' is missing [{nameof(ReplicatedComponentAttribute)}].");
        }

        RegisterReplicatedComponent(attribute.ComponentId);
    }

    public bool IsComponentReplicated(string componentId)
    {
        if (string.IsNullOrWhiteSpace(componentId))
        {
            return false;
        }

        return _replicatedComponentIds.Contains(componentId.Trim());
    }

    public uint ConnectClient()
    {
        uint clientId = _nextClientId;
        _nextClientId = checked(_nextClientId + 1u);
        _clients.Add(clientId, new ClientState());
        return clientId;
    }

    public bool DisconnectClient(uint clientId)
    {
        if (clientId == 0u)
        {
            throw new ArgumentOutOfRangeException(nameof(clientId), "Client id must be greater than zero.");
        }

        return _clients.Remove(clientId);
    }

    public void UpsertServerEntity(NetEntityState entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ValidateEntity(entity);

        if (!_entities.ContainsKey(entity.EntityId) && _entities.Count >= _config.MaxEntitiesPerSnapshot)
        {
            throw new InvalidOperationException(
                $"Max entities per snapshot limit ({_config.MaxEntitiesPerSnapshot}) was reached.");
        }

        _entities[entity.EntityId] = entity;
    }

    public bool DespawnServerEntity(uint entityId)
    {
        if (entityId == 0u)
        {
            throw new ArgumentOutOfRangeException(nameof(entityId), "Entity id must be greater than zero.");
        }

        return _entities.Remove(entityId);
    }

    public void QueueClientRpc(uint sourceClientId, NetRpcMessage message)
    {
        if (sourceClientId == 0u)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceClientId), "Client id must be greater than zero.");
        }

        ArgumentNullException.ThrowIfNull(message);

        if (!_clients.TryGetValue(sourceClientId, out ClientState? client))
        {
            throw new KeyNotFoundException($"Client '{sourceClientId}' is not connected.");
        }

        ValidateRpc(message);

        if (client.QueuedRpcsThisTick >= _config.MaxRpcPerTickPerClient)
        {
            client.Stats.RecordDropped();
            throw new InvalidOperationException(
                $"Client '{sourceClientId}' exceeded per-tick RPC limit ({_config.MaxRpcPerTickPerClient}).");
        }

        if (message.EntityId != 0u)
        {
            if (!_entities.TryGetValue(message.EntityId, out NetEntityState? entity))
            {
                client.Stats.RecordDropped();
                throw new KeyNotFoundException($"Entity '{message.EntityId}' is not available on server.");
            }

            if (entity.OwnerClientId is uint ownerClientId && ownerClientId != sourceClientId)
            {
                client.Stats.RecordDropped();
                throw new InvalidOperationException(
                    $"Client '{sourceClientId}' cannot send RPC for entity '{message.EntityId}' owned by '{ownerClientId}'.");
            }
        }

        _pendingClientRpcs.Enqueue(new PendingClientRpc(sourceClientId, message));
        client.QueuedRpcsThisTick++;
        client.Stats.RecordSent(message.Payload.Length);
    }

    public void QueueServerRpc(NetRpcMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        ValidateRpc(message);
        if (message.TargetClientId is uint targetClientId && !_clients.ContainsKey(targetClientId))
        {
            throw new KeyNotFoundException($"Client '{targetClientId}' is not connected.");
        }

        _pendingServerRpcs.Enqueue(message);
    }

    public long Pump()
    {
        long tick = _clock.Step();

        while (_pendingClientRpcs.Count > 0)
        {
            PendingClientRpc pending = _pendingClientRpcs.Dequeue();
            _serverInbox.Enqueue(new NetRpcEnvelope(pending.SourceClientId, tick, pending.Message));
            _serverStats.RecordReceived(pending.Message.Payload.Length);
        }

        NetSnapshot snapshot = new(tick, _entities.Values.ToArray());
        int snapshotSize = EstimateSnapshotSizeBytes(snapshot);
        foreach (ClientState client in _clients.Values)
        {
            client.Interpolation.Push(snapshot);
            client.LatestSnapshot = snapshot;
            client.QueuedRpcsThisTick = 0;
            client.Stats.RecordReceived(snapshotSize);
            _serverStats.RecordSent(snapshotSize);
        }

        while (_pendingServerRpcs.Count > 0)
        {
            NetRpcMessage message = _pendingServerRpcs.Dequeue();
            if (message.TargetClientId is uint targetClientId)
            {
                DeliverServerRpcToClient(targetClientId, message, tick);
                continue;
            }

            foreach (uint clientId in _clients.Keys)
            {
                DeliverServerRpcToClient(clientId, message, tick);
            }
        }

        return tick;
    }

    public NetSnapshot GetClientSnapshot(uint clientId)
    {
        ClientState client = GetClientState(clientId);
        return client.LatestSnapshot
            ?? throw new InvalidOperationException($"Client '{clientId}' does not have a replicated snapshot yet.");
    }

    public bool TryGetClientInterpolationWindow(uint clientId, out NetSnapshot from, out NetSnapshot to)
    {
        ClientState client = GetClientState(clientId);
        return client.Interpolation.TryGetWindow(out from, out to);
    }

    public IReadOnlyList<NetRpcEnvelope> DrainServerInbox()
    {
        return DrainQueue(_serverInbox);
    }

    public IReadOnlyList<NetRpcEnvelope> DrainClientInbox(uint clientId)
    {
        ClientState client = GetClientState(clientId);
        return DrainQueue(client.Inbox);
    }

    public NetPeerStats GetServerStats()
    {
        return _serverStats.Clone();
    }

    public NetPeerStats GetClientStats(uint clientId)
    {
        ClientState client = GetClientState(clientId);
        return client.Stats.Clone();
    }

    private ClientState GetClientState(uint clientId)
    {
        if (clientId == 0u)
        {
            throw new ArgumentOutOfRangeException(nameof(clientId), "Client id must be greater than zero.");
        }

        if (!_clients.TryGetValue(clientId, out ClientState? client))
        {
            throw new KeyNotFoundException($"Client '{clientId}' is not connected.");
        }

        return client;
    }

    private void ValidateEntity(NetEntityState entity)
    {
        if (entity.ProceduralRecipe is not null)
        {
            int metadataSize = EstimateProceduralRecipeSizeBytes(entity.ProceduralRecipe);
            if (metadataSize > _config.MaxPayloadBytes)
            {
                throw new InvalidDataException(
                    $"Procedural recipe metadata for entity '{entity.EntityId}' exceeds max payload size {_config.MaxPayloadBytes} bytes.");
            }
        }

        foreach (NetComponentState component in entity.Components)
        {
            if (!_replicatedComponentIds.Contains(component.ComponentId))
            {
                throw new InvalidDataException(
                    $"Component '{component.ComponentId}' is not whitelisted for replication.");
            }

            if (component.Payload.Length > _config.MaxPayloadBytes)
            {
                throw new InvalidDataException(
                    $"Component payload for '{component.ComponentId}' exceeds max payload size {_config.MaxPayloadBytes} bytes.");
            }
        }
    }

    private void ValidateRpc(NetRpcMessage message)
    {
        if (!Enum.IsDefined(message.Channel))
        {
            throw new InvalidDataException($"Unsupported network channel value: {message.Channel}.");
        }

        if (message.Payload.Length > _config.MaxPayloadBytes)
        {
            throw new InvalidDataException(
                $"RPC payload for '{message.RpcName}' exceeds max payload size {_config.MaxPayloadBytes} bytes.");
        }
    }

    private void DeliverServerRpcToClient(uint clientId, NetRpcMessage message, long tick)
    {
        ClientState client = GetClientState(clientId);
        client.Inbox.Enqueue(new NetRpcEnvelope(0u, tick, message));
        client.Stats.RecordReceived(message.Payload.Length);
        _serverStats.RecordSent(message.Payload.Length);
    }

    private static IReadOnlyList<NetRpcEnvelope> DrainQueue(Queue<NetRpcEnvelope> queue)
    {
        if (queue.Count == 0)
        {
            return Array.Empty<NetRpcEnvelope>();
        }

        var drained = new NetRpcEnvelope[queue.Count];
        for (int i = 0; i < drained.Length; i++)
        {
            drained[i] = queue.Dequeue();
        }

        return drained;
    }

    private static int EstimateSnapshotSizeBytes(NetSnapshot snapshot)
    {
        int size = 0;
        foreach (NetEntityState entity in snapshot.Entities)
        {
            size = checked(size + sizeof(uint));
            size = checked(size + sizeof(ulong));
            size = checked(size + entity.AssetKey.Length);
            if (entity.ProceduralRecipe is not null)
            {
                size = checked(size + EstimateProceduralRecipeSizeBytes(entity.ProceduralRecipe));
            }

            foreach (NetComponentState component in entity.Components)
            {
                size = checked(size + component.ComponentId.Length);
                size = checked(size + component.Payload.Length);
            }
        }

        return size;
    }

    private static int EstimateProceduralRecipeSizeBytes(NetProceduralRecipeRef recipe)
    {
        int size = 0;
        size = checked(size + recipe.GeneratorId.Length);
        size = checked(size + sizeof(int));
        size = checked(size + sizeof(int));
        size = checked(size + recipe.RecipeHash.Length);
        foreach ((string key, string value) in recipe.Parameters)
        {
            size = checked(size + key.Length);
            size = checked(size + value.Length);
        }

        return size;
    }

    private sealed class ClientState
    {
        public ClientInterpolationBuffer Interpolation { get; } = new();

        public Queue<NetRpcEnvelope> Inbox { get; } = new();

        public NetPeerStats Stats { get; } = new();

        public NetSnapshot? LatestSnapshot { get; set; }

        public int QueuedRpcsThisTick { get; set; }
    }

    private readonly record struct PendingClientRpc(uint SourceClientId, NetRpcMessage Message);
}

