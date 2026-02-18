namespace Engine.Net;

public sealed partial class InMemoryNetSession
{
    public bool TrySetEntityOwner(uint entityId, uint? ownerClientId)
    {
        if (entityId == 0u)
        {
            throw new ArgumentOutOfRangeException(nameof(entityId), "Entity id must be greater than zero.");
        }

        ValidateOwnerClientIsConnected(ownerClientId, nameof(ownerClientId));

        if (!_entities.TryGetValue(entityId, out NetEntityState? existing))
        {
            return false;
        }

        if (existing.OwnerClientId == ownerClientId)
        {
            return true;
        }

        _entities[entityId] = CloneEntityWithOwner(existing, ownerClientId);
        return true;
    }

    private void ValidateOwnerClientIsConnected(uint? ownerClientId, string paramName)
    {
        if (ownerClientId is 0u)
        {
            throw new ArgumentOutOfRangeException(paramName, "Owner client id must be greater than zero when specified.");
        }

        if (ownerClientId is uint connectedClientId && !_clients.ContainsKey(connectedClientId))
        {
            throw new KeyNotFoundException($"Owner client '{connectedClientId}' is not connected.");
        }
    }

    private void ClearDisconnectedClientOwnership(uint clientId)
    {
        if (_entities.Count == 0)
        {
            return;
        }

        List<uint>? affectedEntityIds = null;
        foreach ((uint entityId, NetEntityState entity) in _entities)
        {
            if (entity.OwnerClientId != clientId)
            {
                continue;
            }

            affectedEntityIds ??= new List<uint>();
            affectedEntityIds.Add(entityId);
        }

        if (affectedEntityIds is null)
        {
            return;
        }

        foreach (uint entityId in affectedEntityIds)
        {
            _entities[entityId] = CloneEntityWithOwner(_entities[entityId], ownerClientId: null);
        }
    }

    private static NetEntityState CloneEntityWithOwner(NetEntityState source, uint? ownerClientId)
    {
        return new NetEntityState(
            source.EntityId,
            ownerClientId,
            source.ProceduralSeed,
            source.AssetKey,
            source.Components,
            source.ProceduralRecipe);
    }
}
