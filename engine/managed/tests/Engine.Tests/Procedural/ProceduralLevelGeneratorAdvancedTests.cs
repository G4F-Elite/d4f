using Engine.Procedural;

namespace Engine.Tests.Procedural;

public sealed class ProceduralLevelGeneratorAdvancedTests
{
    [Fact]
    public void Generate_RejectsInvalidComplexity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LevelGenerator.Generate(new LevelGenOptions(Seed: 1u, TargetNodes: 16, Density: 0.5f, Danger: 0.3f, Complexity: -0.01f)));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LevelGenerator.Generate(new LevelGenOptions(Seed: 1u, TargetNodes: 16, Density: 0.5f, Danger: 0.3f, Complexity: 1.01f)));
    }

    [Fact]
    public void Generate_ProducesConnectedBidirectionalGraph()
    {
        LevelGenResult result = LevelGenerator.Generate(new LevelGenOptions(
            Seed: 77u,
            TargetNodes: 48,
            Density: 0.65f,
            Danger: 0.55f,
            Complexity: 0.8f));

        Assert.Equal(48, result.Graph.Nodes.Count);

        var visited = new HashSet<int>();
        var frontier = new Queue<int>();
        frontier.Enqueue(0);
        while (frontier.Count > 0)
        {
            int id = frontier.Dequeue();
            if (!visited.Add(id))
            {
                continue;
            }

            foreach (int connection in result.Graph.Nodes[id].Connections)
            {
                frontier.Enqueue(connection);
            }
        }

        Assert.Equal(result.Graph.Nodes.Count, visited.Count);

        foreach (LevelNode node in result.Graph.Nodes)
        {
            Assert.DoesNotContain(node.Id, node.Connections);

            foreach (int linkedId in node.Connections)
            {
                Assert.Contains(node.Id, result.Graph.Nodes[linkedId].Connections);
            }
        }
    }

    [Fact]
    public void Generate_HigherComplexityProducesMoreBranching()
    {
        LevelGenResult low = LevelGenerator.Generate(new LevelGenOptions(
            Seed: 99u,
            TargetNodes: 80,
            Density: 0.7f,
            Danger: 0.4f,
            Complexity: 0.05f));

        LevelGenResult high = LevelGenerator.Generate(new LevelGenOptions(
            Seed: 99u,
            TargetNodes: 80,
            Density: 0.7f,
            Danger: 0.4f,
            Complexity: 0.95f));

        int lowBranching = low.Graph.Nodes.Count(static node => node.Connections.Count >= 3);
        int highBranching = high.Graph.Nodes.Count(static node => node.Connections.Count >= 3);

        Assert.True(highBranching >= lowBranching);
        Assert.True(highBranching > 0);
    }

    [Fact]
    public void Generate_HigherDangerIncreasesShaftsAndDangerSpawns()
    {
        LevelGenResult low = LevelGenerator.Generate(new LevelGenOptions(
            Seed: 123u,
            TargetNodes: 64,
            Density: 0.6f,
            Danger: 0.1f,
            Complexity: 0.7f));

        LevelGenResult high = LevelGenerator.Generate(new LevelGenOptions(
            Seed: 123u,
            TargetNodes: 64,
            Density: 0.6f,
            Danger: 0.95f,
            Complexity: 0.7f));

        int lowShaftCount = low.Graph.Nodes.Count(static node => node.Type == LevelNodeType.Shaft);
        int highShaftCount = high.Graph.Nodes.Count(static node => node.Type == LevelNodeType.Shaft);
        int lowDangerSpawns = low.SpawnPoints.Count(static spawn => spawn.Category.Contains("danger", StringComparison.Ordinal));
        int highDangerSpawns = high.SpawnPoints.Count(static spawn => spawn.Category.Contains("danger", StringComparison.Ordinal));

        Assert.True(highShaftCount >= lowShaftCount);
        Assert.True(highDangerSpawns >= lowDangerSpawns);
    }

    [Fact]
    public void Generate_AssignsVariantMeshTagsAndPlayerStart()
    {
        LevelGenResult result = LevelGenerator.Generate(new LevelGenOptions(
            Seed: 555u,
            TargetNodes: 24,
            Density: 0.55f,
            Danger: 0.45f,
            Complexity: 0.6f));

        Assert.Equal(result.Graph.Nodes.Count, result.MeshChunks.Count);
        Assert.Contains(result.SpawnPoints, static spawn => spawn.Category == "player_start");

        foreach (LevelMeshChunk chunk in result.MeshChunks)
        {
            string[] parts = chunk.MeshTag.Split('/');
            Assert.Equal(3, parts.Length);
            Assert.Equal("chunk", parts[0]);
            Assert.StartsWith("v", parts[2], StringComparison.Ordinal);
            Assert.True(int.TryParse(parts[2][1..], out int variant));
            Assert.InRange(variant, 0, 3);
        }
    }
}
