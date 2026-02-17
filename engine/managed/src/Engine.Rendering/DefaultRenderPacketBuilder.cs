using System;
using Engine.Core.Timing;
using Engine.Core.Handles;
using Engine.ECS;

namespace Engine.Rendering;

public sealed class DefaultRenderPacketBuilder : IRenderPacketBuilder
{
    public static DefaultRenderPacketBuilder Instance { get; } = new();

    private DefaultRenderPacketBuilder()
    {
    }

    public RenderPacket Build(World world, in FrameTiming timing, FrameArena frameArena)
    {
        return Build(world, timing, frameArena, RenderSettings.Default);
    }

    public RenderPacket Build(World world, in FrameTiming timing, FrameArena frameArena, in RenderSettings renderSettings)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(frameArena);
        IReadOnlyList<UiDrawCommand> uiCommands = CollectUiCommands(world);

        return RenderPacketMarshaller.Marshal(
            timing.FrameNumber,
            frameArena,
            Array.Empty<DrawCommand>(),
            uiCommands,
            renderSettings.DebugViewMode);
    }

    private static IReadOnlyList<UiDrawCommand> CollectUiCommands(World world)
    {
        List<(EntityId Entity, UiRenderBatch Batch)>? batches = null;
        foreach (var (entity, batch) in world.Query<UiRenderBatch>())
        {
            if (batch.Commands.Count == 0)
            {
                continue;
            }

            batches ??= new List<(EntityId Entity, UiRenderBatch Batch)>();
            batches.Add((entity, batch));
        }

        if (batches is null || batches.Count == 0)
        {
            return Array.Empty<UiDrawCommand>();
        }

        batches.Sort(static (left, right) =>
        {
            int byIndex = left.Entity.Index.CompareTo(right.Entity.Index);
            if (byIndex != 0)
            {
                return byIndex;
            }

            return left.Entity.Generation.CompareTo(right.Entity.Generation);
        });

        List<UiDrawCommand>? aggregate = null;
        foreach (var (_, batch) in batches)
        {
            aggregate ??= new List<UiDrawCommand>(batch.Commands.Count);
            aggregate.AddRange(batch.Commands);
        }

        return aggregate is null
            ? Array.Empty<UiDrawCommand>()
            : aggregate;
    }
}
