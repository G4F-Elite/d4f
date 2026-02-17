namespace Engine.Procedural;

public readonly record struct LevelChunkTag(LevelNodeType NodeType, string TypeTag, int Variant)
{
    public static LevelChunkTag Parse(string meshTag)
    {
        if (string.IsNullOrWhiteSpace(meshTag))
        {
            throw new ArgumentException("Mesh tag cannot be empty.", nameof(meshTag));
        }

        string[] parts = meshTag.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 3 || !string.Equals(parts[0], "chunk", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Mesh tag '{meshTag}' must have format 'chunk/<type>/v<variant>'.");
        }

        if (!parts[2].StartsWith("v", StringComparison.OrdinalIgnoreCase) ||
            !int.TryParse(parts[2].AsSpan(1), out int variant) ||
            variant < 0 ||
            variant > 3)
        {
            throw new InvalidDataException($"Mesh tag '{meshTag}' has invalid variant segment '{parts[2]}'.");
        }

        string typeTag = parts[1].ToLowerInvariant();
        LevelNodeType nodeType = typeTag switch
        {
            "room" => LevelNodeType.Room,
            "corridor" => LevelNodeType.Corridor,
            "junction" => LevelNodeType.Junction,
            "deadend" => LevelNodeType.DeadEnd,
            "shaft" => LevelNodeType.Shaft,
            _ => throw new InvalidDataException($"Mesh tag '{meshTag}' has unsupported type segment '{parts[1]}'.")
        };

        return new LevelChunkTag(nodeType, typeTag, variant);
    }
}
