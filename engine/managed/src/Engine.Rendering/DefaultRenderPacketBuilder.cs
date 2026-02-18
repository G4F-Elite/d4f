using System;
using System.Collections.Generic;
using Engine.Core.Timing;
using Engine.Core.Handles;
using Engine.ECS;

namespace Engine.Rendering;

public sealed class DefaultRenderPacketBuilder : IRenderPacketBuilder
{
    public static DefaultRenderPacketBuilder Instance { get; } = new();
    private readonly List<(EntityId Entity, RenderMeshInstance Component)> _renderMeshInstances = new(128);
    private readonly List<DrawCommand> _drawCommands = new(128);
    private readonly List<(EntityId Entity, UiRenderBatch Batch)> _uiBatches = new(64);
    private readonly List<UiDrawCommand> _uiCommands = new(256);

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

    private IReadOnlyList<DrawCommand> CollectDrawCommands(World world)
    {
        _renderMeshInstances.Clear();
        world.QueryNonAlloc(_renderMeshInstances);

        _drawCommands.Clear();
        for (int i = 0; i < _renderMeshInstances.Count; i++)
        {
            (EntityId entity, RenderMeshInstance instance) = _renderMeshInstances[i];
            if (!TryCreateDrawCommand(entity, in instance, out DrawCommand drawCommand))
            {
                continue;
            }

            _drawCommands.Add(drawCommand);
        }

        return _drawCommands;
    }

    private IReadOnlyList<UiDrawCommand> CollectUiCommands(World world)
    {
        _uiBatches.Clear();
        world.QueryNonAlloc(_uiBatches);

        if (_uiBatches.Count == 0)
        {
            _uiCommands.Clear();
            return _uiCommands;
        }

        int writeIndex = 0;
        for (int i = 0; i < _uiBatches.Count; i++)
        {
            (EntityId entity, UiRenderBatch batch) = _uiBatches[i];
            if (batch.Commands.Count == 0)
            {
                continue;
            }

            if (writeIndex != i)
            {
                _uiBatches[writeIndex] = (entity, batch);
            }

            writeIndex++;
        }

        if (writeIndex == 0)
        {
            _uiBatches.Clear();
            _uiCommands.Clear();
            return _uiCommands;
        }

        if (writeIndex < _uiBatches.Count)
        {
            _uiBatches.RemoveRange(writeIndex, _uiBatches.Count - writeIndex);
        }

        _uiBatches.Sort(static (left, right) =>
        {
            int byIndex = left.Entity.Index.CompareTo(right.Entity.Index);
            if (byIndex != 0)
            {
                return byIndex;
            }

            return left.Entity.Generation.CompareTo(right.Entity.Generation);
        });

        _uiCommands.Clear();
        for (int i = 0; i < _uiBatches.Count; i++)
        {
            _uiCommands.AddRange(_uiBatches[i].Batch.Commands);
        }

        return _uiCommands;
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
