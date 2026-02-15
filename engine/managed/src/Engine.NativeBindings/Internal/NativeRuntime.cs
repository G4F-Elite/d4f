using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Engine.Core.Handles;
using Engine.Core.Timing;
using Engine.ECS;
using Engine.NativeBindings.Internal.Interop;
using Engine.Physics;
using Engine.Rendering;

namespace Engine.NativeBindings.Internal;

internal sealed partial class NativeRuntime
    : INativePlatformApi, INativeTimingApi, INativePhysicsApi, INativeRenderingApi, IDisposable
{
    private readonly INativeInteropApi _interop;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private TimeSpan _previousElapsed = TimeSpan.Zero;
    private long _frameNumber;
    private IntPtr _engine;
    private IntPtr _renderer;
    private IntPtr _physics;
    private bool _disposed;

    public NativeRuntime(INativeInteropApi interop)
    {
        _interop = interop ?? throw new ArgumentNullException(nameof(interop));

        var createDesc = new EngineNativeCreateDesc
        {
            ApiVersion = EngineNativeConstants.ApiVersion,
            UserData = IntPtr.Zero
        };

        NativeStatusGuard.ThrowIfFailed(_interop.EngineCreate(in createDesc, out _engine), "engine_create");

        try
        {
            NativeStatusGuard.ThrowIfFailed(_interop.EngineGetRenderer(_engine, out _renderer), "engine_get_renderer");
            NativeStatusGuard.ThrowIfFailed(_interop.EngineGetPhysics(_engine, out _physics), "engine_get_physics");
        }
        catch (Exception initializationException)
        {
            var destroyFailure = TryDestroyEngine();
            if (destroyFailure is null)
            {
                throw;
            }

            throw new AggregateException(initializationException, destroyFailure);
        }
    }

    public bool PumpEvents()
    {
        ThrowIfDisposed();

        NativeStatusGuard.ThrowIfFailed(
            _interop.EnginePumpEvents(_engine, out _, out var windowEvents),
            "engine_pump_events");

        return windowEvents.ShouldClose == 0;
    }

    public FrameTiming NextFrameTiming()
    {
        ThrowIfDisposed();

        var now = _clock.Elapsed;
        var delta = now - _previousElapsed;
        _previousElapsed = now;

        var timing = new FrameTiming(_frameNumber, delta, now);
        _frameNumber++;
        return timing;
    }

    public void SyncToPhysics(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        ThrowIfDisposed();

        var writes = BuildBodyWrites(world);
        var pinnedWrites = default(GCHandle);

        try
        {
            var writesPtr = IntPtr.Zero;
            if (writes.Length > 0)
            {
                pinnedWrites = GCHandle.Alloc(writes, GCHandleType.Pinned);
                writesPtr = pinnedWrites.AddrOfPinnedObject();
            }

            NativeStatusGuard.ThrowIfFailed(
                _interop.PhysicsSyncFromWorld(_physics, writesPtr, checked((uint)writes.Length)),
                "physics_sync_from_world");
        }
        finally
        {
            if (pinnedWrites.IsAllocated)
            {
                pinnedWrites.Free();
            }
        }
    }

    public void Step(TimeSpan deltaTime)
    {
        if (deltaTime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time must be positive.");
        }

        ThrowIfDisposed();

        NativeStatusGuard.ThrowIfFailed(
            _interop.PhysicsStep(_physics, deltaTime.TotalSeconds),
            "physics_step");
    }

    public void SyncFromPhysics(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        ThrowIfDisposed();

        var bodyToEntity = BuildBodyEntityMap(world);
        var readCapacity = checked((uint)bodyToEntity.Count);
        var reads = readCapacity == 0 ? Array.Empty<EngineNativeBodyRead>() : new EngineNativeBodyRead[readCapacity];
        var pinnedReads = default(GCHandle);

        try
        {
            var readsPtr = IntPtr.Zero;
            if (reads.Length > 0)
            {
                pinnedReads = GCHandle.Alloc(reads, GCHandleType.Pinned);
                readsPtr = pinnedReads.AddrOfPinnedObject();
            }

            NativeStatusGuard.ThrowIfFailed(
                _interop.PhysicsSyncToWorld(_physics, readsPtr, readCapacity, out var readCount),
                "physics_sync_to_world");

            if (readCount > readCapacity)
            {
                throw new InvalidOperationException(
                    $"Native physics returned read count {readCount}, exceeding capacity {readCapacity}.");
            }

            for (var i = 0; i < readCount; i++)
            {
                var read = reads[i];
                if (!bodyToEntity.TryGetValue(read.Body, out var entity))
                {
                    continue;
                }

                if (!world.TryGetComponent(entity, out PhysicsBody body))
                {
                    continue;
                }

                body.Position = new(read.Position0, read.Position1, read.Position2);
                body.Rotation = new(read.Rotation0, read.Rotation1, read.Rotation2, read.Rotation3);
                body.LinearVelocity = new(read.LinearVelocity0, read.LinearVelocity1, read.LinearVelocity2);
                body.AngularVelocity = new(read.AngularVelocity0, read.AngularVelocity1, read.AngularVelocity2);
                body.IsActive = read.IsActive != 0;
                world.SetComponent(entity, body);
            }
        }
        finally
        {
            if (pinnedReads.IsAllocated)
            {
                pinnedReads.Free();
            }
        }
    }

    public FrameArena BeginFrame(int requestedBytes, int alignment)
    {
        ThrowIfDisposed();

        NativeStatusGuard.ThrowIfFailed(
            _interop.RendererBeginFrame(
                _renderer,
                checked((nuint)requestedBytes),
                checked((nuint)alignment),
                out var frameMemory),
            "renderer_begin_frame");

        if (frameMemory == IntPtr.Zero)
        {
            throw new InvalidOperationException("Native renderer_begin_frame returned null frame memory.");
        }

        return FrameArena.WrapExternalMemory(frameMemory, requestedBytes, alignment);
    }

    public void Submit(RenderPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ThrowIfDisposed();

        var drawItemsPtr = packet.NativeDrawItemsPointer;
        var drawItemsCount = packet.NativeDrawItemCount;
        var uiItemsPtr = packet.NativeUiDrawItemsPointer;
        var uiItemsCount = packet.NativeUiDrawItemCount;

        var drawItems = drawItemsCount == 0 ? BuildDrawItems(packet.DrawCommands) : Array.Empty<EngineNativeDrawItem>();
        var uiItems = uiItemsCount == 0 ? BuildUiItems(packet.UiDrawCommands) : Array.Empty<EngineNativeUiDrawItem>();

        var pinnedDrawItems = default(GCHandle);
        var pinnedUiItems = default(GCHandle);

        try
        {
            if (drawItems.Length > 0)
            {
                pinnedDrawItems = GCHandle.Alloc(drawItems, GCHandleType.Pinned);
                drawItemsPtr = pinnedDrawItems.AddrOfPinnedObject();
                drawItemsCount = drawItems.Length;
            }

            if (uiItems.Length > 0)
            {
                pinnedUiItems = GCHandle.Alloc(uiItems, GCHandleType.Pinned);
                uiItemsPtr = pinnedUiItems.AddrOfPinnedObject();
                uiItemsCount = uiItems.Length;
            }

            var nativePacket = new EngineNativeRenderPacket
            {
                DrawItems = drawItemsPtr,
                DrawItemCount = checked((uint)drawItemsCount),
                UiItems = uiItemsPtr,
                UiItemCount = checked((uint)uiItemsCount)
            };

            NativeStatusGuard.ThrowIfFailed(
                _interop.RendererSubmit(_renderer, in nativePacket),
                "renderer_submit");
        }
        finally
        {
            if (pinnedDrawItems.IsAllocated)
            {
                pinnedDrawItems.Free();
            }

            if (pinnedUiItems.IsAllocated)
            {
                pinnedUiItems.Free();
            }
        }
    }

    public void Present()
    {
        ThrowIfDisposed();

        NativeStatusGuard.ThrowIfFailed(_interop.RendererPresent(_renderer), "renderer_present");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        var destroyFailure = TryDestroyEngine();
        if (destroyFailure is not null)
        {
            throw destroyFailure;
        }
    }

    private static EngineNativeBodyWrite[] BuildBodyWrites(World world)
    {
        var writes = new List<EngineNativeBodyWrite>(world.GetComponentCount<PhysicsBody>());

        foreach (var (_, body) in world.Query<PhysicsBody>())
        {
            writes.Add(new EngineNativeBodyWrite
            {
                Body = EncodeBodyHandle(body.Body),
                Position0 = body.Position.X,
                Position1 = body.Position.Y,
                Position2 = body.Position.Z,
                Rotation0 = body.Rotation.X,
                Rotation1 = body.Rotation.Y,
                Rotation2 = body.Rotation.Z,
                Rotation3 = body.Rotation.W,
                LinearVelocity0 = body.LinearVelocity.X,
                LinearVelocity1 = body.LinearVelocity.Y,
                LinearVelocity2 = body.LinearVelocity.Z,
                AngularVelocity0 = body.AngularVelocity.X,
                AngularVelocity1 = body.AngularVelocity.Y,
                AngularVelocity2 = body.AngularVelocity.Z
            });
        }

        return writes.Count == 0 ? Array.Empty<EngineNativeBodyWrite>() : writes.ToArray();
    }

    private static Dictionary<ulong, EntityId> BuildBodyEntityMap(World world)
    {
        var map = new Dictionary<ulong, EntityId>(world.GetComponentCount<PhysicsBody>());
        foreach (var (entity, body) in world.Query<PhysicsBody>())
        {
            map[EncodeBodyHandle(body.Body)] = entity;
        }

        return map;
    }

    private Exception? TryDestroyEngine()
    {
        if (_engine == IntPtr.Zero)
        {
            _renderer = IntPtr.Zero;
            _physics = IntPtr.Zero;
            return null;
        }

        var status = _interop.EngineDestroy(_engine);
        _engine = IntPtr.Zero;
        _renderer = IntPtr.Zero;
        _physics = IntPtr.Zero;

        return status == EngineNativeStatus.Ok
            ? null
            : new NativeCallException("engine_destroy", status);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed || _engine == IntPtr.Zero)
        {
            throw new ObjectDisposedException(nameof(NativeRuntime));
        }
    }

    private static ulong EncodeBodyHandle(BodyHandle body)
    {
        if (!body.IsValid)
        {
            throw new ArgumentException("Body handle must be valid.", nameof(body));
        }

        return body.Value;
    }
}
