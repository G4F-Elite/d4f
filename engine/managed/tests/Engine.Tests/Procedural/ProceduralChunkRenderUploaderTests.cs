using Engine.Core.Handles;
using Engine.Content;
using Engine.Procedural;
using Engine.Rendering;

namespace Engine.Tests.Procedural;

public sealed class ProceduralChunkRenderUploaderTests
{
    [Fact]
    public void Upload_ShouldCreateRenderInstanceAndTextureMap()
    {
        ProceduralChunkContent content = CreateChunkContent();
        var rendering = new RecordingRenderingFacade();

        ProceduralChunkUploadResult uploaded = ProceduralChunkRenderUploader.Upload(rendering, content);

        Assert.True(uploaded.Mesh.IsValid);
        Assert.True(uploaded.Material.IsValid);
        Assert.True(uploaded.AlbedoTexture.IsValid);
        Assert.Equal(content.MaterialBundle.Textures.Count, uploaded.TexturesByKey.Count);
        Assert.Equal(uploaded.Mesh, uploaded.Instance.Mesh);
        Assert.Equal(uploaded.Material, uploaded.Instance.Material);
        Assert.Equal(uploaded.AlbedoTexture, uploaded.Instance.Texture);
        Assert.Single(rendering.MeshBlobs);
        Assert.Equal(content.MaterialBundle.Textures.Count, rendering.TextureBlobs.Count);
        Assert.Single(rendering.MaterialBlobs);

        MeshBlobData meshBlob = MeshBlobCodec.Read(rendering.MeshBlobs[0]);
        Assert.True(meshBlob.VertexCount > 0);
        Assert.NotEmpty(meshBlob.VertexStreams);
        Assert.NotEmpty(meshBlob.IndexData);
        Assert.NotEmpty(meshBlob.Submeshes);

        TextureBlobData textureBlob = TextureBlobCodec.Read(rendering.TextureBlobs[0]);
        Assert.Equal(TextureBlobFormat.Rgba8Unorm, textureBlob.Format);
        Assert.True(textureBlob.Width > 0);
        Assert.True(textureBlob.Height > 0);

        MaterialBlobData materialBlob = MaterialBlobCodec.Read(rendering.MaterialBlobs[0]);
        Assert.Equal(MaterialTemplateId.DffLitPbr.ToString(), materialBlob.TemplateId);
        Assert.NotEmpty(materialBlob.TextureReferences);
    }

    [Fact]
    public void Upload_ShouldSerializeBlobsDeterministically()
    {
        ProceduralChunkContent content = CreateChunkContent();
        var firstRendering = new RecordingRenderingFacade();
        var secondRendering = new RecordingRenderingFacade();

        _ = ProceduralChunkRenderUploader.Upload(firstRendering, content);
        _ = ProceduralChunkRenderUploader.Upload(secondRendering, content);

        Assert.Equal(firstRendering.MeshBlobs.Count, secondRendering.MeshBlobs.Count);
        Assert.Equal(firstRendering.MaterialBlobs.Count, secondRendering.MaterialBlobs.Count);
        Assert.Equal(firstRendering.TextureBlobs.Count, secondRendering.TextureBlobs.Count);
        Assert.Equal(firstRendering.MeshBlobs[0], secondRendering.MeshBlobs[0]);
        Assert.Equal(firstRendering.MaterialBlobs[0], secondRendering.MaterialBlobs[0]);
        for (int i = 0; i < firstRendering.TextureBlobs.Count; i++)
        {
            Assert.Equal(firstRendering.TextureBlobs[i], secondRendering.TextureBlobs[i]);
        }
    }

    [Fact]
    public void Upload_WithCpuOptions_ShouldUseCpuCreationApis()
    {
        ProceduralChunkContent content = CreateChunkContent();
        var rendering = new RecordingRenderingFacade();

        ProceduralChunkUploadResult uploaded = ProceduralChunkRenderUploader.Upload(
            rendering,
            content,
            new ProceduralChunkUploadOptions(UseCpuMeshPath: true, UseCpuTexturePath: true));

        Assert.True(uploaded.Mesh.IsValid);
        Assert.True(uploaded.Material.IsValid);
        Assert.Empty(rendering.MeshBlobs);
        Assert.Empty(rendering.TextureBlobs);
        Assert.Single(rendering.MeshCpuUploads);
        Assert.Equal(content.MaterialBundle.Textures.Count, rendering.TextureCpuUploads.Count);
        Assert.Single(rendering.MaterialBlobs);
    }

    [Fact]
    public void UploadResult_Destroy_ShouldDestroyAllResourceHandles()
    {
        ProceduralChunkContent content = CreateChunkContent();
        var rendering = new RecordingRenderingFacade();

        ProceduralChunkUploadResult uploaded = ProceduralChunkRenderUploader.Upload(rendering, content);
        uploaded.Destroy(rendering);

        int expectedUniqueHandleCount = uploaded.TexturesByKey.Count + 2; // mesh + material + textures
        Assert.Equal(expectedUniqueHandleCount, rendering.DestroyedHandles.Count);
        Assert.Contains(uploaded.Mesh.Value, rendering.DestroyedHandles);
        Assert.Contains(uploaded.Material.Value, rendering.DestroyedHandles);
        Assert.Contains(uploaded.AlbedoTexture.Value, rendering.DestroyedHandles);
    }

    [Fact]
    public void Upload_ShouldValidateInput()
    {
        ProceduralChunkContent content = CreateChunkContent();
        var rendering = new RecordingRenderingFacade();
        var instance = new RenderMeshInstance(new MeshHandle(1), new MaterialHandle(2), new TextureHandle(3));

        Assert.Throws<ArgumentNullException>(() => ProceduralChunkRenderUploader.Upload(null!, content));
        Assert.Throws<ArgumentNullException>(() => ProceduralChunkRenderUploader.Upload(rendering, null!));
        Assert.Throws<ArgumentNullException>(() => new ProceduralChunkUploadResult(
            instance,
            new MeshHandle(1),
            new MaterialHandle(2),
            new TextureHandle(3),
            null!).Validate());
    }

    private static ProceduralChunkContent CreateChunkContent()
    {
        LevelMeshChunk chunk = new(NodeId: 12, MeshTag: "chunk/room/v2");
        return ProceduralChunkContentFactory.Build(chunk, seed: 456u, surfaceWidth: 32, surfaceHeight: 32);
    }

    private sealed class RecordingRenderingFacade : IRenderingFacade
    {
        private ulong _nextHandle = 100u;

        public List<byte[]> MeshBlobs { get; } = [];

        public List<byte[]> TextureBlobs { get; } = [];

        public List<byte[]> MaterialBlobs { get; } = [];

        public List<(float[] Positions, uint[] Indices)> MeshCpuUploads { get; } = [];

        public List<(uint Width, uint Height, byte[] Rgba8, uint StrideBytes)> TextureCpuUploads { get; } = [];

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
            MeshBlobs.Add(blob.ToArray());
            return new MeshHandle(_nextHandle++);
        }

        public MeshHandle CreateMeshFromCpu(ReadOnlySpan<float> positions, ReadOnlySpan<uint> indices)
        {
            if (positions.Length == 0 || indices.Length == 0)
            {
                throw new ArgumentException("Mesh CPU payload must be non-empty.");
            }

            MeshCpuUploads.Add((positions.ToArray(), indices.ToArray()));
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
            if (width == 0u || height == 0u || rgba8.Length == 0)
            {
                throw new ArgumentException("Texture CPU payload must be valid.");
            }

            TextureCpuUploads.Add((width, height, rgba8.ToArray(), strideBytes));
            return new TextureHandle(_nextHandle++);
        }

        public MaterialHandle CreateMaterialFromBlob(ReadOnlySpan<byte> blob)
        {
            MaterialBlobs.Add(blob.ToArray());
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
