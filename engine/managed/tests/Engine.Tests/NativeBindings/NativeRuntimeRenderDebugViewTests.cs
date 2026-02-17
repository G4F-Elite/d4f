using Engine.Core.Handles;
using Engine.ECS;
using Engine.NativeBindings;
using Engine.Rendering;

namespace Engine.Tests.NativeBindings;

public sealed class NativeRuntimeRenderDebugViewTests
{
    [Fact]
    public void NativeRuntimeSubmit_PropagatesRenderDebugViewModeToInteropPacket()
    {
        var backend = new FakeNativeInteropApi();
        var world = new World();
        EntityId entity = world.CreateEntity();
        using var nativeSet = NativeFacadeFactory.CreateNativeFacadeSet(backend);
        using var frameArena = nativeSet.Rendering.BeginFrame(1024, 64);

        var draw = new DrawCommand(entity, new MeshHandle(10), new MaterialHandle(20), new TextureHandle(30));
        var packet = new RenderPacket(0, [draw], Array.Empty<UiDrawCommand>(), RenderDebugViewMode.Albedo);

        nativeSet.Rendering.Submit(packet);

        Assert.Equal((byte)RenderDebugViewMode.Albedo, backend.LastRendererSubmitPacket.DebugViewMode);
        Assert.Equal((byte)0, backend.LastRendererSubmitPacket.Reserved0);
        Assert.Equal((byte)0, backend.LastRendererSubmitPacket.Reserved1);
        Assert.Equal((byte)0, backend.LastRendererSubmitPacket.Reserved2);
    }
}
