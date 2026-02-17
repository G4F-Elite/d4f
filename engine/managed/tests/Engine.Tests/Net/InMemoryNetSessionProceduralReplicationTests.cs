using System.IO;
using Engine.Net;

namespace Engine.Tests.Net;

public sealed class InMemoryNetSessionProceduralReplicationTests
{
    [Fact]
    public void ProceduralRecipeRef_ShouldValidateInput()
    {
        Assert.Throws<ArgumentException>(() =>
            new NetProceduralRecipeRef("", 1, 1, "abc"));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new NetProceduralRecipeRef("gen", 0, 1, "abc"));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new NetProceduralRecipeRef("gen", 1, 0, "abc"));
        Assert.Throws<ArgumentException>(() =>
            new NetProceduralRecipeRef("gen", 1, 1, ""));
        Assert.Throws<ArgumentException>(() =>
            new NetProceduralRecipeRef("gen", 1, 1, "hash", new Dictionary<string, string>
            {
                [" "] = "value"
            }));
        Assert.Throws<ArgumentNullException>(() =>
            new NetProceduralRecipeRef("gen", 1, 1, "hash", new Dictionary<string, string>
            {
                ["param"] = null!
            }));
    }

    [Fact]
    public void ProceduralRecipeRef_ShouldNormalizeAndSortParameters()
    {
        var recipe = new NetProceduralRecipeRef(
            generatorId: " level.generator ",
            generatorVersion: 2,
            recipeVersion: 7,
            recipeHash: " a1b2c3 ",
            parameters: new Dictionary<string, string>
            {
                [" room_count "] = " 18 ",
                ["danger"] = "0.5"
            });

        Assert.Equal("level.generator", recipe.GeneratorId);
        Assert.Equal("a1b2c3", recipe.RecipeHash);
        Assert.Equal(["danger", "room_count"], recipe.Parameters.Keys.ToArray());
        Assert.Equal("0.5", recipe.Parameters["danger"]);
        Assert.Equal("18", recipe.Parameters["room_count"]);
    }

    [Fact]
    public void Pump_ShouldReplicateProceduralRecipeReferenceInSnapshot()
    {
        InMemoryNetSession session = CreateSession();
        session.RegisterReplicatedComponent("transform");
        uint clientId = session.ConnectClient();

        var recipe = new NetProceduralRecipeRef(
            generatorId: "level.graph",
            generatorVersion: 4,
            recipeVersion: 3,
            recipeHash: "hash-0001",
            parameters: new Dictionary<string, string>
            {
                ["density"] = "0.7",
                ["danger"] = "0.4"
            });
        session.UpsertServerEntity(CreateEntity(100u, recipe));

        session.Pump();

        NetSnapshot snapshot = session.GetClientSnapshot(clientId);
        NetEntityState entity = Assert.Single(snapshot.Entities);
        NetProceduralRecipeRef actual = Assert.IsType<NetProceduralRecipeRef>(entity.ProceduralRecipe);
        Assert.Equal("level.graph", actual.GeneratorId);
        Assert.Equal(4, actual.GeneratorVersion);
        Assert.Equal(3, actual.RecipeVersion);
        Assert.Equal("hash-0001", actual.RecipeHash);
        Assert.Equal("0.7", actual.Parameters["density"]);
        Assert.Equal("0.4", actual.Parameters["danger"]);
    }

    [Fact]
    public void UpsertServerEntity_ShouldRejectTooLargeProceduralRecipeMetadata()
    {
        InMemoryNetSession session = CreateSession(new NetworkConfig(
            TickRateHz: 30,
            MaxPayloadBytes: 32,
            MaxRpcPerTickPerClient: 16,
            MaxEntitiesPerSnapshot: 128));
        session.RegisterReplicatedComponent("transform");

        var recipe = new NetProceduralRecipeRef(
            generatorId: "very-long-generator-id",
            generatorVersion: 1,
            recipeVersion: 1,
            recipeHash: new string('a', 64),
            parameters: new Dictionary<string, string>
            {
                ["density"] = "0.75"
            });

        Assert.Throws<InvalidDataException>(() => session.UpsertServerEntity(CreateEntity(1u, recipe)));
    }

    [Fact]
    public void Stats_ShouldIncludeProceduralRecipeMetadataInSnapshotSize()
    {
        NetPeerStats baseline = PumpSingleEntitySession(proceduralRecipe: null);
        NetPeerStats withRecipe = PumpSingleEntitySession(new NetProceduralRecipeRef(
            generatorId: "level.graph",
            generatorVersion: 1,
            recipeVersion: 1,
            recipeHash: "hash-123",
            parameters: new Dictionary<string, string>
            {
                ["rooms"] = "12",
                ["danger"] = "0.2"
            }));

        Assert.True(withRecipe.BytesSent > baseline.BytesSent);
        Assert.True(withRecipe.MessagesSent >= baseline.MessagesSent);
    }

    private static NetPeerStats PumpSingleEntitySession(NetProceduralRecipeRef? proceduralRecipe)
    {
        InMemoryNetSession session = CreateSession();
        session.RegisterReplicatedComponent("transform");
        _ = session.ConnectClient();
        session.UpsertServerEntity(CreateEntity(1u, proceduralRecipe));
        session.Pump();
        return session.GetServerStats();
    }

    private static InMemoryNetSession CreateSession(NetworkConfig? config = null)
    {
        return new InMemoryNetSession(config ?? new NetworkConfig());
    }

    private static NetEntityState CreateEntity(uint entityId, NetProceduralRecipeRef? proceduralRecipe)
    {
        return new NetEntityState(
            entityId: entityId,
            ownerClientId: null,
            proceduralSeed: 1234u,
            assetKey: $"asset:{entityId}",
            components: [new NetComponentState("transform", [1, 2, 3])],
            proceduralRecipe: proceduralRecipe);
    }
}
