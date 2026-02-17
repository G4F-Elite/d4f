using System.Globalization;
using Engine.Core.Handles;
using Engine.Net;
using Engine.Procedural;
using Engine.Rendering;

namespace Engine.Tests.Procedural;

public sealed class NetProceduralChunkReplicatorTests
{
    [Fact]
    public void Apply_ShouldSpawnEntities_AndReuseCachedAssetByAssetKey()
    {
        var rendering = new RecordingRenderingFacade();
        using var replicator = new NetProceduralChunkReplicator(rendering);
        NetProceduralRecipeRef recipe = CreateChunkRecipe(nodeId: 5, meshTag: "chunk/room/v2", recipeHash: "hash-room");
        NetSnapshot snapshot = CreateSnapshot(
            tick: 1,
            CreateEntity(1u, "asset:room:shared", seed: 42u, recipe),
            CreateEntity(2u, "asset:room:shared", seed: 42u, recipe));

        NetProceduralChunkApplyResult result = replicator.Apply(snapshot);

        Assert.Equal([1u, 2u], result.SpawnedEntityIds);
        Assert.Empty(result.UpdatedEntityIds);
        Assert.Empty(result.DespawnedEntityIds);
        Assert.Equal(1, result.CachedAssetCount);
        Assert.Equal(2, result.ActiveEntities.Count);
        Assert.Equal(1, rendering.MeshCreateCount);
        Assert.Equal(1, rendering.MaterialCreateCount);
        Assert.True(rendering.TextureCreateCount >= 1);

        RenderMeshInstance firstInstance = result.ActiveEntities.Single(static x => x.EntityId == 1u).Instance;
        RenderMeshInstance secondInstance = result.ActiveEntities.Single(static x => x.EntityId == 2u).Instance;
        Assert.Equal(firstInstance.Mesh, secondInstance.Mesh);
        Assert.Equal(firstInstance.Material, secondInstance.Material);
        Assert.Equal(firstInstance.Texture, secondInstance.Texture);
    }

    [Fact]
    public void Apply_ShouldUpdateEntity_WhenAssetKeyChanges_AndReleasePreviousResources()
    {
        var rendering = new RecordingRenderingFacade();
        using var replicator = new NetProceduralChunkReplicator(rendering);
        NetSnapshot firstSnapshot = CreateSnapshot(
            tick: 1,
            CreateEntity(
                entityId: 100u,
                assetKey: "asset:first",
                seed: 1000u,
                CreateChunkRecipe(nodeId: 11, meshTag: "chunk/corridor/v1", recipeHash: "h1")));

        NetProceduralChunkApplyResult firstResult = replicator.Apply(firstSnapshot);
        Assert.Equal([100u], firstResult.SpawnedEntityIds);
        Assert.Empty(firstResult.UpdatedEntityIds);
        Assert.Empty(firstResult.DespawnedEntityIds);
        Assert.Equal(1, firstResult.CachedAssetCount);

        NetSnapshot secondSnapshot = CreateSnapshot(
            tick: 2,
            CreateEntity(
                entityId: 100u,
                assetKey: "asset:second",
                seed: 1001u,
                CreateChunkRecipe(nodeId: 12, meshTag: "chunk/shaft/v0", recipeHash: "h2")));

        NetProceduralChunkApplyResult secondResult = replicator.Apply(secondSnapshot);

        Assert.Empty(secondResult.SpawnedEntityIds);
        Assert.Equal([100u], secondResult.UpdatedEntityIds);
        Assert.Empty(secondResult.DespawnedEntityIds);
        Assert.Equal(1, secondResult.CachedAssetCount);
        Assert.True(rendering.DestroyedHandles.Count > 0);
        Assert.Equal(2, rendering.MeshCreateCount);
        Assert.Equal(2, rendering.MaterialCreateCount);
    }

    [Fact]
    public void Apply_ShouldRespectReferenceCounting_WhenSharedAssetDespawns()
    {
        var rendering = new RecordingRenderingFacade();
        using var replicator = new NetProceduralChunkReplicator(rendering);
        NetProceduralRecipeRef recipe = CreateChunkRecipe(nodeId: 3, meshTag: "chunk/junction/v0", recipeHash: "junction");

        _ = replicator.Apply(CreateSnapshot(
            tick: 1,
            CreateEntity(1u, "asset:junction", seed: 5u, recipe),
            CreateEntity(2u, "asset:junction", seed: 5u, recipe)));
        Assert.Empty(rendering.DestroyedHandles);

        NetProceduralChunkApplyResult afterFirstDespawn = replicator.Apply(CreateSnapshot(
            tick: 2,
            CreateEntity(1u, "asset:junction", seed: 5u, recipe)));
        Assert.Equal([2u], afterFirstDespawn.DespawnedEntityIds);
        Assert.Equal(1, afterFirstDespawn.CachedAssetCount);
        Assert.Empty(rendering.DestroyedHandles);

        NetProceduralChunkApplyResult afterSecondDespawn = replicator.Apply(CreateSnapshot(tick: 3));
        Assert.Equal([1u], afterSecondDespawn.DespawnedEntityIds);
        Assert.Equal(0, afterSecondDespawn.CachedAssetCount);
        Assert.True(rendering.DestroyedHandles.Count > 0);
    }

    [Fact]
    public void Apply_ShouldDespawnTrackedEntity_WhenRecipeMissingOrUnsupported()
    {
        var rendering = new RecordingRenderingFacade();
        using var replicator = new NetProceduralChunkReplicator(rendering);
        NetProceduralRecipeRef chunkRecipe = CreateChunkRecipe(nodeId: 8, meshTag: "chunk/deadend/v3", recipeHash: "deadend");

        _ = replicator.Apply(CreateSnapshot(
            tick: 1,
            CreateEntity(7u, "asset:deadend", seed: 77u, chunkRecipe)));

        NetProceduralChunkApplyResult missingRecipeResult = replicator.Apply(CreateSnapshot(
            tick: 2,
            CreateEntity(7u, "asset:deadend", seed: 77u, recipe: null)));
        Assert.Equal([7u], missingRecipeResult.DespawnedEntityIds);
        Assert.Empty(missingRecipeResult.ActiveEntities);

        NetProceduralRecipeRef unsupportedRecipe = new(
            generatorId: "proc/other/content",
            generatorVersion: 1,
            recipeVersion: 1,
            recipeHash: "other",
            parameters: new Dictionary<string, string>
            {
                ["meshTag"] = "chunk/room/v0",
                ["nodeId"] = "7"
            });

        NetProceduralChunkApplyResult unsupportedResult = replicator.Apply(CreateSnapshot(
            tick: 3,
            CreateEntity(7u, "asset:deadend", seed: 77u, unsupportedRecipe)));
        Assert.Empty(unsupportedResult.SpawnedEntityIds);
        Assert.Empty(unsupportedResult.UpdatedEntityIds);
        Assert.Empty(unsupportedResult.ActiveEntities);
    }

    [Fact]
    public void Apply_ShouldFail_WhenChunkRecipeParametersInvalid()
    {
        var rendering = new RecordingRenderingFacade();
        using var replicator = new NetProceduralChunkReplicator(rendering);

        NetProceduralRecipeRef missingMeshTag = new(
            generatorId: "proc/chunk/content",
            generatorVersion: 1,
            recipeVersion: 1,
            recipeHash: "invalid",
            parameters: new Dictionary<string, string>
            {
                ["nodeId"] = "10"
            });
        NetSnapshot missingMeshSnapshot = CreateSnapshot(
            tick: 1,
            CreateEntity(1u, "asset:invalid-mesh", seed: 1u, missingMeshTag));
        Assert.Throws<InvalidDataException>(() => replicator.Apply(missingMeshSnapshot));

        NetProceduralRecipeRef invalidNodeId = new(
            generatorId: "proc/chunk/content",
            generatorVersion: 1,
            recipeVersion: 1,
            recipeHash: "invalid-node",
            parameters: new Dictionary<string, string>
            {
                ["meshTag"] = "chunk/room/v0",
                ["nodeId"] = "not-an-int"
            });
        NetSnapshot invalidNodeSnapshot = CreateSnapshot(
            tick: 2,
            CreateEntity(2u, "asset:invalid-node", seed: 1u, invalidNodeId));
        Assert.Throws<InvalidDataException>(() => replicator.Apply(invalidNodeSnapshot));
    }

    [Fact]
    public void Apply_ShouldFail_WhenSameAssetKeyHasDifferentProceduralMetadata()
    {
        var rendering = new RecordingRenderingFacade();
        using var replicator = new NetProceduralChunkReplicator(rendering);
        NetSnapshot conflictingSnapshot = CreateSnapshot(
            tick: 1,
            CreateEntity(
                entityId: 1u,
                assetKey: "asset:conflict",
                seed: 10u,
                CreateChunkRecipe(nodeId: 1, meshTag: "chunk/room/v0", recipeHash: "same")),
            CreateEntity(
                entityId: 2u,
                assetKey: "asset:conflict",
                seed: 11u,
                CreateChunkRecipe(nodeId: 1, meshTag: "chunk/room/v0", recipeHash: "same")));

        Assert.Throws<InvalidDataException>(() => replicator.Apply(conflictingSnapshot));
    }

    [Fact]
    public void Dispose_ShouldReleaseAllCachedResources_AndRejectFurtherApplyCalls()
    {
        var rendering = new RecordingRenderingFacade();
        var replicator = new NetProceduralChunkReplicator(rendering);
        _ = replicator.Apply(CreateSnapshot(
            tick: 1,
            CreateEntity(
                entityId: 42u,
                assetKey: "asset:dispose",
                seed: 101u,
                CreateChunkRecipe(nodeId: 2, meshTag: "chunk/shaft/v1", recipeHash: "dispose"))));

        replicator.Dispose();

        Assert.True(rendering.DestroyedHandles.Count > 0);
        Assert.Throws<ObjectDisposedException>(() => replicator.Apply(CreateSnapshot(tick: 2)));
    }

    private static NetSnapshot CreateSnapshot(long tick, params NetEntityState[] entities)
    {
        return new NetSnapshot(tick, entities);
    }

    private static NetEntityState CreateEntity(
        uint entityId,
        string assetKey,
        ulong seed,
        NetProceduralRecipeRef? recipe)
    {
        return new NetEntityState(
            entityId: entityId,
            ownerClientId: null,
            proceduralSeed: seed,
            assetKey: assetKey,
            components: [new NetComponentState("transform", [1, 2, 3])],
            proceduralRecipe: recipe);
    }

    private static NetProceduralRecipeRef CreateChunkRecipe(
        int nodeId,
        string meshTag,
        string recipeHash,
        int generatorVersion = 1,
        int recipeVersion = 1,
        int? surfaceWidth = null,
        int? surfaceHeight = null)
    {
        var parameters = new Dictionary<string, string>
        {
            ["meshTag"] = meshTag,
            ["nodeId"] = nodeId.ToString(CultureInfo.InvariantCulture)
        };

        if (surfaceWidth.HasValue)
        {
            parameters["surfaceWidth"] = surfaceWidth.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (surfaceHeight.HasValue)
        {
            parameters["surfaceHeight"] = surfaceHeight.Value.ToString(CultureInfo.InvariantCulture);
        }

        return new NetProceduralRecipeRef(
            generatorId: "proc/chunk/content",
            generatorVersion: generatorVersion,
            recipeVersion: recipeVersion,
            recipeHash: recipeHash,
            parameters: parameters);
    }

    private sealed class RecordingRenderingFacade : IRenderingFacade
    {
        private ulong _nextHandle = 1000u;

        public int MeshCreateCount { get; private set; }

        public int TextureCreateCount { get; private set; }

        public int MaterialCreateCount { get; private set; }

        public HashSet<ulong> DestroyedHandles { get; } = [];

        public FrameArena BeginFrame(int requestedBytes, int alignment)
        {
            return new FrameArena(requestedBytes, alignment);
        }

        public void Submit(RenderPacket packet)
        {
            ArgumentNullException.ThrowIfNull(packet);
        }

        public void Present()
        {
        }

        public RenderingFrameStats GetLastFrameStats() => RenderingFrameStats.Empty;

        public MeshHandle CreateMeshFromBlob(ReadOnlySpan<byte> blob)
        {
            if (blob.IsEmpty)
            {
                throw new ArgumentException("Mesh blob cannot be empty.", nameof(blob));
            }

            MeshCreateCount++;
            return new MeshHandle(_nextHandle++);
        }

        public MeshHandle CreateMeshFromCpu(ReadOnlySpan<float> positions, ReadOnlySpan<uint> indices)
        {
            if (positions.IsEmpty || indices.IsEmpty)
            {
                throw new ArgumentException("Mesh CPU data cannot be empty.");
            }

            MeshCreateCount++;
            return new MeshHandle(_nextHandle++);
        }

        public TextureHandle CreateTextureFromBlob(ReadOnlySpan<byte> blob)
        {
            if (blob.IsEmpty)
            {
                throw new ArgumentException("Texture blob cannot be empty.", nameof(blob));
            }

            TextureCreateCount++;
            return new TextureHandle(_nextHandle++);
        }

        public TextureHandle CreateTextureFromCpu(
            uint width,
            uint height,
            ReadOnlySpan<byte> rgba8,
            uint strideBytes = 0)
        {
            if (width == 0u || height == 0u || rgba8.IsEmpty)
            {
                throw new ArgumentException("Texture CPU data is invalid.");
            }

            TextureCreateCount++;
            return new TextureHandle(_nextHandle++);
        }

        public MaterialHandle CreateMaterialFromBlob(ReadOnlySpan<byte> blob)
        {
            if (blob.IsEmpty)
            {
                throw new ArgumentException("Material blob cannot be empty.", nameof(blob));
            }

            MaterialCreateCount++;
            return new MaterialHandle(_nextHandle++);
        }

        public void DestroyResource(ulong handle)
        {
            DestroyedHandles.Add(handle);
        }

        public byte[] CaptureFrameRgba8(uint width, uint height, bool includeAlpha = true)
        {
            return new byte[checked((int)width * (int)height * 4)];
        }
    }
}
