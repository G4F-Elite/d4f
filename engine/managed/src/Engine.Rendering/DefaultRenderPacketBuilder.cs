using System;
using System.Collections.Generic;
using Engine.Core.Timing;
using Engine.Core.Handles;
using Engine.ECS;

namespace Engine.Rendering;

public sealed class DefaultRenderPacketBuilder : IRenderPacketBuilder
{
    public static DefaultRenderPacketBuilder Instance { get; } = new();
    [ThreadStatic]
    private static BuilderScratch? s_scratch;

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
        BuilderScratch scratch = s_scratch ??= new BuilderScratch();
        IReadOnlyList<DrawCommand> drawCommands = CollectDrawCommands(world, scratch);
        IReadOnlyList<UiDrawCommand> uiCommands = CollectUiCommands(world, scratch);

        return RenderPacketMarshaller.Marshal(
            timing.FrameNumber,
            frameArena,
            drawCommands,
            uiCommands,
            renderSettings.DebugViewMode,
            renderSettings.FeatureFlags);
    }

    private static IReadOnlyList<DrawCommand> CollectDrawCommands(World world, BuilderScratch scratch)
    {
        scratch.RenderMeshInstances.Clear();
        world.QueryNonAlloc(scratch.RenderMeshInstances);

        scratch.DrawCommands.Clear();
        for (int i = 0; i < scratch.RenderMeshInstances.Count; i++)
        {
            (EntityId entity, RenderMeshInstance instance) = scratch.RenderMeshInstances[i];
            if (!TryCreateDrawCommand(entity, in instance, out DrawCommand drawCommand))
            {
                continue;
            }

            scratch.DrawCommands.Add(drawCommand);
        }

        return scratch.DrawCommands;
    }

    private static IReadOnlyList<UiDrawCommand> CollectUiCommands(World world, BuilderScratch scratch)
    {
        scratch.UiBatches.Clear();
        world.QueryNonAlloc(scratch.UiBatches);

        if (scratch.UiBatches.Count == 0)
        {
            scratch.UiCommands.Clear();
            return scratch.UiCommands;
        }

        int writeIndex = 0;
        for (int i = 0; i < scratch.UiBatches.Count; i++)
        {
            (EntityId entity, UiRenderBatch batch) = scratch.UiBatches[i];
            if (batch.Commands.Count == 0)
            {
                continue;
            }

            if (writeIndex != i)
            {
                scratch.UiBatches[writeIndex] = (entity, batch);
            }

            writeIndex++;
        }

        if (writeIndex == 0)
        {
            scratch.UiBatches.Clear();
            scratch.UiCommands.Clear();
            return scratch.UiCommands;
        }

        if (writeIndex < scratch.UiBatches.Count)
        {
            scratch.UiBatches.RemoveRange(writeIndex, scratch.UiBatches.Count - writeIndex);
        }

        scratch.UiBatches.Sort(static (left, right) =>
        {
            int byIndex = left.Entity.Index.CompareTo(right.Entity.Index);
            if (byIndex != 0)
            {
                return byIndex;
            }

            return left.Entity.Generation.CompareTo(right.Entity.Generation);
        });

        scratch.UiCommands.Clear();
        for (int i = 0; i < scratch.UiBatches.Count; i++)
        {
            scratch.UiCommands.AddRange(scratch.UiBatches[i].Batch.Commands);
        }

        return scratch.UiCommands;
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

    private sealed class BuilderScratch
    {
        public List<(EntityId Entity, RenderMeshInstance Component)> RenderMeshInstances { get; } = new(128);

        public List<DrawCommand> DrawCommands { get; } = new(128);

        public List<(EntityId Entity, UiRenderBatch Batch)> UiBatches { get; } = new(64);

        public List<UiDrawCommand> UiCommands { get; } = new(256);
    }
}
