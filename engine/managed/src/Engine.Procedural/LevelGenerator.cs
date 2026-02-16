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
    float Danger)
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

        var rng = new Random(unchecked((int)options.Seed));
        var nodes = new List<LevelNode>(options.TargetNodes);
        var connectionsByNode = new Dictionary<int, HashSet<int>>();

        Vector3 currentPos = Vector3.Zero;
        for (int id = 0; id < options.TargetNodes; id++)
        {
            LevelNodeType type = ChooseNodeType(rng, id, options.TargetNodes, options.Danger);
            if (id > 0)
            {
                currentPos += ChooseDirection(rng) * (2f + options.Density * 4f);
            }

            nodes.Add(new LevelNode(id, type, currentPos, Array.Empty<int>()));
            connectionsByNode[id] = [];

            if (id > 0)
            {
                ConnectBidirectional(id - 1, id, connectionsByNode);
            }

            if (id > 2 && rng.NextSingle() < options.Density * 0.25f)
            {
                int linkTo = rng.Next(0, id - 1);
                ConnectBidirectional(linkTo, id, connectionsByNode);
            }
        }

        var finalizedNodes = nodes
            .Select(node => node with { Connections = connectionsByNode[node.Id].OrderBy(static x => x).ToArray() })
            .ToArray();

        var meshChunks = finalizedNodes
            .Select(static node => new LevelMeshChunk(node.Id, $"chunk/{node.Type.ToString().ToLowerInvariant()}"))
            .ToArray();

        var spawnPoints = new List<LevelSpawnPoint>();
        foreach (LevelNode node in finalizedNodes)
        {
            if (node.Type is LevelNodeType.Room or LevelNodeType.Junction)
            {
                spawnPoints.Add(new LevelSpawnPoint(node.Id, node.Position + Vector3.UnitY, "player"));
            }

            if (node.Type is LevelNodeType.DeadEnd or LevelNodeType.Shaft)
            {
                spawnPoints.Add(new LevelSpawnPoint(node.Id, node.Position + Vector3.UnitY * 0.5f, "danger"));
            }
        }

        return new LevelGenResult(new LevelGraph(finalizedNodes), meshChunks, spawnPoints.ToArray());
    }

    private static void ConnectBidirectional(int a, int b, Dictionary<int, HashSet<int>> connections)
    {
        connections[a].Add(b);
        connections[b].Add(a);
    }

    private static LevelNodeType ChooseNodeType(Random rng, int index, int total, float danger)
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
        if (roll < 0.15f)
        {
            return LevelNodeType.Junction;
        }

        if (roll < 0.15f + danger * 0.35f)
        {
            return LevelNodeType.Shaft;
        }

        return roll < 0.7f ? LevelNodeType.Corridor : LevelNodeType.Room;
    }

    private static Vector3 ChooseDirection(Random rng)
    {
        Vector3[] directions =
        [
            Vector3.UnitX,
            -Vector3.UnitX,
            Vector3.UnitZ,
            -Vector3.UnitZ
        ];

        return directions[rng.Next(0, directions.Length)];
    }
}
