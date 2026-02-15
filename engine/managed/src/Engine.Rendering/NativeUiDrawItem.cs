using System.Runtime.InteropServices;

namespace Engine.Rendering;

[StructLayout(LayoutKind.Sequential)]
public struct NativeUiDrawItem
{
    public ulong Texture;
    public uint VertexOffset;
    public uint VertexCount;
    public uint IndexOffset;
    public uint IndexCount;

    public static NativeUiDrawItem From(in UiDrawCommand command)
        => new()
        {
            Texture = command.Texture.Value,
            VertexOffset = command.VertexOffset,
            VertexCount = command.VertexCount,
            IndexOffset = command.IndexOffset,
            IndexCount = command.IndexCount
        };
}
