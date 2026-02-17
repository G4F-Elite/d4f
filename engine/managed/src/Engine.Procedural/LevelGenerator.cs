using System.Numerics;

namespace Engine.Procedural;

public enum LevelNodeType
{
    Room = 0,
    Corridor = 1,
    Junction = 2,
    DeadEnd = 3,
    Shaft = 4
}

public sealed record LevelNode(int Id, LevelNodeType Type, Vector3 Position, IReadOnlyList<int> Connections);

public sealed record LevelGraph(IReadOnlyList<LevelNode> Nodes);

public sealed record LevelMeshChunk(int NodeId, string MeshTag);

public sealed record LevelSpawnPoint(int NodeId, Vector3 Position, string Category);

public sealed record LevelGenOptions(
    uint Seed,
    int TargetNodes,
    float Density,
    float Danger,
    float Complexity = 0.5f)
{
    public LevelGenOptions Validate()
    {
        if (TargetNodes <= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(TargetNodes), "Target nodes must be greater than one.");
        }

        if (Density <= 0f || Density > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(Density), "Density must be within (0,1].");
        }

        if (Danger < 0f || Danger > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(Danger), "Danger must be within [0,1].");
        }

        if (Complexity < 0f || Complexity > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(Complexity), "Complexity must be within [0,1].");
        }

        return this;
    }
}

public sealed record LevelGenResult(
    LevelGraph Graph,
    IReadOnlyList<LevelMeshChunk> MeshChunks,
    IReadOnlyList<LevelSpawnPoint> SpawnPoints);

public static class LevelGenerator
{
    public static LevelGenResult Generate(LevelGenOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _ = options.Validate();

        int nodeCount = options.TargetNodes;
        var rng = new Random(unchecked((int)options.Seed));
        var nodeTypes = new LevelNodeType[nodeCount];
        var positions = new Vector3[nodeCount];
        var connectionsByNode = new Dictionary<int, HashSet<int>>(nodeCount);
        for (int id = 0; id < nodeCount; id++)
        {
            connectionsByNode[id] = [];
        }

        nodeTypes[0] = LevelNodeType.Room;
        positions[0] = Vector3.Zero;
        for (int id = 1; id < nodeCount; id++)
        {
            int parentId = ChooseParentNodeId(rng, id, connectionsByNode, options.Complexity);
            LevelNodeType type = ChooseNodeType(rng, id, nodeCount, options.Danger, options.Complexity);
            Vector3 position = ChooseNodePosition(rng, parentId, type, positions, id, options.Density, options.Danger);

            nodeTypes[id] = type;
            positions[id] = position;

            ConnectBidirectional(parentId, id, connectionsByNode);
            TryAddExtraConnections(rng, id, positions, connectionsByNode, options.Density, options.Complexity);
        }

        LevelNode[] finalizedNodes = Enumerable.Range(0, nodeCount)
            .Select(id => new LevelNode(
                id,
                nodeTypes[id],
                positions[id],
                connectionsByNode[id].OrderBy(static x => x).ToArray()))
            .ToArray();

        LevelMeshChunk[] meshChunks = BuildMeshChunks(options.Seed, finalizedNodes);
        LevelSpawnPoint[] spawnPoints = BuildSpawnPoints(options, finalizedNodes, connectionsByNode);

        return new LevelGenResult(new LevelGraph(finalizedNodes), meshChunks, spawnPoints);
    }

    private static LevelMeshChunk[] BuildMeshChunks(uint seed, IReadOnlyList<LevelNode> nodes)
    {
        var meshChunks = new LevelMeshChunk[nodes.Count];
        for (int i = 0; i < nodes.Count; i++)
        {
            LevelNode node = nodes[i];
            uint variantSeed = seed ^ ((uint)node.Id * 2654435761u) ^ ((uint)node.Type * 97u);
            int variant = (int)(variantSeed & 0x3u);
            string typeTag = node.Type.ToString().ToLowerInvariant();
            meshChunks[i] = new LevelMeshChunk(node.Id, $"chunk/{typeTag}/v{variant}");
        }

        return meshChunks;
    }

    private static LevelSpawnPoint[] BuildSpawnPoints(
        LevelGenOptions options,
        IReadOnlyList<LevelNode> nodes,
        IReadOnlyDictionary<int, HashSet<int>> connectionsByNode)
    {
        var spawnPoints = new List<LevelSpawnPoint>(nodes.Count);
        bool hasPlayerStart = false;

        foreach (LevelNode node in nodes)
        {
            int degree = connectionsByNode[node.Id].Count;
            if (node.Id == 0)
            {
                spawnPoints.Add(new LevelSpawnPoint(node.Id, node.Position + Vector3.UnitY, "player_start"));
                hasPlayerStart = true;
                continue;
            }

            switch (node.Type)
            {
                case LevelNodeType.Room:
                    if (SampleNodeRoll(options.Seed, node.Id, 11u) < 0.20f + options.Density * 0.45f)
                    {
                        spawnPoints.Add(new LevelSpawnPoint(node.Id, node.Position + Vector3.UnitY, "player"));
                    }

                    if (SampleNodeRoll(options.Seed, node.Id, 13u) < 0.20f + options.Complexity * 0.50f)
                    {
                        spawnPoints.Add(new LevelSpawnPoint(node.Id, node.Position + Vector3.UnitY * 0.3f, "loot"));
                    }

                    break;
                case LevelNodeType.Junction:
                    spawnPoints.Add(new LevelSpawnPoint(node.Id, node.Position + Vector3.UnitY * 0.4f, "loot"));
                    if (options.Danger > 0.4f)
                    {
                        spawnPoints.Add(new LevelSpawnPoint(node.Id, node.Position + Vector3.UnitY * 0.6f, "danger"));
                    }

                    break;
                case LevelNodeType.Corridor:
                    if (degree <= 2 && SampleNodeRoll(options.Seed, node.Id, 17u) < options.Danger * 0.65f)
                    {
                        spawnPoints.Add(new LevelSpawnPoint(node.Id, node.Position + Vector3.UnitY * 0.5f, "danger_patrol"));
                    }

                    break;
                case LevelNodeType.Shaft:
                    spawnPoints.Add(new LevelSpawnPoint(node.Id, node.Position + Vector3.UnitY * 0.5f, "danger"));
                    if (SampleNodeRoll(options.Seed, node.Id, 19u) < options.Complexity * 0.45f)
                    {
                        spawnPoints.Add(new LevelSpawnPoint(node.Id, node.Position + Vector3.UnitY * 1.1f, "loot"));
                    }

                    break;
                case LevelNodeType.DeadEnd:
                    spawnPoints.Add(new LevelSpawnPoint(node.Id, node.Position + Vector3.UnitY * 0.45f, "danger"));
                    if (SampleNodeRoll(options.Seed, node.Id, 23u) < 0.30f + options.Complexity * 0.40f)
                    {
                        spawnPoints.Add(new LevelSpawnPoint(node.Id, node.Position + Vector3.UnitY * 0.2f, "loot_cache"));
                    }

                    break;
                default:
                    throw new InvalidDataException($"Unsupported level node type: {node.Type}.");
            }
        }

        if (!hasPlayerStart)
        {
            LevelNode start = nodes[0];
            spawnPoints.Insert(0, new LevelSpawnPoint(start.Id, start.Position + Vector3.UnitY, "player_start"));
        }

        return spawnPoints.ToArray();
    }

    private static void ConnectBidirectional(int a, int b, Dictionary<int, HashSet<int>> connections)
    {
        connections[a].Add(b);
        connections[b].Add(a);
    }

    private static int ChooseParentNodeId(
        Random rng,
        int nodeId,
        Dictionary<int, HashSet<int>> connectionsByNode,
        float complexity)
    {
        if (nodeId <= 1)
        {
            return 0;
        }

        float branchChance = 0.18f + complexity * 0.62f;
        if (rng.NextSingle() > branchChance)
        {
            return nodeId - 1;
        }

        var weightedCandidates = new List<int>();
        for (int candidate = 0; candidate < nodeId; candidate++)
        {
            int degree = connectionsByNode[candidate].Count;
            if (degree >= 5)
            {
                continue;
            }

            int weight = Math.Max(1, 6 - degree);
            for (int i = 0; i < weight; i++)
            {
                weightedCandidates.Add(candidate);
            }
        }

        if (weightedCandidates.Count == 0)
        {
            return nodeId - 1;
        }

        return weightedCandidates[rng.Next(weightedCandidates.Count)];
    }

    private static void TryAddExtraConnections(
        Random rng,
        int nodeId,
        IReadOnlyList<Vector3> positions,
        Dictionary<int, HashSet<int>> connectionsByNode,
        float density,
        float complexity)
    {
        float chance = 0.06f + density * 0.22f + complexity * 0.35f;
        int maxExtraLinks = complexity >= 0.75f ? 2 : 1;

        for (int linkIndex = 0; linkIndex < maxExtraLinks; linkIndex++)
        {
            if (rng.NextSingle() > chance)
            {
                break;
            }

            int target = ChooseExtraLinkTarget(rng, nodeId, positions, connectionsByNode, density, complexity);
            if (target < 0)
            {
                break;
            }

            ConnectBidirectional(target, nodeId, connectionsByNode);
            chance *= 0.45f;
        }
    }

    private static int ChooseExtraLinkTarget(
        Random rng,
        int nodeId,
        IReadOnlyList<Vector3> positions,
        Dictionary<int, HashSet<int>> connectionsByNode,
        float density,
        float complexity)
    {
        float baseStep = 2f + density * 4f;
        float maxDistance = baseStep * (1.6f + complexity * 1.2f);
        float maxDistanceSquared = maxDistance * maxDistance;

        var candidates = new List<int>();
        for (int candidate = 0; candidate < nodeId; candidate++)
        {
            if (connectionsByNode[nodeId].Contains(candidate))
            {
                continue;
            }

            if (connectionsByNode[candidate].Count >= 5)
            {
                continue;
            }

            Vector3 delta = positions[nodeId] - positions[candidate];
            if (delta.LengthSquared() <= maxDistanceSquared)
            {
                candidates.Add(candidate);
            }
        }

        if (candidates.Count == 0)
        {
            return -1;
        }

        return candidates[rng.Next(candidates.Count)];
    }

    private static LevelNodeType ChooseNodeType(Random rng, int index, int total, float danger, float complexity)
    {
        if (index == 0)
        {
            return LevelNodeType.Room;
        }

        if (index == total - 1)
        {
            return LevelNodeType.DeadEnd;
        }

        float roll = rng.NextSingle();
        float junctionThreshold = 0.08f + complexity * 0.32f;
        if (roll < junctionThreshold)
        {
            return LevelNodeType.Junction;
        }

        float shaftThreshold = junctionThreshold + 0.05f + danger * 0.35f;
        if (roll < shaftThreshold)
        {
            return LevelNodeType.Shaft;
        }

        float roomThreshold = shaftThreshold + 0.12f + (1f - complexity) * 0.26f;
        return roll < roomThreshold ? LevelNodeType.Room : LevelNodeType.Corridor;
    }

    private static Vector3 ChooseNodePosition(
        Random rng,
        int parentId,
        LevelNodeType type,
        IReadOnlyList<Vector3> existingPositions,
        int existingCount,
        float density,
        float danger)
    {
        float baseStep = 2f + density * 4f;
        float minDistanceSquared = (baseStep * 0.45f) * (baseStep * 0.45f);

        for (int attempt = 0; attempt < 8; attempt++)
        {
            Vector3 direction = type == LevelNodeType.Shaft
                ? ChooseVerticalDirection(rng, danger)
                : ChooseHorizontalDirection(rng);
            float step = type == LevelNodeType.Shaft
                ? baseStep * (0.7f + danger * 0.6f)
                : baseStep;
            Vector3 candidate = existingPositions[parentId] + direction * step;
            if (IsPositionFree(candidate, existingPositions, existingCount, minDistanceSquared))
            {
                return candidate;
            }
        }

        float deterministicOffset = (existingCount % 5) * 0.35f;
        return existingPositions[parentId] + new Vector3(baseStep + deterministicOffset, 0f, deterministicOffset);
    }

    private static bool IsPositionFree(
        Vector3 candidate,
        IReadOnlyList<Vector3> existingPositions,
        int existingCount,
        float minDistanceSquared)
    {
        for (int i = 0; i < existingCount; i++)
        {
            Vector3 delta = candidate - existingPositions[i];
            if (delta.LengthSquared() < minDistanceSquared)
            {
                return false;
            }
        }

        return true;
    }

    private static Vector3 ChooseHorizontalDirection(Random rng)
    {
        Vector3[] directions =
        [
            Vector3.UnitX,
            -Vector3.UnitX,
            Vector3.UnitZ,
            -Vector3.UnitZ
        ];

        return directions[rng.Next(directions.Length)];
    }

    private static Vector3 ChooseVerticalDirection(Random rng, float danger)
    {
        float upwardChance = 0.45f + danger * 0.35f;
        return rng.NextSingle() <= upwardChance
            ? Vector3.UnitY
            : -Vector3.UnitY;
    }

    private static float SampleNodeRoll(uint seed, int nodeId, uint salt)
    {
        uint value = seed ^ ((uint)nodeId * 747796405u) ^ salt;
        value ^= value >> 16;
        value *= 2246822519u;
        value ^= value >> 13;
        value *= 3266489917u;
        value ^= value >> 16;
        return (value & 0x00FFFFFFu) / 16777215.0f;
    }
}
