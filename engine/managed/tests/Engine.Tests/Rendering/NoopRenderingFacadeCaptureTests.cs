using Engine.Core.Handles;
using Engine.Rendering;

namespace Engine.Tests.Rendering;

public sealed class NoopRenderingFacadeCaptureTests
{
    [Fact]
    public void ResourceBlobApi_ShouldCreateAndDestroyHandles()
    {
        MeshHandle mesh = NoopRenderingFacade.Instance.CreateMeshFromBlob([1, 2, 3]);
        TextureHandle texture = NoopRenderingFacade.Instance.CreateTextureFromBlob([4, 5, 6]);
        MaterialHandle material = NoopRenderingFacade.Instance.CreateMaterialFromBlob([7, 8, 9]);

        Assert.True(mesh.IsValid);
        Assert.True(texture.IsValid);
        Assert.True(material.IsValid);
        Assert.NotEqual(mesh.Value, texture.Value);
        Assert.NotEqual(mesh.Value, material.Value);
        Assert.NotEqual(texture.Value, material.Value);

        NoopRenderingFacade.Instance.DestroyResource(mesh.Value);
        NoopRenderingFacade.Instance.DestroyResource(texture.Value);
        NoopRenderingFacade.Instance.DestroyResource(material.Value);

        Assert.Throws<InvalidOperationException>(() => NoopRenderingFacade.Instance.DestroyResource(mesh.Value));
    }

    [Fact]
    public void ResourceBlobApi_ShouldRejectEmptyPayload()
    {
        Assert.Throws<ArgumentException>(() => NoopRenderingFacade.Instance.CreateMeshFromBlob([]));
        Assert.Throws<ArgumentException>(() => NoopRenderingFacade.Instance.CreateTextureFromBlob([]));
        Assert.Throws<ArgumentException>(() => NoopRenderingFacade.Instance.CreateMaterialFromBlob([]));
    }

    [Fact]
    public void ResourceCpuApi_ShouldCreateAndDestroyHandles()
    {
        MeshHandle mesh = NoopRenderingFacade.Instance.CreateMeshFromCpu(
            positions: [0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f],
            indices: [0u, 1u, 2u]);
        TextureHandle texture = NoopRenderingFacade.Instance.CreateTextureFromCpu(
            width: 1u,
            height: 1u,
            rgba8: [10, 20, 30, 255]);

        Assert.True(mesh.IsValid);
        Assert.True(texture.IsValid);
        NoopRenderingFacade.Instance.DestroyResource(mesh.Value);
        NoopRenderingFacade.Instance.DestroyResource(texture.Value);
    }

    [Fact]
    public void ResourceCpuApi_ShouldValidateInput()
    {
        Assert.Throws<ArgumentException>(() => NoopRenderingFacade.Instance.CreateMeshFromCpu([], [0u, 1u, 2u]));
        Assert.Throws<ArgumentException>(() => NoopRenderingFacade.Instance.CreateMeshFromCpu([0f, 0f, 0f], []));
        Assert.Throws<ArgumentException>(() => NoopRenderingFacade.Instance.CreateMeshFromCpu(
            [0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f],
            [0u, 1u, 9u]));
        Assert.Throws<ArgumentOutOfRangeException>(() => NoopRenderingFacade.Instance.CreateTextureFromCpu(0u, 1u, [0, 0, 0, 255]));
        Assert.Throws<ArgumentOutOfRangeException>(() => NoopRenderingFacade.Instance.CreateTextureFromCpu(1u, 0u, [0, 0, 0, 255]));
        Assert.Throws<ArgumentOutOfRangeException>(() => NoopRenderingFacade.Instance.CreateTextureFromCpu(1u, 1u, [0, 0, 0, 255], strideBytes: 3u));
    }

    [Fact]
    public void FrameStats_ShouldTrackDrawTrianglesUploadsAndGpuMemory()
    {
        NoopRenderingFacade facade = NoopRenderingFacade.Instance;
        MeshHandle mesh = default;
        TextureHandle texture = default;
        MaterialHandle material = default;

        try
        {
            mesh = facade.CreateMeshFromCpu(
                positions:
                [
                    0f, 0f, 0f,
                    1f, 0f, 0f,
                    0f, 1f, 0f,
                    1f, 1f, 0f
                ],
                indices: [0u, 1u, 2u, 2u, 1u, 3u]);
            texture = facade.CreateTextureFromBlob([1, 2, 3, 4]);
            material = facade.CreateMaterialFromBlob([5, 6, 7]);
            ulong expectedResourceBytes = checked((ulong)(12 * sizeof(float) + 6 * sizeof(uint) + 4 + 3));

            using (facade.BeginFrame(1024, 64))
            {
                var packet = new RenderPacket(
                    0,
                    [new DrawCommand(new EntityId(1, 1u), mesh, material, texture)]);
                facade.Submit(packet);
            }

            facade.Present();
            RenderingFrameStats firstFrame = facade.GetLastFrameStats();
            Assert.Equal((uint)1, firstFrame.DrawItemCount);
            Assert.Equal((uint)0, firstFrame.UiItemCount);
            Assert.Equal((ulong)2, firstFrame.TriangleCount);
            Assert.Equal(expectedResourceBytes, firstFrame.UploadBytes);
            Assert.Equal(expectedResourceBytes, firstFrame.GpuMemoryBytes);
            Assert.Equal(RenderingBackendKind.Noop, firstFrame.BackendKind);

            using (facade.BeginFrame(512, 16))
            {
                facade.Submit(RenderPacket.Empty(1));
            }

            facade.Present();
            RenderingFrameStats secondFrame = facade.GetLastFrameStats();
            Assert.Equal((ulong)0, secondFrame.UploadBytes);
            Assert.Equal(expectedResourceBytes, secondFrame.GpuMemoryBytes);
            Assert.Equal(RenderingBackendKind.Noop, secondFrame.BackendKind);

            facade.DestroyResource(mesh.Value);
            mesh = default;
            facade.DestroyResource(texture.Value);
            texture = default;
            facade.DestroyResource(material.Value);
            material = default;

            using (facade.BeginFrame(512, 16))
            {
                facade.Submit(RenderPacket.Empty(2));
            }

            facade.Present();
            RenderingFrameStats thirdFrame = facade.GetLastFrameStats();
            Assert.Equal((ulong)0, thirdFrame.UploadBytes);
            Assert.Equal((ulong)0, thirdFrame.GpuMemoryBytes);
            Assert.Equal(RenderingBackendKind.Noop, thirdFrame.BackendKind);
        }
        finally
        {
            DestroyResourceIfValid(mesh.Value);
            DestroyResourceIfValid(texture.Value);
            DestroyResourceIfValid(material.Value);
        }
    }

    [Fact]
    public void CaptureFrameRgba8_ReturnsDeterministicRgbaPayload()
    {
        byte[] first = NoopRenderingFacade.Instance.CaptureFrameRgba8(4u, 3u, includeAlpha: true);
        byte[] second = NoopRenderingFacade.Instance.CaptureFrameRgba8(4u, 3u, includeAlpha: true);

        Assert.Equal(4 * 3 * 4, first.Length);
        Assert.Equal(first, second);
        Assert.Equal((byte)220, first[3]);
    }

    [Fact]
    public void CaptureFrameRgba8_UsesOpaqueAlpha_WhenDisabled()
    {
        byte[] rgba = NoopRenderingFacade.Instance.CaptureFrameRgba8(2u, 2u, includeAlpha: false);

        Assert.Equal((byte)255, rgba[3]);
        Assert.Equal((byte)255, rgba[7]);
        Assert.Equal((byte)255, rgba[11]);
        Assert.Equal((byte)255, rgba[15]);
    }

    [Fact]
    public void CaptureFrameRgba8_Throws_WhenSizeIsInvalid()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NoopRenderingFacade.Instance.CaptureFrameRgba8(0u, 1u));
        Assert.Throws<ArgumentOutOfRangeException>(() => NoopRenderingFacade.Instance.CaptureFrameRgba8(1u, 0u));
    }

    private static void DestroyResourceIfValid(ulong handle)
    {
        if (handle != 0u)
        {
            NoopRenderingFacade.Instance.DestroyResource(handle);
        }
    }
}
