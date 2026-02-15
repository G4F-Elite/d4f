using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Engine.Core.Handles;
using Engine.ECS;
using Engine.NativeBindings;
using Engine.NativeBindings.Internal.Interop;
using Engine.Physics;
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
        var frame0 = nativeSet.Timing.NextFrameTiming();
        var frame1 = nativeSet.Timing.NextFrameTiming();
        Assert.Equal(0, frame0.FrameNumber);
        Assert.Equal(1, frame1.FrameNumber);
        Assert.True(frame1.TotalTime >= frame0.TotalTime);

        using var frameArena = nativeSet.Rendering.BeginFrame(1024, 64);
        Assert.Equal(backend.RendererBeginFrameMemory, frameArena.BasePointer);
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
    public void NativeRuntimePhysicsSync_UsesPhysicsBodyComponents()
    {
        var backend = new FakeNativeInteropApi();
        var world = new World();
        var syncedEntity = world.CreateEntity();
        var ignoredEntity = world.CreateEntity();

        world.AddComponent(
            syncedEntity,
            new PhysicsBody(
                new BodyHandle(101),
                PhysicsBodyType.Kinematic,
                new PhysicsCollider(
                    ColliderShapeType.Capsule,
                    new Vector3(0.4f, 2.0f, 0.4f),
                    isTrigger: true,
                    new PhysicsMaterial(0.25f, 0.75f)),
                new Vector3(1.0f, 2.0f, 3.0f),
                Quaternion.Identity,
                new Vector3(4.0f, 5.0f, 6.0f),
                new Vector3(7.0f, 8.0f, 9.0f),
                isActive: true));
        world.AddComponent(ignoredEntity, new DummyComponent(13));

        backend.PhysicsReadsToReturn =
        [
            new EngineNativeBodyRead
            {
                Body = 101,
                Position0 = 10.0f,
                Position1 = 20.0f,
                Position2 = 30.0f,
                Rotation0 = 0.0f,
                Rotation1 = 0.0f,
                Rotation2 = 0.0f,
                Rotation3 = 1.0f,
                LinearVelocity0 = 40.0f,
                LinearVelocity1 = 50.0f,
                LinearVelocity2 = 60.0f,
                AngularVelocity0 = 70.0f,
                AngularVelocity1 = 80.0f,
                AngularVelocity2 = 90.0f,
                IsActive = 1
            }
        ];

        using var nativeSet = NativeFacadeFactory.CreateNativeFacadeSet(backend);

        nativeSet.Physics.SyncToPhysics(world);
        nativeSet.Physics.SyncFromPhysics(world);

        Assert.Equal((uint)1, backend.LastPhysicsWriteCount);
        Assert.True(backend.LastPhysicsWrite.HasValue);
        Assert.Equal((ulong)101, backend.LastPhysicsWrite.Value.Body);
        Assert.Equal(1.0f, backend.LastPhysicsWrite.Value.Position0);
        Assert.Equal(9.0f, backend.LastPhysicsWrite.Value.AngularVelocity2);
        Assert.Equal((byte)PhysicsBodyType.Kinematic, backend.LastPhysicsWrite.Value.BodyType);
        Assert.Equal((byte)ColliderShapeType.Capsule, backend.LastPhysicsWrite.Value.ColliderShape);
        Assert.Equal((byte)1, backend.LastPhysicsWrite.Value.IsTrigger);
        Assert.Equal(0.4f, backend.LastPhysicsWrite.Value.ColliderDimensions0);
        Assert.Equal(2.0f, backend.LastPhysicsWrite.Value.ColliderDimensions1);
        Assert.Equal(0.4f, backend.LastPhysicsWrite.Value.ColliderDimensions2);
        Assert.Equal(0.25f, backend.LastPhysicsWrite.Value.Friction);
        Assert.Equal(0.75f, backend.LastPhysicsWrite.Value.Restitution);

        Assert.True(world.TryGetComponent(syncedEntity, out PhysicsBody updated));
        Assert.Equal(new Vector3(10.0f, 20.0f, 30.0f), updated.Position);
        Assert.Equal(new Vector3(40.0f, 50.0f, 60.0f), updated.LinearVelocity);
        Assert.Equal(new Vector3(70.0f, 80.0f, 90.0f), updated.AngularVelocity);
        Assert.True(updated.IsActive);
    }

    [Fact]
    public void NativeRuntimePhysicsRaycast_UsesInteropAndMapsHit()
    {
        var backend = new FakeNativeInteropApi
        {
            PhysicsRaycastHitToReturn = new EngineNativeRaycastHit
            {
                HasHit = 1,
                IsTrigger = 1,
                Body = 77,
                Distance = 12.5f,
                Point0 = 1.0f,
                Point1 = 2.0f,
                Point2 = 3.0f,
                Normal0 = 0.0f,
                Normal1 = 1.0f,
                Normal2 = 0.0f
            }
        };

        using var nativeSet = NativeFacadeFactory.CreateNativeFacadeSet(backend);
        var query = new PhysicsRaycastQuery(
            new Vector3(-1.0f, 0.0f, 0.0f),
            new Vector3(1.0f, 0.0f, 0.0f),
            100.0f,
            includeTriggers: true);

        bool hasHit = nativeSet.Physics.Raycast(query, out PhysicsRaycastHit hit);

        Assert.True(hasHit);
        Assert.Equal(new BodyHandle(77), hit.Body);
        Assert.Equal(12.5f, hit.Distance);
        Assert.Equal(new Vector3(1.0f, 2.0f, 3.0f), hit.Point);
        Assert.Equal(new Vector3(0.0f, 1.0f, 0.0f), hit.Normal);
        Assert.True(hit.IsTrigger);

        Assert.True(backend.LastPhysicsRaycastQuery.HasValue);
        EngineNativeRaycastQuery nativeQuery = backend.LastPhysicsRaycastQuery.Value;
        Assert.Equal(-1.0f, nativeQuery.Origin0);
        Assert.Equal(1.0f, nativeQuery.Direction0);
        Assert.Equal(100.0f, nativeQuery.MaxDistance);
        Assert.Equal((byte)1, nativeQuery.IncludeTriggers);
    }

    [Fact]
    public void NativeRuntimePhysicsRaycast_ReturnsFalseWhenNoHit()
    {
        var backend = new FakeNativeInteropApi
        {
            PhysicsRaycastHitToReturn = new EngineNativeRaycastHit
            {
                HasHit = 0
            }
        };

        using var nativeSet = NativeFacadeFactory.CreateNativeFacadeSet(backend);
        bool hasHit = nativeSet.Physics.Raycast(
            new PhysicsRaycastQuery(Vector3.Zero, Vector3.UnitY, 5.0f),
            out PhysicsRaycastHit hit);

        Assert.False(hasHit);
        Assert.Equal(default, hit);
    }

    [Fact]
    public void NativeRuntimeSubmit_UsesPacketNativePointersWhenProvided()
    {
        var backend = new FakeNativeInteropApi();
        using var nativeSet = NativeFacadeFactory.CreateNativeFacadeSet(backend);
        using var frameArena = nativeSet.Rendering.BeginFrame(1024, 64);

        var drawBuffer = Marshal.AllocHGlobal(Marshal.SizeOf<NativeDrawItem>());
        var uiBuffer = Marshal.AllocHGlobal(Marshal.SizeOf<NativeUiDrawItem>());

        try
        {
            var packet = RenderPacket.CreateNative(
                0,
                Array.Empty<DrawCommand>(),
                Array.Empty<UiDrawCommand>(),
                drawBuffer,
                1,
                uiBuffer,
                1);

            nativeSet.Rendering.Submit(packet);

            Assert.Equal(drawBuffer, backend.LastRendererSubmitPacket.DrawItems);
            Assert.Equal((uint)1, backend.LastRendererSubmitPacket.DrawItemCount);
            Assert.Equal(uiBuffer, backend.LastRendererSubmitPacket.UiItems);
            Assert.Equal((uint)1, backend.LastRendererSubmitPacket.UiItemCount);
        }
        finally
        {
            Marshal.FreeHGlobal(drawBuffer);
            Marshal.FreeHGlobal(uiBuffer);
        }
    }

    [Fact]
    public void NativeRuntimeSubmit_FallsBackToManagedCommandsWhenNativePointersMissing()
    {
        var backend = new FakeNativeInteropApi();
        var world = new World();
        var entity = world.CreateEntity();
        using var nativeSet = NativeFacadeFactory.CreateNativeFacadeSet(backend);
        using var frameArena = nativeSet.Rendering.BeginFrame(1024, 64);

        var draw = new DrawCommand(entity, new MeshHandle(10), new MaterialHandle(20), new TextureHandle(30));
        var ui = new UiDrawCommand(new TextureHandle(40), 5, 6, 7, 8);
        var packet = new RenderPacket(0, [draw], [ui]);

        nativeSet.Rendering.Submit(packet);

        Assert.NotEqual(IntPtr.Zero, backend.LastRendererSubmitPacket.DrawItems);
        Assert.Equal((uint)1, backend.LastRendererSubmitPacket.DrawItemCount);
        Assert.NotEqual(IntPtr.Zero, backend.LastRendererSubmitPacket.UiItems);
        Assert.Equal((uint)1, backend.LastRendererSubmitPacket.UiItemCount);

        Assert.True(backend.LastSubmittedDrawItem.HasValue);
        var submittedDraw = backend.LastSubmittedDrawItem.Value;
        Assert.Equal((ulong)10, submittedDraw.Mesh);
        Assert.Equal((ulong)20, submittedDraw.Material);
        Assert.Equal(1.0f, submittedDraw.World00);
        Assert.Equal(1.0f, submittedDraw.World11);
        Assert.Equal(1.0f, submittedDraw.World22);
        Assert.Equal(1.0f, submittedDraw.World33);

        Assert.True(backend.LastSubmittedUiItem.HasValue);
        var submittedUi = backend.LastSubmittedUiItem.Value;
        Assert.Equal((ulong)40, submittedUi.Texture);
        Assert.Equal((uint)5, submittedUi.VertexOffset);
        Assert.Equal((uint)6, submittedUi.VertexCount);
        Assert.Equal((uint)7, submittedUi.IndexOffset);
        Assert.Equal((uint)8, submittedUi.IndexCount);
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
}
