using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Engine.NativeBindings.Internal.Interop;

internal sealed partial class DffNativeInteropApi : INativeInteropApi
{
    public static DffNativeInteropApi Instance { get; } = new();

    private DffNativeInteropApi()
    {
    }

    public EngineNativeStatus EngineCreate(in EngineNativeCreateDesc createDesc, out IntPtr engine)
        => NativeMethods.EngineCreate(in createDesc, out engine);

    public EngineNativeStatus EngineDestroy(IntPtr engine)
        => NativeMethods.EngineDestroy(engine);

    public EngineNativeStatus EnginePumpEvents(
        IntPtr engine,
        out EngineNativeInputSnapshot input,
        out EngineNativeWindowEvents windowEvents)
        => NativeMethods.EnginePumpEvents(engine, out input, out windowEvents);

    public EngineNativeStatus EngineGetRenderer(IntPtr engine, out IntPtr renderer)
        => NativeMethods.EngineGetRenderer(engine, out renderer);

    public EngineNativeStatus EngineGetPhysics(IntPtr engine, out IntPtr physics)
        => NativeMethods.EngineGetPhysics(engine, out physics);

    public EngineNativeStatus RendererBeginFrame(
        IntPtr renderer,
        nuint requestedBytes,
        nuint alignment,
        out IntPtr frameMemory)
        => NativeMethods.RendererBeginFrame(renderer, requestedBytes, alignment, out frameMemory);

    public EngineNativeStatus RendererSubmit(IntPtr renderer, in EngineNativeRenderPacket packet)
        => NativeMethods.RendererSubmit(renderer, in packet);

    public EngineNativeStatus RendererPresent(IntPtr renderer)
        => NativeMethods.RendererPresent(renderer);

    public EngineNativeStatus PhysicsStep(IntPtr physics, double deltaSeconds)
        => NativeMethods.PhysicsStep(physics, deltaSeconds);

    public EngineNativeStatus PhysicsSyncFromWorld(IntPtr physics, IntPtr writes, uint writeCount)
        => NativeMethods.PhysicsSyncFromWorld(physics, writes, writeCount);

    public EngineNativeStatus PhysicsSyncToWorld(
        IntPtr physics,
        IntPtr reads,
        uint readCapacity,
        out uint readCount)
        => NativeMethods.PhysicsSyncToWorld(physics, reads, readCapacity, out readCount);

    private static partial class NativeMethods
    {
        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "engine_create")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus EngineCreate(
            in EngineNativeCreateDesc createDesc,
            out IntPtr outEngine);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "engine_destroy")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus EngineDestroy(IntPtr engine);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "engine_pump_events")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus EnginePumpEvents(
            IntPtr engine,
            out EngineNativeInputSnapshot input,
            out EngineNativeWindowEvents windowEvents);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "engine_get_renderer")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus EngineGetRenderer(
            IntPtr engine,
            out IntPtr outRenderer);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "engine_get_physics")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus EngineGetPhysics(
            IntPtr engine,
            out IntPtr outPhysics);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "renderer_begin_frame")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus RendererBeginFrame(
            IntPtr renderer,
            nuint requestedBytes,
            nuint alignment,
            out IntPtr outFrameMemory);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "renderer_submit")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus RendererSubmit(
            IntPtr renderer,
            in EngineNativeRenderPacket packet);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "renderer_present")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus RendererPresent(IntPtr renderer);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "physics_step")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus PhysicsStep(IntPtr physics, double dtSeconds);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "physics_sync_from_world")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus PhysicsSyncFromWorld(
            IntPtr physics,
            IntPtr writes,
            uint writeCount);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "physics_sync_to_world")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus PhysicsSyncToWorld(
            IntPtr physics,
            IntPtr reads,
            uint readCapacity,
            out uint outReadCount);
    }
}
