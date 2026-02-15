using System;
using Engine.Core.Timing;
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
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(frameArena);
        IReadOnlyList<UiDrawCommand> uiCommands = CollectUiCommands(world);

        return RenderPacketMarshaller.Marshal(
            timing.FrameNumber,
            frameArena,
            Array.Empty<DrawCommand>(),
            uiCommands);
    }

    private static IReadOnlyList<UiDrawCommand> CollectUiCommands(World world)
    {
        List<UiDrawCommand>? aggregate = null;

        foreach (var (_, batch) in world.Query<UiRenderBatch>())
        {
            if (batch.Commands.Count == 0)
            {
                continue;
            }

            aggregate ??= new List<UiDrawCommand>(batch.Commands.Count);
            aggregate.AddRange(batch.Commands);
        }

        return aggregate is null
            ? Array.Empty<UiDrawCommand>()
            : aggregate;
    }
}
