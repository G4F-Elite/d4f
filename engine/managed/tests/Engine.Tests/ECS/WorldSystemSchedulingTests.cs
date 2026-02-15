using System;
using System.Collections.Generic;
using Engine.Core.Timing;
using Engine.ECS;
using Xunit;

namespace Engine.Tests.ECS;

public sealed class WorldSystemSchedulingTests
{
    [Fact]
    public void RegisterSystem_RejectsNullSystem()
    {
        var world = new World();
        Assert.Throws<ArgumentNullException>(() => world.RegisterSystem(SystemStage.PrePhysics, null!));
    }

    [Fact]
    public void RunStage_ExecutesSystemsByPriorityThenRegistrationOrder()
    {
        var world = new World();
        var execution = new List<string>();

        world.RegisterSystem(SystemStage.PrePhysics, new RecordingSystem("A", execution), priority: 10);
        world.RegisterSystem(SystemStage.PrePhysics, new RecordingSystem("B", execution), priority: 0);
        world.RegisterSystem(SystemStage.PrePhysics, new RecordingSystem("C", execution), priority: 0);

        world.RunStage(SystemStage.PrePhysics, new FrameTiming(0, TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)));

        Assert.Equal(new[] { "B", "C", "A" }, execution);
    }

    [Fact]
    public void RunStage_ExecutesOnlySystemsInRequestedStage()
    {
        var world = new World();
        var execution = new List<string>();

        world.RegisterSystem(SystemStage.PrePhysics, new RecordingSystem("PrePhysics", execution));
        world.RegisterSystem(SystemStage.UI, new RecordingSystem("UI", execution));

        world.RunStage(SystemStage.PrePhysics, new FrameTiming(0, TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)));

        Assert.Equal(new[] { "PrePhysics" }, execution);
    }

    [Fact]
    public void RunStage_RejectsUnknownStageValue()
    {
        var world = new World();
        Assert.Throws<ArgumentOutOfRangeException>(() => world.RunStage((SystemStage)999, new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero)));
    }

    private sealed class RecordingSystem : IWorldSystem
    {
        private readonly string _name;
        private readonly IList<string> _execution;

        public RecordingSystem(string name, IList<string> execution)
        {
            _name = name;
            _execution = execution;
        }

        public void Update(World world, in FrameTiming timing)
        {
            _execution.Add(_name);
        }
    }
}
