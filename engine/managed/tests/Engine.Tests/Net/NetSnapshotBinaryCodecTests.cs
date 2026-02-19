using Engine.Net;

namespace Engine.Tests.Net;

public sealed class NetSnapshotBinaryCodecTests
{
    [Fact]
    public void EncodeDecode_RoundTrip_ShouldPreserveSnapshotData()
    {
        var snapshot = new NetSnapshot(
            tick: 42,
            entities:
            [
                new NetEntityState(
                    entityId: 1u,
                    ownerClientId: 5u,
                    proceduralSeed: 12345UL,
                    assetKey: "props/crate",
                    components:
                    [
                        new NetComponentState("Transform", [1, 2, 3]),
                        new NetComponentState("Health", [100])
                    ],
                    proceduralRecipe: new NetProceduralRecipeRef(
                        generatorId: "level.scatter",
                        generatorVersion: 2,
                        recipeVersion: 8,
                        recipeHash: "abc123",
                        parameters: new Dictionary<string, string>
                        {
                            ["density"] = "0.35",
                            ["radius"] = "12"
                        })),
                new NetEntityState(
                    entityId: 2u,
                    ownerClientId: null,
                    proceduralSeed: 8UL,
                    assetKey: "npc/worker",
                    components:
                    [
                        new NetComponentState("Transform", [9, 9, 9])
                    ])
            ]);

        byte[] payload = NetSnapshotBinaryCodec.Encode(snapshot);
        NetSnapshot decoded = NetSnapshotBinaryCodec.Decode(payload);

        AssertSnapshotsEqual(snapshot, decoded);
    }

    [Fact]
    public void Decode_ShouldFail_WhenMagicIsInvalid()
    {
        NetSnapshot snapshot = new(
            tick: 1,
            entities:
            [
                new NetEntityState(1u, null, 0UL, "entity", [new NetComponentState("Transform", [1])])
            ]);
        byte[] payload = NetSnapshotBinaryCodec.Encode(snapshot);
        payload[0] ^= 0x5A;

        Assert.Throws<InvalidDataException>(() => NetSnapshotBinaryCodec.Decode(payload));
    }

    [Fact]
    public void Decode_ShouldFail_WhenPayloadIsTruncated()
    {
        NetSnapshot snapshot = new(
            tick: 1,
            entities:
            [
                new NetEntityState(1u, 2u, 5UL, "entity", [new NetComponentState("Transform", [1, 2, 3])])
            ]);
        byte[] payload = NetSnapshotBinaryCodec.Encode(snapshot);
        byte[] truncated = payload[..(payload.Length - 3)];

        Assert.ThrowsAny<Exception>(() => NetSnapshotBinaryCodec.Decode(truncated));
    }

    private static void AssertSnapshotsEqual(NetSnapshot expected, NetSnapshot actual)
    {
        Assert.Equal(expected.Tick, actual.Tick);
        Assert.Equal(expected.Entities.Count, actual.Entities.Count);

        for (int entityIndex = 0; entityIndex < expected.Entities.Count; entityIndex++)
        {
            NetEntityState expectedEntity = expected.Entities[entityIndex];
            NetEntityState actualEntity = actual.Entities[entityIndex];
            Assert.Equal(expectedEntity.EntityId, actualEntity.EntityId);
            Assert.Equal(expectedEntity.OwnerClientId, actualEntity.OwnerClientId);
            Assert.Equal(expectedEntity.ProceduralSeed, actualEntity.ProceduralSeed);
            Assert.Equal(expectedEntity.AssetKey, actualEntity.AssetKey);

            Assert.Equal(expectedEntity.Components.Count, actualEntity.Components.Count);
            for (int componentIndex = 0; componentIndex < expectedEntity.Components.Count; componentIndex++)
            {
                NetComponentState expectedComponent = expectedEntity.Components[componentIndex];
                NetComponentState actualComponent = actualEntity.Components[componentIndex];
                Assert.Equal(expectedComponent.ComponentId, actualComponent.ComponentId);
                Assert.Equal(expectedComponent.Payload, actualComponent.Payload);
            }

            if (expectedEntity.ProceduralRecipe is null)
            {
                Assert.Null(actualEntity.ProceduralRecipe);
            }
            else
            {
                NetProceduralRecipeRef expectedRecipe = expectedEntity.ProceduralRecipe;
                NetProceduralRecipeRef actualRecipe = Assert.IsType<NetProceduralRecipeRef>(actualEntity.ProceduralRecipe);
                Assert.Equal(expectedRecipe.GeneratorId, actualRecipe.GeneratorId);
                Assert.Equal(expectedRecipe.GeneratorVersion, actualRecipe.GeneratorVersion);
                Assert.Equal(expectedRecipe.RecipeVersion, actualRecipe.RecipeVersion);
                Assert.Equal(expectedRecipe.RecipeHash, actualRecipe.RecipeHash);
                Assert.Equal(expectedRecipe.Parameters, actualRecipe.Parameters);
            }
        }
    }
}
