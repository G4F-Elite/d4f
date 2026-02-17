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
    public void CreateCpuResources_ShouldReturn64BitHandlesFromNative()
    {
        var backend = new FakeNativeInteropApi
        {
            RendererMeshHandleToReturn = 0x1_0000_0101UL,
            RendererTextureHandleToReturn = 0x1_0000_0102UL
        };

        using var runtime = new NativeRuntime(backend);
        MeshHandle mesh = runtime.CreateMeshFromCpu(
            positions: [0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f],
            indices: [0u, 1u, 2u]);
        TextureHandle texture = runtime.CreateTextureFromCpu(
            width: 1u,
            height: 1u,
            rgba8: [16, 32, 48, 255]);

        Assert.Equal(backend.RendererMeshHandleToReturn, mesh.Value);
        Assert.Equal(backend.RendererTextureHandleToReturn, texture.Value);
        Assert.Equal(1, backend.CountCall("renderer_create_mesh_from_cpu"));
        Assert.Equal(1, backend.CountCall("renderer_create_texture_from_cpu"));
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
    public void CreateCpuResources_ShouldValidateInput()
    {
        var backend = new FakeNativeInteropApi();
        using var runtime = new NativeRuntime(backend);

        Assert.Throws<ArgumentException>(() => runtime.CreateMeshFromCpu([], [0u, 1u, 2u]));
        Assert.Throws<ArgumentException>(() => runtime.CreateMeshFromCpu([0f, 0f, 0f], []));
        Assert.Throws<ArgumentException>(() => runtime.CreateMeshFromCpu(
            [0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f],
            [0u, 1u, 9u]));
        Assert.Throws<ArgumentOutOfRangeException>(() => runtime.CreateTextureFromCpu(0u, 1u, [0, 0, 0, 255]));
        Assert.Throws<ArgumentOutOfRangeException>(() => runtime.CreateTextureFromCpu(1u, 0u, [0, 0, 0, 255]));
        Assert.Throws<ArgumentException>(() => runtime.CreateTextureFromCpu(2u, 1u, [0, 0, 0, 255]));
        Assert.Throws<ArgumentOutOfRangeException>(() => runtime.CreateTextureFromCpu(1u, 1u, [0, 0, 0, 255], strideBytes: 3u));
        Assert.Equal(0, backend.CountCall("renderer_create_mesh_from_cpu"));
        Assert.Equal(0, backend.CountCall("renderer_create_texture_from_cpu"));
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
    public void CreateCpuResource_ShouldThrowWhenNativeReturnsFailure()
    {
        var backend = new FakeNativeInteropApi
        {
            RendererCreateMeshFromCpuStatus = EngineNativeStatus.InvalidArgument
        };
        using var runtime = new NativeRuntime(backend);

        NativeCallException ex = Assert.Throws<NativeCallException>(() => runtime.CreateMeshFromCpu(
            [0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f],
            [0u, 1u, 2u]));
        Assert.Contains("renderer_create_mesh_from_cpu", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DestroyResource_ShouldValidateHandle()
    {
        var backend = new FakeNativeInteropApi();
        using var runtime = new NativeRuntime(backend);

        Assert.Throws<ArgumentOutOfRangeException>(() => runtime.DestroyResource(0u));
    }
}
