using System.Collections.Generic;

namespace Engine.Rendering;

internal static class RenderCommandOrdering
{
    private static readonly DrawCommandComparer Comparer = new();

    public static IReadOnlyList<DrawCommand> OrderDrawCommands(IReadOnlyList<DrawCommand> drawCommands)
    {
        ArgumentNullException.ThrowIfNull(drawCommands);
        if (drawCommands.Count <= 1)
        {
            return drawCommands;
        }

        if (drawCommands is List<DrawCommand> mutableList)
        {
            mutableList.Sort(Comparer);
            return mutableList;
        }

        DrawCommand[] ordered = drawCommands.ToArray();
        Array.Sort(ordered, Comparer);
        return ordered;
    }

    private sealed class DrawCommandComparer : IComparer<DrawCommand>
    {
        public int Compare(DrawCommand left, DrawCommand right)
        {
            int byHigh = left.SortKeyHigh.CompareTo(right.SortKeyHigh);
            if (byHigh != 0)
            {
                return byHigh;
            }

            int byLow = left.SortKeyLow.CompareTo(right.SortKeyLow);
            if (byLow != 0)
            {
                return byLow;
            }

            int byMaterial = left.Material.Value.CompareTo(right.Material.Value);
            if (byMaterial != 0)
            {
                return byMaterial;
            }

            int byMesh = left.Mesh.Value.CompareTo(right.Mesh.Value);
            if (byMesh != 0)
            {
                return byMesh;
            }

            int byTexture = left.Texture.Value.CompareTo(right.Texture.Value);
            if (byTexture != 0)
            {
                return byTexture;
            }

            int byEntityIndex = left.EntityId.Index.CompareTo(right.EntityId.Index);
            if (byEntityIndex != 0)
            {
                return byEntityIndex;
            }

            return left.EntityId.Generation.CompareTo(right.EntityId.Generation);
        }
    }
}
