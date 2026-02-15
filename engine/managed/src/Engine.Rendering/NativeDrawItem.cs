using System.Runtime.InteropServices;

namespace Engine.Rendering;

[StructLayout(LayoutKind.Sequential)]
public struct NativeDrawItem
{
    public ulong Mesh;
    public ulong Material;
    public float World00;
    public float World01;
    public float World02;
    public float World03;
    public float World10;
    public float World11;
    public float World12;
    public float World13;
    public float World20;
    public float World21;
    public float World22;
    public float World23;
    public float World30;
    public float World31;
    public float World32;
    public float World33;
    public uint SortKeyHigh;
    public uint SortKeyLow;

    public static NativeDrawItem From(in DrawCommand command)
    {
        var world = command.WorldMatrix;
        return new NativeDrawItem
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
}
