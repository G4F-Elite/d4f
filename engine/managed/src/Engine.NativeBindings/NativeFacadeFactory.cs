using System;
using Engine.Core.Abstractions;
using Engine.Core.Timing;
using Engine.ECS;
using Engine.NativeBindings.Internal;
using Engine.NativeBindings.Internal.Interop;
using Engine.Physics;
using Engine.Rendering;
using Engine.UI;

namespace Engine.NativeBindings;

public static class NativeFacadeFactory
{
    public static NativeFacadeSet CreateNativeFacadeSet()
        => CreateNativeFacadeSet(DffNativeInteropApi.Instance);

    public static IPlatformFacade CreatePlatformFacade() => new NativePlatformFacade(new NativePlatformApiStub());

    public static ITimingFacade CreateTimingFacade() => new NativeTimingFacade(new NativeTimingApiStub());

    public static IPhysicsFacade CreatePhysicsFacade() => new NativePhysicsFacade(new NativePhysicsApiStub());

    public static IUiFacade CreateUiFacade() => new NativeUiFacade(new NativeUiApiStub());

    public static IRenderingFacade CreateRenderingFacade() => new NativeRenderingFacade(new NativeRenderingApiStub());

    internal static IPlatformFacade CreatePlatformFacade(INativePlatformApi nativeApi) => new NativePlatformFacade(nativeApi);

    internal static ITimingFacade CreateTimingFacade(INativeTimingApi nativeApi) => new NativeTimingFacade(nativeApi);

    internal static IPhysicsFacade CreatePhysicsFacade(INativePhysicsApi nativeApi) => new NativePhysicsFacade(nativeApi);

    internal static IUiFacade CreateUiFacade(INativeUiApi nativeApi) => new NativeUiFacade(nativeApi);

    internal static IRenderingFacade CreateRenderingFacade(INativeRenderingApi nativeApi) => new NativeRenderingFacade(nativeApi);

    internal static NativeFacadeSet CreateNativeFacadeSet(INativeInteropApi interop)
        => new(new NativeRuntime(interop));

    private sealed class NativePlatformFacade : IPlatformFacade
    {
        private readonly INativePlatformApi _nativeApi;

        public NativePlatformFacade(INativePlatformApi nativeApi)
        {
            _nativeApi = nativeApi ?? throw new ArgumentNullException(nameof(nativeApi));
        }

        public bool PumpEvents() => _nativeApi.PumpEvents();
    }

    private sealed class NativeTimingFacade : ITimingFacade
    {
        private readonly INativeTimingApi _nativeApi;

        public NativeTimingFacade(INativeTimingApi nativeApi)
        {
            _nativeApi = nativeApi ?? throw new ArgumentNullException(nameof(nativeApi));
        }

        public FrameTiming NextFrameTiming() => _nativeApi.NextFrameTiming();
    }

    private sealed class NativePhysicsFacade : IPhysicsFacade
    {
        private readonly INativePhysicsApi _nativeApi;

        public NativePhysicsFacade(INativePhysicsApi nativeApi)
        {
            _nativeApi = nativeApi ?? throw new ArgumentNullException(nameof(nativeApi));
        }

        public void SyncToPhysics(World world) => _nativeApi.SyncToPhysics(world);

        public void Step(TimeSpan deltaTime) => _nativeApi.Step(deltaTime);

        public void SyncFromPhysics(World world) => _nativeApi.SyncFromPhysics(world);

        public bool Raycast(in PhysicsRaycastQuery query, out PhysicsRaycastHit hit)
            => _nativeApi.Raycast(query, out hit);

        public bool Sweep(in PhysicsSweepQuery query, out PhysicsSweepHit hit)
            => _nativeApi.Sweep(query, out hit);

        public int Overlap(in PhysicsOverlapQuery query, Span<PhysicsOverlapHit> hits)
            => _nativeApi.Overlap(query, hits);
    }

    private sealed class NativeUiFacade : IUiFacade
    {
        private readonly INativeUiApi _nativeApi;

        public NativeUiFacade(INativeUiApi nativeApi)
        {
            _nativeApi = nativeApi ?? throw new ArgumentNullException(nameof(nativeApi));
        }

        public void Update(World world, in FrameTiming timing) => _nativeApi.Update(world, timing);
    }

    private sealed class NativeRenderingFacade : IRenderingFacade
    {
        private readonly INativeRenderingApi _nativeApi;

        public NativeRenderingFacade(INativeRenderingApi nativeApi)
        {
            _nativeApi = nativeApi ?? throw new ArgumentNullException(nameof(nativeApi));
        }

        public FrameArena BeginFrame(int requestedBytes, int alignment)
            => _nativeApi.BeginFrame(requestedBytes, alignment);

        public void Submit(RenderPacket packet) => _nativeApi.Submit(packet);

        public void Present() => _nativeApi.Present();

        public RenderingFrameStats GetLastFrameStats() => _nativeApi.GetLastFrameStats();

        public byte[] CaptureFrameRgba8(uint width, uint height, bool includeAlpha = true)
            => _nativeApi.CaptureFrameRgba8(width, height, includeAlpha);
    }
}
