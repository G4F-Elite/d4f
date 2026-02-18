using Engine.Core.Handles;
using Engine.Procedural;
using Engine.Rendering;

namespace Engine.Tests.Procedural;

public sealed class ProceduralPipelineIntegrationTests
{
    [Fact]
    public void LevelToUploadPipeline_ShouldBeDeterministicAcrossRuns()
    {
        var options = new LevelGenOptions(
            Seed: 3301u,
            TargetNodes: 18,
            Density: 0.63f,
            Danger: 0.52f,
            Complexity: 0.71f);
        LevelGenResult level = LevelGenerator.Generate(options);
        LevelMeshChunk[] chunks = level.MeshChunks.Take(3).ToArray();

        var firstRendering = new RecordingRenderingFacade();
        var secondRendering = new RecordingRenderingFacade();

        IReadOnlyList<ProceduralChunkUploadResult> firstUploads = BuildAndUploadChunks(
            chunks,
            options.Seed,
            firstRendering);
        IReadOnlyList<ProceduralChunkUploadResult> secondUploads = BuildAndUploadChunks(
            chunks,
            options.Seed,
            secondRendering);

        Assert.Equal(chunks.Length, firstUploads.Count);
        Assert.Equal(chunks.Length, secondUploads.Count);
        Assert.Equal(firstRendering.MeshBlobs.Count, secondRendering.MeshBlobs.Count);
        Assert.Equal(firstRendering.MaterialBlobs.Count, secondRendering.MaterialBlobs.Count);
        Assert.Equal(firstRendering.TextureBlobs.Count, secondRendering.TextureBlobs.Count);

        for (int i = 0; i < firstRendering.MeshBlobs.Count; i++)
        {
            Assert.Equal(firstRendering.MeshBlobs[i], secondRendering.MeshBlobs[i]);
        }

        for (int i = 0; i < firstRendering.MaterialBlobs.Count; i++)
        {
            Assert.Equal(firstRendering.MaterialBlobs[i], secondRendering.MaterialBlobs[i]);
        }

        for (int i = 0; i < firstRendering.TextureBlobs.Count; i++)
        {
            Assert.Equal(firstRendering.TextureBlobs[i], secondRendering.TextureBlobs[i]);
        }

        for (int i = 0; i < firstUploads.Count; i++)
        {
            ProceduralChunkUploadResult upload = firstUploads[i];
            LevelMeshChunk chunk = chunks[i];
            LevelChunkTag tag = LevelChunkTag.Parse(chunk.MeshTag);
            string texturePrefix = $"proc/chunk/{tag.TypeTag}/v{tag.Variant}/n{chunk.NodeId}";

            Assert.True(upload.Mesh.IsValid);
            Assert.True(upload.Material.IsValid);
            Assert.True(upload.AlbedoTexture.IsValid);
            Assert.NotEmpty(upload.TexturesByKey);
            Assert.All(upload.TexturesByKey.Keys, key =>
                Assert.StartsWith(texturePrefix, key, StringComparison.Ordinal));
        }
    }

    private static IReadOnlyList<ProceduralChunkUploadResult> BuildAndUploadChunks(
        IReadOnlyList<LevelMeshChunk> chunks,
        uint seed,
        RecordingRenderingFacade rendering)
    {
        ArgumentNullException.ThrowIfNull(chunks);
        ArgumentNullException.ThrowIfNull(rendering);

        var uploaded = new List<ProceduralChunkUploadResult>(chunks.Count);
        foreach (LevelMeshChunk chunk in chunks)
        {
            ProceduralChunkContent content = ProceduralChunkContentFactory.Build(
                chunk,
                seed,
                surfaceWidth: 48,
                surfaceHeight: 48);
            uploaded.Add(ProceduralChunkRenderUploader.Upload(rendering, content));
        }

        return uploaded;
    }

    private sealed class RecordingRenderingFacade : IRenderingFacade
    {
        private ulong _nextHandle = 100u;

        public List<byte[]> MeshBlobs { get; } = [];

        public List<byte[]> TextureBlobs { get; } = [];

        public List<byte[]> MaterialBlobs { get; } = [];

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
            MeshBlobs.Add(blob.ToArray());
            return new MeshHandle(_nextHandle++);
        }

        public MeshHandle CreateMeshFromCpu(ReadOnlySpan<float> positions, ReadOnlySpan<uint> indices)
        {
            if (positions.IsEmpty || indices.IsEmpty)
            {
                throw new ArgumentException("Mesh CPU payload must be non-empty.");
            }

            return new MeshHandle(_nextHandle++);
        }

        public TextureHandle CreateTextureFromBlob(ReadOnlySpan<byte> blob)
        {
            TextureBlobs.Add(blob.ToArray());
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
                throw new ArgumentException("Texture CPU payload must be non-empty.");
            }

            return new TextureHandle(_nextHandle++);
        }

        public MaterialHandle CreateMaterialFromBlob(ReadOnlySpan<byte> blob)
        {
            MaterialBlobs.Add(blob.ToArray());
            return new MaterialHandle(_nextHandle++);
        }

        public void DestroyResource(ulong handle)
        {
        }

        public byte[] CaptureFrameRgba8(uint width, uint height, bool includeAlpha = true)
        {
            return new byte[checked((int)width * (int)height * 4)];
        }
    }
}
