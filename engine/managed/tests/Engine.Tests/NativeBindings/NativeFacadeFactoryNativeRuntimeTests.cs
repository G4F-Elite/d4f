using System;
using System.Collections.Generic;
using Engine.Core.Handles;
using Engine.ECS;
using Engine.NativeBindings;
using Engine.NativeBindings.Internal.Interop;
using Engine.Rendering;
using Xunit;

namespace Engine.Tests.NativeBindings;

public sealed class NativeFacadeFactoryNativeRuntimeTests
{
    [Fact]
    public void NativeRuntimeUsesExpectedLifecycleAndCallOrder()
    {
        var backend = new FakeNativeInteropApi();
        var world = new World();
        var entity = world.CreateEntity();
        using var nativeSet = NativeFacadeFactory.CreateNativeFacadeSet(backend);

        Assert.True(nativeSet.Platform.PumpEvents());

        using var frameArena = nativeSet.Rendering.BeginFrame(1024, 64);
        nativeSet.Rendering.Submit(CreatePacket(entity));
        nativeSet.Rendering.Present();

        nativeSet.Physics.SyncToPhysics(world);
        nativeSet.Physics.Step(TimeSpan.FromSeconds(1.0 / 60.0));
        nativeSet.Physics.SyncFromPhysics(world);

        nativeSet.Dispose();

        Assert.Equal(
            [
                "engine_create",
                "engine_get_renderer",
                "engine_get_physics",
                "engine_pump_events",
                "renderer_begin_frame",
                "renderer_submit",
                "renderer_present",
                "physics_sync_from_world",
                "physics_step",
                "physics_sync_to_world",
                "engine_destroy"
            ],
            backend.Calls);
    }

    [Fact]
    public void NativeRuntimeThrowsOnNativeStatusFailure()
    {
        var backend = new FakeNativeInteropApi
        {
            RendererSubmitStatus = EngineNativeStatus.InvalidState
        };

        var world = new World();
        var entity = world.CreateEntity();
        using var nativeSet = NativeFacadeFactory.CreateNativeFacadeSet(backend);
        using var frameArena = nativeSet.Rendering.BeginFrame(1024, 64);

        var exception = Assert.Throws<NativeCallException>(() => nativeSet.Rendering.Submit(CreatePacket(entity)));

        Assert.Contains("renderer_submit", exception.Message, StringComparison.Ordinal);
        Assert.Contains("InvalidState", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NativeRuntimeDisposeMakesFacadesUnusable()
    {
        var backend = new FakeNativeInteropApi();
        var nativeSet = NativeFacadeFactory.CreateNativeFacadeSet(backend);

        nativeSet.Dispose();

        Assert.Throws<ObjectDisposedException>(() => nativeSet.Platform.PumpEvents());
        Assert.Equal(1, backend.CountCall("engine_destroy"));
    }

    [Fact]
    public void NativeRuntimeThrowsWhenEngineCreationFails()
    {
        var backend = new FakeNativeInteropApi
        {
            EngineCreateStatus = EngineNativeStatus.VersionMismatch
        };

        var exception = Assert.Throws<NativeCallException>(() => NativeFacadeFactory.CreateNativeFacadeSet(backend));

        Assert.Contains("engine_create", exception.Message, StringComparison.Ordinal);
        Assert.Contains("VersionMismatch", exception.Message, StringComparison.Ordinal);
        Assert.Equal(["engine_create"], backend.Calls);
    }

    private static RenderPacket CreatePacket(EntityId entity)
    {
        var drawCommand = new DrawCommand(entity, new MeshHandle(10), new MaterialHandle(20), new TextureHandle(30));
        return new RenderPacket(0, [drawCommand]);
    }

    private sealed class FakeNativeInteropApi : INativeInteropApi
    {
        private readonly IntPtr _engineHandle = new(101);
        private readonly IntPtr _rendererHandle = new(202);
        private readonly IntPtr _physicsHandle = new(303);

        public List<string> Calls { get; } = [];

        public EngineNativeStatus EngineCreateStatus { get; set; } = EngineNativeStatus.Ok;

        public EngineNativeStatus EngineDestroyStatus { get; set; } = EngineNativeStatus.Ok;

        public EngineNativeStatus EnginePumpEventsStatus { get; set; } = EngineNativeStatus.Ok;

        public EngineNativeStatus EngineGetRendererStatus { get; set; } = EngineNativeStatus.Ok;

        public EngineNativeStatus EngineGetPhysicsStatus { get; set; } = EngineNativeStatus.Ok;

        public EngineNativeStatus RendererBeginFrameStatus { get; set; } = EngineNativeStatus.Ok;

        public EngineNativeStatus RendererSubmitStatus { get; set; } = EngineNativeStatus.Ok;

        public EngineNativeStatus RendererPresentStatus { get; set; } = EngineNativeStatus.Ok;

        public EngineNativeStatus PhysicsStepStatus { get; set; } = EngineNativeStatus.Ok;

        public EngineNativeStatus PhysicsSyncFromWorldStatus { get; set; } = EngineNativeStatus.Ok;

        public EngineNativeStatus PhysicsSyncToWorldStatus { get; set; } = EngineNativeStatus.Ok;

        public EngineNativeStatus EngineCreate(in EngineNativeCreateDesc createDesc, out IntPtr engine)
        {
            Calls.Add("engine_create");
            engine = EngineCreateStatus == EngineNativeStatus.Ok ? _engineHandle : IntPtr.Zero;
            return EngineCreateStatus;
        }

        public EngineNativeStatus EngineDestroy(IntPtr engine)
        {
            Calls.Add("engine_destroy");
            return EngineDestroyStatus;
        }

        public EngineNativeStatus EnginePumpEvents(
            IntPtr engine,
            out EngineNativeInputSnapshot input,
            out EngineNativeWindowEvents windowEvents)
        {
            Calls.Add("engine_pump_events");

            input = new EngineNativeInputSnapshot
            {
                FrameIndex = 1,
                ButtonsMask = 0,
                MouseX = 0,
                MouseY = 0
            };

            windowEvents = new EngineNativeWindowEvents
            {
                ShouldClose = 0,
                Width = 1280,
                Height = 720
            };

            return EnginePumpEventsStatus;
        }

        public EngineNativeStatus EngineGetRenderer(IntPtr engine, out IntPtr renderer)
        {
            Calls.Add("engine_get_renderer");
            renderer = EngineGetRendererStatus == EngineNativeStatus.Ok ? _rendererHandle : IntPtr.Zero;
            return EngineGetRendererStatus;
        }

        public EngineNativeStatus EngineGetPhysics(IntPtr engine, out IntPtr physics)
        {
            Calls.Add("engine_get_physics");
            physics = EngineGetPhysicsStatus == EngineNativeStatus.Ok ? _physicsHandle : IntPtr.Zero;
            return EngineGetPhysicsStatus;
        }

        public EngineNativeStatus RendererBeginFrame(
            IntPtr renderer,
            nuint requestedBytes,
            nuint alignment,
            out IntPtr frameMemory)
        {
            Calls.Add("renderer_begin_frame");
            frameMemory = RendererBeginFrameStatus == EngineNativeStatus.Ok ? new IntPtr(404) : IntPtr.Zero;
            return RendererBeginFrameStatus;
        }

        public EngineNativeStatus RendererSubmit(IntPtr renderer, in EngineNativeRenderPacket packet)
        {
            Calls.Add("renderer_submit");
            return RendererSubmitStatus;
        }

        public EngineNativeStatus RendererPresent(IntPtr renderer)
        {
            Calls.Add("renderer_present");
            return RendererPresentStatus;
        }

        public EngineNativeStatus PhysicsStep(IntPtr physics, double deltaSeconds)
        {
            Calls.Add("physics_step");
            return PhysicsStepStatus;
        }

        public EngineNativeStatus PhysicsSyncFromWorld(IntPtr physics, IntPtr writes, uint writeCount)
        {
            Calls.Add("physics_sync_from_world");
            return PhysicsSyncFromWorldStatus;
        }

        public EngineNativeStatus PhysicsSyncToWorld(
            IntPtr physics,
            IntPtr reads,
            uint readCapacity,
            out uint readCount)
        {
            Calls.Add("physics_sync_to_world");
            readCount = 0;
            return PhysicsSyncToWorldStatus;
        }

        public int CountCall(string callName)
        {
            var count = 0;
            foreach (var call in Calls)
            {
                if (string.Equals(call, callName, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }
    }
}
