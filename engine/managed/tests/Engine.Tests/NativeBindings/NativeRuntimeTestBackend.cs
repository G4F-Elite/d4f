using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Engine.NativeBindings.Internal.Interop;

namespace Engine.Tests.NativeBindings;

internal sealed class FakeNativeInteropApi : INativeInteropApi
{
    private readonly IntPtr _engineHandle = new(101);
    private readonly IntPtr _rendererHandle = new(202);
    private readonly IntPtr _physicsHandle = new(303);

    public List<string> Calls { get; } = [];

    public IntPtr RendererBeginFrameMemory { get; set; } = new(4096);

    public EngineNativeRenderPacket LastRendererSubmitPacket { get; private set; }

    public EngineNativeDrawItem? LastSubmittedDrawItem { get; private set; }

    public EngineNativeUiDrawItem? LastSubmittedUiItem { get; private set; }

    public uint LastPhysicsWriteCount { get; private set; }

    public EngineNativeBodyWrite? LastPhysicsWrite { get; private set; }

    public EngineNativeBodyRead[] PhysicsReadsToReturn { get; set; } = Array.Empty<EngineNativeBodyRead>();

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
        frameMemory = RendererBeginFrameStatus == EngineNativeStatus.Ok ? RendererBeginFrameMemory : IntPtr.Zero;
        return RendererBeginFrameStatus;
    }

    public EngineNativeStatus RendererSubmit(IntPtr renderer, in EngineNativeRenderPacket packet)
    {
        Calls.Add("renderer_submit");
        LastRendererSubmitPacket = packet;
        LastSubmittedDrawItem = packet.DrawItemCount > 0 && packet.DrawItems != IntPtr.Zero
            ? Marshal.PtrToStructure<EngineNativeDrawItem>(packet.DrawItems)
            : null;
        LastSubmittedUiItem = packet.UiItemCount > 0 && packet.UiItems != IntPtr.Zero
            ? Marshal.PtrToStructure<EngineNativeUiDrawItem>(packet.UiItems)
            : null;
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
        LastPhysicsWriteCount = writeCount;
        LastPhysicsWrite = writeCount > 0 && writes != IntPtr.Zero
            ? Marshal.PtrToStructure<EngineNativeBodyWrite>(writes)
            : null;
        return PhysicsSyncFromWorldStatus;
    }

    public EngineNativeStatus PhysicsSyncToWorld(
        IntPtr physics,
        IntPtr reads,
        uint readCapacity,
        out uint readCount)
    {
        Calls.Add("physics_sync_to_world");

        var writableCount = Math.Min((uint)PhysicsReadsToReturn.Length, readCapacity);
        for (var i = 0u; i < writableCount; i++)
        {
            var destination = reads + checked((int)(i * (uint)Marshal.SizeOf<EngineNativeBodyRead>()));
            Marshal.StructureToPtr(PhysicsReadsToReturn[i], destination, fDeleteOld: false);
        }

        readCount = writableCount;
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

internal readonly record struct DummyComponent(int Value);
