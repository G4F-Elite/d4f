using Engine.Core.Handles;
using Engine.NativeBindings.Internal;
using Engine.NativeBindings.Internal.Interop;

namespace Engine.Tests.NativeBindings;

public sealed class NativeRuntimeResourceInteropTests
{
    [Fact]
    public void CreateBlobResources_ShouldReturn64BitHandlesFromNative()
    {
        var backend = new FakeNativeInteropApi
        {
            RendererMeshHandleToReturn = 0x1_0000_0001UL,
            RendererTextureHandleToReturn = 0x1_0000_0002UL,
            RendererMaterialHandleToReturn = 0x1_0000_0003UL
        };

        using var runtime = new NativeRuntime(backend);

        MeshHandle mesh = runtime.CreateMeshFromBlob([1, 2, 3]);
        TextureHandle texture = runtime.CreateTextureFromBlob([4, 5, 6]);
        MaterialHandle material = runtime.CreateMaterialFromBlob([7, 8, 9]);

        Assert.Equal(backend.RendererMeshHandleToReturn, mesh.Value);
        Assert.Equal(backend.RendererTextureHandleToReturn, texture.Value);
        Assert.Equal(backend.RendererMaterialHandleToReturn, material.Value);
        Assert.Equal(1, backend.CountCall("renderer_create_mesh_from_blob"));
        Assert.Equal(1, backend.CountCall("renderer_create_texture_from_blob"));
        Assert.Equal(1, backend.CountCall("renderer_create_material_from_blob"));
    }

    [Fact]
    public void CreateBlobResources_ShouldValidateInput()
    {
        var backend = new FakeNativeInteropApi();
        using var runtime = new NativeRuntime(backend);

        Assert.Throws<ArgumentException>(() => runtime.CreateMeshFromBlob([]));
        Assert.Throws<ArgumentException>(() => runtime.CreateTextureFromBlob([]));
        Assert.Throws<ArgumentException>(() => runtime.CreateMaterialFromBlob([]));
        Assert.Equal(0, backend.CountCall("renderer_create_mesh_from_blob"));
        Assert.Equal(0, backend.CountCall("renderer_create_texture_from_blob"));
        Assert.Equal(0, backend.CountCall("renderer_create_material_from_blob"));
    }

    [Fact]
    public void DestroyResource_ShouldForwardHandleToNative()
    {
        var backend = new FakeNativeInteropApi();
        using var runtime = new NativeRuntime(backend);

        runtime.DestroyResource(0x1_0000_0009UL);

        Assert.Equal(1, backend.CountCall("renderer_destroy_resource"));
        Assert.Equal(0x1_0000_0009UL, backend.LastDestroyedRendererResource);
    }

    [Fact]
    public void CreateBlobResource_ShouldThrowWhenNativeReturnsFailure()
    {
        var backend = new FakeNativeInteropApi
        {
            RendererCreateMeshFromBlobStatus = EngineNativeStatus.InvalidArgument
        };
        using var runtime = new NativeRuntime(backend);

        NativeCallException ex = Assert.Throws<NativeCallException>(() => runtime.CreateMeshFromBlob([1, 2]));
        Assert.Contains("renderer_create_mesh_from_blob", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DestroyResource_ShouldValidateHandle()
    {
        var backend = new FakeNativeInteropApi();
        using var runtime = new NativeRuntime(backend);

        Assert.Throws<ArgumentOutOfRangeException>(() => runtime.DestroyResource(0u));
    }
}
