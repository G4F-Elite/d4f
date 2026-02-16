namespace Engine.Net;

public sealed class NetComponentState
{
    public NetComponentState(string componentId, byte[] payload)
    {
        if (string.IsNullOrWhiteSpace(componentId))
        {
            throw new ArgumentException("Component id cannot be empty.", nameof(componentId));
        }

        ArgumentNullException.ThrowIfNull(payload);

        ComponentId = componentId.Trim();
        Payload = payload.Length == 0 ? Array.Empty<byte>() : payload.ToArray();
    }

    public string ComponentId { get; }

    public byte[] Payload { get; }
}

public sealed class NetEntityState
{
    public NetEntityState(
        uint entityId,
        uint? ownerClientId,
        ulong proceduralSeed,
        string assetKey,
        IReadOnlyList<NetComponentState> components)
    {
        if (entityId == 0u)
        {
            throw new ArgumentOutOfRangeException(nameof(entityId), "Entity id must be greater than zero.");
        }

        if (ownerClientId is 0u)
        {
            throw new ArgumentOutOfRangeException(nameof(ownerClientId), "Owner client id must be greater than zero when specified.");
        }

        if (string.IsNullOrWhiteSpace(assetKey))
        {
            throw new ArgumentException("Asset key cannot be empty.", nameof(assetKey));
        }

        ArgumentNullException.ThrowIfNull(components);

        var uniqueById = new Dictionary<string, NetComponentState>(StringComparer.Ordinal);
        foreach (NetComponentState component in components)
        {
            ArgumentNullException.ThrowIfNull(component);
            uniqueById[component.ComponentId] = component;
        }

        EntityId = entityId;
        OwnerClientId = ownerClientId;
        ProceduralSeed = proceduralSeed;
        AssetKey = assetKey.Trim();
        Components = uniqueById
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(static pair => pair.Value)
            .ToArray();
    }

    public uint EntityId { get; }

    public uint? OwnerClientId { get; }

    public ulong ProceduralSeed { get; }

    public string AssetKey { get; }

    public IReadOnlyList<NetComponentState> Components { get; }
}

public sealed class NetSnapshot
{
    public NetSnapshot(long tick, IReadOnlyList<NetEntityState> entities)
    {
        if (tick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tick), "Tick cannot be negative.");
        }

        ArgumentNullException.ThrowIfNull(entities);

        var uniqueById = new Dictionary<uint, NetEntityState>();
        foreach (NetEntityState entity in entities)
        {
            ArgumentNullException.ThrowIfNull(entity);
            uniqueById[entity.EntityId] = entity;
        }

        Tick = tick;
        Entities = uniqueById
            .OrderBy(static pair => pair.Key)
            .Select(static pair => pair.Value)
            .ToArray();
    }

    public long Tick { get; }

    public IReadOnlyList<NetEntityState> Entities { get; }
}

public sealed class NetRpcMessage
{
    public NetRpcMessage(
        uint entityId,
        string rpcName,
        byte[] payload,
        NetworkChannel channel,
        uint? targetClientId = null)
    {
        if (string.IsNullOrWhiteSpace(rpcName))
        {
            throw new ArgumentException("RPC name cannot be empty.", nameof(rpcName));
        }

        if (targetClientId is 0u)
        {
            throw new ArgumentOutOfRangeException(nameof(targetClientId), "Target client id must be greater than zero when specified.");
        }

        ArgumentNullException.ThrowIfNull(payload);

        EntityId = entityId;
        RpcName = rpcName.Trim();
        Payload = payload.Length == 0 ? Array.Empty<byte>() : payload.ToArray();
        Channel = channel;
        TargetClientId = targetClientId;
    }

    public uint EntityId { get; }

    public string RpcName { get; }

    public byte[] Payload { get; }

    public NetworkChannel Channel { get; }

    public uint? TargetClientId { get; }
}

public sealed class NetRpcEnvelope
{
    public NetRpcEnvelope(uint sourceClientId, long tick, NetRpcMessage message)
    {
        if (tick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tick), "Tick cannot be negative.");
        }

        ArgumentNullException.ThrowIfNull(message);

        SourceClientId = sourceClientId;
        Tick = tick;
        Message = message;
    }

    public uint SourceClientId { get; }

    public long Tick { get; }

    public NetRpcMessage Message { get; }
}
