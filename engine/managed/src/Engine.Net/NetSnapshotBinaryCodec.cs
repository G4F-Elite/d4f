using System.Text;

namespace Engine.Net;

public static class NetSnapshotBinaryCodec
{
    private const uint Magic = 0x504E534Eu; // NSNP
    private const uint Version = 1u;
    private const int MaxEntityCount = 65_536;
    private const int MaxComponentsPerEntity = 1_024;
    private const int MaxRecipeParameters = 4_096;
    private const int MaxStringBytes = 1_048_576;
    private const int MaxPayloadBytes = 4 * 1_048_576;

    public static byte[] Encode(NetSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(Magic);
        writer.Write(Version);
        writer.Write(snapshot.Tick);
        writer.Write(snapshot.Entities.Count);

        foreach (NetEntityState entity in snapshot.Entities)
        {
            writer.Write(entity.EntityId);
            writer.Write(entity.OwnerClientId.HasValue);
            if (entity.OwnerClientId.HasValue)
            {
                writer.Write(entity.OwnerClientId.Value);
            }

            writer.Write(entity.ProceduralSeed);
            WriteString(writer, entity.AssetKey);

            writer.Write(entity.ProceduralRecipe is not null);
            if (entity.ProceduralRecipe is not null)
            {
                NetProceduralRecipeRef recipe = entity.ProceduralRecipe;
                WriteString(writer, recipe.GeneratorId);
                writer.Write(recipe.GeneratorVersion);
                writer.Write(recipe.RecipeVersion);
                WriteString(writer, recipe.RecipeHash);
                writer.Write(recipe.Parameters.Count);
                foreach ((string key, string value) in recipe.Parameters.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
                {
                    WriteString(writer, key);
                    WriteString(writer, value);
                }
            }

            writer.Write(entity.Components.Count);
            foreach (NetComponentState component in entity.Components)
            {
                WriteString(writer, component.ComponentId);
                writer.Write(component.Payload.Length);
                writer.Write(component.Payload);
            }
        }

        writer.Flush();
        return stream.ToArray();
    }

    public static NetSnapshot Decode(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        using var stream = new MemoryStream(data, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        uint magic = reader.ReadUInt32();
        if (magic != Magic)
        {
            throw new InvalidDataException($"Snapshot binary magic mismatch: expected 0x{Magic:X8}, got 0x{magic:X8}.");
        }

        uint version = reader.ReadUInt32();
        if (version != Version)
        {
            throw new InvalidDataException($"Snapshot binary version mismatch: expected {Version}, got {version}.");
        }

        long tick = reader.ReadInt64();
        if (tick < 0L)
        {
            throw new InvalidDataException($"Snapshot tick cannot be negative: {tick}.");
        }

        int entityCount = reader.ReadInt32();
        ValidateCount(entityCount, MaxEntityCount, "entity count");

        var entities = new List<NetEntityState>(entityCount);
        for (int entityIndex = 0; entityIndex < entityCount; entityIndex++)
        {
            uint entityId = reader.ReadUInt32();
            bool hasOwnerClientId = reader.ReadBoolean();
            uint? ownerClientId = hasOwnerClientId ? reader.ReadUInt32() : null;
            ulong proceduralSeed = reader.ReadUInt64();
            string assetKey = ReadString(reader);

            NetProceduralRecipeRef? proceduralRecipe = null;
            bool hasRecipe = reader.ReadBoolean();
            if (hasRecipe)
            {
                string generatorId = ReadString(reader);
                int generatorVersion = reader.ReadInt32();
                int recipeVersion = reader.ReadInt32();
                string recipeHash = ReadString(reader);
                int parameterCount = reader.ReadInt32();
                ValidateCount(parameterCount, MaxRecipeParameters, "recipe parameter count");

                var parameters = new Dictionary<string, string>(parameterCount, StringComparer.Ordinal);
                for (int parameterIndex = 0; parameterIndex < parameterCount; parameterIndex++)
                {
                    string key = ReadString(reader);
                    string value = ReadString(reader);
                    parameters[key] = value;
                }

                proceduralRecipe = new NetProceduralRecipeRef(
                    generatorId,
                    generatorVersion,
                    recipeVersion,
                    recipeHash,
                    parameters);
            }

            int componentCount = reader.ReadInt32();
            ValidateCount(componentCount, MaxComponentsPerEntity, "component count");
            var components = new List<NetComponentState>(componentCount);
            for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
            {
                string componentId = ReadString(reader);
                int payloadLength = reader.ReadInt32();
                if (payloadLength < 0 || payloadLength > MaxPayloadBytes)
                {
                    throw new InvalidDataException($"Component payload length is out of range: {payloadLength}.");
                }

                byte[] payload = reader.ReadBytes(payloadLength);
                if (payload.Length != payloadLength)
                {
                    throw new InvalidDataException("Unexpected end of data while reading component payload.");
                }

                components.Add(new NetComponentState(componentId, payload));
            }

            entities.Add(new NetEntityState(entityId, ownerClientId, proceduralSeed, assetKey, components, proceduralRecipe));
        }

        if (stream.Position != stream.Length)
        {
            throw new InvalidDataException("Unexpected trailing bytes in snapshot binary payload.");
        }

        return new NetSnapshot(tick, entities);
    }

    private static void ValidateCount(int value, int max, string label)
    {
        if (value < 0 || value > max)
        {
            throw new InvalidDataException($"Snapshot {label} is out of range: {value}.");
        }
    }

    private static void WriteString(BinaryWriter writer, string value)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        byte[] bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.Length > MaxStringBytes)
        {
            throw new InvalidDataException($"String exceeds maximum UTF-8 byte length of {MaxStringBytes}.");
        }

        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static string ReadString(BinaryReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        int length = reader.ReadInt32();
        if (length < 0 || length > MaxStringBytes)
        {
            throw new InvalidDataException($"String UTF-8 byte length is out of range: {length}.");
        }

        byte[] bytes = reader.ReadBytes(length);
        if (bytes.Length != length)
        {
            throw new InvalidDataException("Unexpected end of data while reading UTF-8 string.");
        }

        return Encoding.UTF8.GetString(bytes);
    }
}
