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
        IReadOnlyList<DrawCommand> drawCommands = CollectDrawCommands(world);
        IReadOnlyList<UiDrawCommand> uiCommands = CollectUiCommands(world);

        return RenderPacketMarshaller.Marshal(
            timing.FrameNumber,
            frameArena,
            drawCommands,
            uiCommands,
            renderSettings.DebugViewMode,
            renderSettings.FeatureFlags);
    }

    private static IReadOnlyList<DrawCommand> CollectDrawCommands(World world)
    {
        List<DrawCommand>? drawCommands = null;
        foreach (var (entity, instance) in world.Query<RenderMeshInstance>())
        {
            if (!TryCreateDrawCommand(entity, in instance, out DrawCommand drawCommand))
            {
                continue;
            }

            drawCommands ??= new List<DrawCommand>();
            drawCommands.Add(drawCommand);
        }

        return drawCommands is null || drawCommands.Count == 0
            ? Array.Empty<DrawCommand>()
            : drawCommands;
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

    private static bool TryCreateDrawCommand(
        EntityId entity,
        in RenderMeshInstance instance,
        out DrawCommand drawCommand)
    {
        if (!entity.IsValid ||
            !instance.Mesh.IsValid ||
            !instance.Material.IsValid ||
            !instance.Texture.IsValid)
        {
            drawCommand = default;
            return false;
        }

        drawCommand = new DrawCommand(
            entity,
            instance.Mesh,
            instance.Material,
            instance.Texture,
            instance.WorldMatrix,
            instance.SortKeyHigh,
            instance.SortKeyLow);
        return true;
    }
}
