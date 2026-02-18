using System;
using Engine.Rendering;

namespace Engine.App;

public readonly record struct FrameObservabilitySnapshot(
    long FrameNumber,
    TimeSpan PrePhysicsCpuTime,
    TimeSpan PhysicsCpuTime,
    TimeSpan PostPhysicsCpuTime,
    TimeSpan UiCpuTime,
    TimeSpan PreRenderCpuTime,
    TimeSpan RenderCpuTime,
    TimeSpan TotalCpuTime,
    int PhysicsSubsteps,
    int PhysicsInteropCallCount,
    int RendererInteropCallCount,
    RenderingFrameStats RenderingStats)
{
    public static FrameObservabilitySnapshot Empty { get; } = new(
        -1,
        TimeSpan.Zero,
        TimeSpan.Zero,
        TimeSpan.Zero,
        TimeSpan.Zero,
        TimeSpan.Zero,
        TimeSpan.Zero,
        TimeSpan.Zero,
        0,
        0,
        0,
        RenderingFrameStats.Empty);
}
