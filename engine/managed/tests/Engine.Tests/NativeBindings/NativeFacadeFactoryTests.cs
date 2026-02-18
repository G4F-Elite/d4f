using System;
using Engine.Core.Timing;
using Engine.ECS;
using Engine.NativeBindings;
using Engine.Net;
using Engine.Physics;
using Engine.Rendering;
using Xunit;

namespace Engine.Tests.NativeBindings;

public sealed class NativeFacadeFactoryTests
{
    [Fact]
    public void FactoryCreatesUsableFacadeSet()
    {
        var platform = NativeFacadeFactory.CreatePlatformFacade();
        var timing = NativeFacadeFactory.CreateTimingFacade();
        var physics = NativeFacadeFactory.CreatePhysicsFacade();
        var content = NativeFacadeFactory.CreateContentRuntimeFacade();
        var net = NativeFacadeFactory.CreateNetFacade();
        var ui = NativeFacadeFactory.CreateUiFacade();
        var rendering = NativeFacadeFactory.CreateRenderingFacade();

        Assert.True(platform.PumpEvents());

        var frame0 = timing.NextFrameTiming();
        var frame1 = timing.NextFrameTiming();

        Assert.Equal(0, frame0.FrameNumber);
        Assert.Equal(1, frame1.FrameNumber);
        Assert.True(frame1.TotalTime >= frame0.TotalTime);

        var world = new World();
        physics.SyncToPhysics(world);
        physics.Step(TimeSpan.FromMilliseconds(8));
        physics.SyncFromPhysics(world);
        Assert.False(physics.Raycast(new PhysicsRaycastQuery(default, new(1, 0, 0), 10.0f), out _));
        Assert.False(physics.Sweep(
            new PhysicsSweepQuery(default, new(0, 1, 0), 10.0f, ColliderShapeType.Box, new(1, 1, 1)),
            out _));
        Span<PhysicsOverlapHit> overlapHits = stackalloc PhysicsOverlapHit[4];
        Assert.Equal(0, physics.Overlap(
            new PhysicsOverlapQuery(default, ColliderShapeType.Sphere, new(1, 1, 1)),
            overlapHits));
        content.MountDirectory("D:/content");
        Assert.Throws<FileNotFoundException>(() => content.ReadFile("assets/missing.bin"));
        net.Send(77u, NetworkChannel.Unreliable, [1, 2, 3]);
        NetEvent netEvent = Assert.Single(net.Pump());
        Assert.Equal(NetEventKind.Message, netEvent.Kind);
        Assert.Equal(NetworkChannel.Unreliable, netEvent.Channel);
        Assert.Equal(77u, netEvent.PeerId);
        Assert.Equal([1, 2, 3], netEvent.Payload);

        ui.Update(world, frame1);
        using var frameArena = rendering.BeginFrame(1024, 64);
        rendering.Submit(RenderPacket.Empty(frame1.FrameNumber));
        rendering.Present();
    }

    [Fact]
    public void PhysicsFacadeRejectsNegativeDeltaTime()
    {
        var physics = NativeFacadeFactory.CreatePhysicsFacade();
        Assert.Throws<ArgumentOutOfRangeException>(() => physics.Step(TimeSpan.FromMilliseconds(-1)));
    }
}
