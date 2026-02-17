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
}
