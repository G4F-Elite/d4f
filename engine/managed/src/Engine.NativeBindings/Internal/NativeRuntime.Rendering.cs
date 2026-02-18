using System;
using System.Collections.Generic;
using Engine.NativeBindings.Internal.Interop;
using Engine.Rendering;

namespace Engine.NativeBindings.Internal;

internal sealed partial class NativeRuntime
{
    private static EngineNativeDrawItem[] BuildDrawItems(IReadOnlyList<DrawCommand> drawCommands)
    {
        if (drawCommands.Count == 0)
        {
            return Array.Empty<EngineNativeDrawItem>();
        }

        var drawItems = new EngineNativeDrawItem[drawCommands.Count];
        for (var i = 0; i < drawCommands.Count; i++)
        {
            var command = drawCommands[i];
            var world = command.WorldMatrix;
            drawItems[i] = new EngineNativeDrawItem
            {
                Mesh = command.Mesh.Value,
                Material = command.Material.Value,
                World00 = world.M11,
                World01 = world.M12,
                World02 = world.M13,
                World03 = world.M14,
                World10 = world.M21,
                World11 = world.M22,
                World12 = world.M23,
                World13 = world.M24,
                World20 = world.M31,
                World21 = world.M32,
                World22 = world.M33,
                World23 = world.M34,
                World30 = world.M41,
                World31 = world.M42,
                World32 = world.M43,
                World33 = world.M44,
                SortKeyHigh = command.SortKeyHigh,
                SortKeyLow = command.SortKeyLow
            };
        }

        return drawItems;
    }

    private static EngineNativeUiDrawItem[] BuildUiItems(IReadOnlyList<UiDrawCommand> uiDrawCommands)
    {
        if (uiDrawCommands.Count == 0)
        {
            return Array.Empty<EngineNativeUiDrawItem>();
        }

        var uiItems = new EngineNativeUiDrawItem[uiDrawCommands.Count];
        for (var i = 0; i < uiDrawCommands.Count; i++)
        {
            var command = uiDrawCommands[i];
            uiItems[i] = new EngineNativeUiDrawItem
            {
                Texture = command.Texture.Value,
                VertexOffset = command.VertexOffset,
                VertexCount = command.VertexCount,
                IndexOffset = command.IndexOffset,
                IndexCount = command.IndexCount,
                ScissorX = command.ScissorRect.X,
                ScissorY = command.ScissorRect.Y,
                ScissorWidth = command.ScissorRect.Width,
                ScissorHeight = command.ScissorRect.Height
            };
        }

        return uiItems;
    }
}
