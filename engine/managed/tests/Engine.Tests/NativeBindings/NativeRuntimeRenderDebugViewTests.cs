using Engine.Core.Handles;
using Engine.ECS;
using Engine.NativeBindings;
using Engine.Rendering;

namespace Engine.Tests.NativeBindings;

public sealed class NativeRuntimeRenderDebugViewTests
{
    [Theory]
    [InlineData(RenderDebugViewMode.Albedo)]
    [InlineData(RenderDebugViewMode.Roughness)]
    [InlineData(RenderDebugViewMode.AmbientOcclusion)]
    public void NativeRuntimeSubmit_PropagatesRenderDebugViewModeToInteropPacket(RenderDebugViewMode debugViewMode)
    {
        var backend = new FakeNativeInteropApi();
        var world = new World();
        EntityId entity = world.CreateEntity();
        using var nativeSet = NativeFacadeFactory.CreateNativeFacadeSet(backend);
        using var frameArena = nativeSet.Rendering.BeginFrame(1024, 64);

        var draw = new DrawCommand(entity, new MeshHandle(10), new MaterialHandle(20), new TextureHandle(30));
        var packet = new RenderPacket(0, [draw], Array.Empty<UiDrawCommand>(), debugViewMode);

        nativeSet.Rendering.Submit(packet);

        Assert.Equal((byte)debugViewMode, backend.LastRendererSubmitPacket.DebugViewMode);
        Assert.Equal((byte)0, backend.LastRendererSubmitPacket.Reserved0);
        Assert.Equal((byte)0, backend.LastRendererSubmitPacket.Reserved1);
        Assert.Equal((byte)0, backend.LastRendererSubmitPacket.Reserved2);
    }

    [Theory]
    [InlineData(RenderFeatureFlags.DisableAutoExposure, 0x01)]
    [InlineData(RenderFeatureFlags.DisableJitterEffects, 0x02)]
    [InlineData(RenderFeatureFlags.DisableAutoExposure | RenderFeatureFlags.DisableJitterEffects, 0x03)]
    public void NativeRuntimeSubmit_PropagatesRenderFeatureFlagsToInteropPacket(
        RenderFeatureFlags featureFlags,
        byte expectedReserved0)
    {
        var backend = new FakeNativeInteropApi();
        var world = new World();
        EntityId entity = world.CreateEntity();
        using var nativeSet = NativeFacadeFactory.CreateNativeFacadeSet(backend);
        using var frameArena = nativeSet.Rendering.BeginFrame(1024, 64);

        var draw = new DrawCommand(entity, new MeshHandle(10), new MaterialHandle(20), new TextureHandle(30));
        var packet = new RenderPacket(
            0,
            [draw],
            Array.Empty<UiDrawCommand>(),
            RenderDebugViewMode.None,
            featureFlags);

        nativeSet.Rendering.Submit(packet);

        Assert.Equal((byte)RenderDebugViewMode.None, backend.LastRendererSubmitPacket.DebugViewMode);
        Assert.Equal(expectedReserved0, backend.LastRendererSubmitPacket.Reserved0);
        Assert.Equal((byte)0, backend.LastRendererSubmitPacket.Reserved1);
        Assert.Equal((byte)0, backend.LastRendererSubmitPacket.Reserved2);
    }
}
