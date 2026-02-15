using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Engine.Core.Handles;
using Engine.ECS;
using Engine.NativeBindings.Internal.Interop;
using Engine.Rendering;

namespace Engine.NativeBindings.Internal;

internal sealed class NativeRuntime : INativePlatformApi, INativePhysicsApi, INativeRenderingApi, IDisposable
{
    private readonly INativeInteropApi _interop;
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

        var readCapacity = checked((uint)world.AliveEntityCount);
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
                _interop.PhysicsSyncToWorld(_physics, readsPtr, readCapacity, out _),
                "physics_sync_to_world");
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

        return new FrameArena(requestedBytes, alignment);
    }

    public void Submit(RenderPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ThrowIfDisposed();

        var drawItems = BuildDrawItems(packet);
        var pinnedDrawItems = default(GCHandle);

        try
        {
            var drawItemsPtr = IntPtr.Zero;
            if (drawItems.Length > 0)
            {
                pinnedDrawItems = GCHandle.Alloc(drawItems, GCHandleType.Pinned);
                drawItemsPtr = pinnedDrawItems.AddrOfPinnedObject();
            }

            var nativePacket = new EngineNativeRenderPacket
            {
                DrawItems = drawItemsPtr,
                DrawItemCount = checked((uint)drawItems.Length),
                UiItems = IntPtr.Zero,
                UiItemCount = 0
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
        var writes = new List<EngineNativeBodyWrite>(world.AliveEntityCount);

        foreach (var entity in world.EnumerateAliveEntities())
        {
            writes.Add(new EngineNativeBodyWrite
            {
                Body = EncodeEntityHandle(entity),
                Rotation3 = 1.0f
            });
        }

        return writes.Count == 0 ? Array.Empty<EngineNativeBodyWrite>() : writes.ToArray();
    }

    private static EngineNativeDrawItem[] BuildDrawItems(RenderPacket packet)
    {
        if (packet.DrawCommands.Count == 0)
        {
            return Array.Empty<EngineNativeDrawItem>();
        }

        var drawItems = new EngineNativeDrawItem[packet.DrawCommands.Count];
        for (var i = 0; i < packet.DrawCommands.Count; i++)
        {
            var command = packet.DrawCommands[i];
            drawItems[i] = new EngineNativeDrawItem
            {
                Mesh = command.Mesh.Value,
                Material = command.Material.Value,
                World00 = 1.0f,
                World11 = 1.0f,
                World22 = 1.0f,
                World33 = 1.0f,
                SortKeyHigh = command.Material.Value,
                SortKeyLow = command.Mesh.Value
            };
        }

        return drawItems;
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

    private static ulong EncodeEntityHandle(EntityId entity)
    {
        if (!entity.IsValid)
        {
            throw new ArgumentException("Entity id must be valid.", nameof(entity));
        }

        return ((ulong)entity.Generation << 32) | (uint)(entity.Index + 1);
    }
}
