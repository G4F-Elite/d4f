using System;
using System.Runtime.InteropServices;

namespace Engine.NativeBindings.Internal.Interop;

[StructLayout(LayoutKind.Sequential)]
internal struct EngineNativeCreateDesc
{
    public uint ApiVersion;
    public IntPtr UserData;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineNativeInputSnapshot
{
    public ulong FrameIndex;
    public uint ButtonsMask;
    public float MouseX;
    public float MouseY;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineNativeWindowEvents
{
    public byte ShouldClose;
    public uint Width;
    public uint Height;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineNativeDrawItem
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
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineNativeUiDrawItem
{
    public ulong Texture;
    public uint VertexOffset;
    public uint VertexCount;
    public uint IndexOffset;
    public uint IndexCount;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineNativeRenderPacket
{
    public IntPtr DrawItems;
    public uint DrawItemCount;
    public IntPtr UiItems;
    public uint UiItemCount;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineNativeBodyWrite
{
    public ulong Body;
    public float Position0;
    public float Position1;
    public float Position2;
    public float Rotation0;
    public float Rotation1;
    public float Rotation2;
    public float Rotation3;
    public float LinearVelocity0;
    public float LinearVelocity1;
    public float LinearVelocity2;
    public float AngularVelocity0;
    public float AngularVelocity1;
    public float AngularVelocity2;
    public byte BodyType;
    public byte ColliderShape;
    public byte IsTrigger;
    public byte Reserved0;
    public float ColliderDimensions0;
    public float ColliderDimensions1;
    public float ColliderDimensions2;
    public float Friction;
    public float Restitution;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineNativeBodyRead
{
    public ulong Body;
    public float Position0;
    public float Position1;
    public float Position2;
    public float Rotation0;
    public float Rotation1;
    public float Rotation2;
    public float Rotation3;
    public float LinearVelocity0;
    public float LinearVelocity1;
    public float LinearVelocity2;
    public float AngularVelocity0;
    public float AngularVelocity1;
    public float AngularVelocity2;
    public byte IsActive;
}
