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
internal struct EngineNativeStringView
{
    public IntPtr Data;
    public nuint Length;
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
    public float ScissorX;
    public float ScissorY;
    public float ScissorWidth;
    public float ScissorHeight;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineNativeMeshCpuData
{
    public IntPtr Positions;
    public uint VertexCount;
    public IntPtr Indices;
    public uint IndexCount;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineNativeTextureCpuData
{
    public IntPtr Rgba8;
    public uint Width;
    public uint Height;
    public uint Stride;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineNativeRenderPacket
{
    public IntPtr DrawItems;
    public uint DrawItemCount;
    public IntPtr UiItems;
    public uint UiItemCount;
    public byte DebugViewMode;
    public byte Reserved0;
    public byte Reserved1;
    public byte Reserved2;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineNativeRendererFrameStats
{
    public uint DrawItemCount;
    public uint UiItemCount;
    public uint ExecutedPassCount;
    public uint Reserved0;
    public ulong PresentCount;
    public ulong PipelineCacheHits;
    public ulong PipelineCacheMisses;
    public ulong PassMask;
    public ulong TriangleCount;
    public ulong UploadBytes;
    public ulong GpuMemoryBytes;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineNativeCaptureRequest
{
    public uint Width;
    public uint Height;
    public byte IncludeAlpha;
    public byte Reserved0;
    public byte Reserved1;
    public byte Reserved2;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineNativeCaptureResult
{
    public uint Width;
    public uint Height;
    public uint Stride;
    public uint Format;
    public IntPtr Pixels;
    public nuint PixelBytes;
}

internal enum EngineNativeAudioBus : byte
{
    Master = 0,
    Music = 1,
    Sfx = 2,
    Ambience = 3
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineNativeAudioPlayDesc
{
    public float Volume;
    public float Pitch;
    public byte Bus;
    public byte Loop;
    public byte IsSpatialized;
    public byte Reserved0;
    public float Position0;
    public float Position1;
    public float Position2;
    public float Velocity0;
    public float Velocity1;
    public float Velocity2;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineNativeListenerDesc
{
    public float Position0;
    public float Position1;
    public float Position2;
    public float Forward0;
    public float Forward1;
    public float Forward2;
    public float Up0;
    public float Up1;
    public float Up2;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineNativeEmitterParams
{
    public float Volume;
    public float Pitch;
    public float Position0;
    public float Position1;
    public float Position2;
    public float Velocity0;
    public float Velocity1;
    public float Velocity2;
    public float Lowpass;
    public float ReverbSend;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineNativeAudioBusParams
{
    public byte Bus;
    public byte Muted;
    public byte Reserved0;
    public byte Reserved1;
    public float Gain;
    public float Lowpass;
    public float ReverbSend;
}

internal enum EngineNativeNetEventKind : byte
{
    Connected = 1,
    Disconnected = 2,
    Message = 3
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineNativeNetDesc
{
    public uint LocalPeerId;
    public uint MaxEventsPerPump;
    public uint MaxPayloadBytes;
    public byte LoopbackEnabled;
    public byte Reserved0;
    public byte Reserved1;
    public byte Reserved2;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineNativeNetSendDesc
{
    public uint PeerId;
    public byte Channel;
    public byte Reserved0;
    public byte Reserved1;
    public byte Reserved2;
    public IntPtr Payload;
    public uint PayloadSize;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineNativeNetEvent
{
    public byte Kind;
    public byte Channel;
    public ushort Reserved0;
    public uint PeerId;
    public IntPtr Payload;
    public uint PayloadSize;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineNativeNetEvents
{
    public IntPtr Events;
    public uint EventCount;
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

[StructLayout(LayoutKind.Sequential)]
internal struct EngineNativeRaycastQuery
{
    public float Origin0;
    public float Origin1;
    public float Origin2;
    public float Direction0;
    public float Direction1;
    public float Direction2;
    public float MaxDistance;
    public byte IncludeTriggers;
    public byte Reserved0;
    public byte Reserved1;
    public byte Reserved2;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineNativeRaycastHit
{
    public byte HasHit;
    public byte IsTrigger;
    public byte Reserved0;
    public byte Reserved1;
    public ulong Body;
    public float Distance;
    public float Point0;
    public float Point1;
    public float Point2;
    public float Normal0;
    public float Normal1;
    public float Normal2;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineNativeSweepQuery
{
    public float Origin0;
    public float Origin1;
    public float Origin2;
    public float Direction0;
    public float Direction1;
    public float Direction2;
    public float MaxDistance;
    public byte IncludeTriggers;
    public byte ShapeType;
    public byte Reserved0;
    public byte Reserved1;
    public float ShapeDimensions0;
    public float ShapeDimensions1;
    public float ShapeDimensions2;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineNativeSweepHit
{
    public byte HasHit;
    public byte IsTrigger;
    public byte Reserved0;
    public byte Reserved1;
    public ulong Body;
    public float Distance;
    public float Point0;
    public float Point1;
    public float Point2;
    public float Normal0;
    public float Normal1;
    public float Normal2;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineNativeOverlapQuery
{
    public float Center0;
    public float Center1;
    public float Center2;
    public byte IncludeTriggers;
    public byte ShapeType;
    public byte Reserved0;
    public byte Reserved1;
    public float ShapeDimensions0;
    public float ShapeDimensions1;
    public float ShapeDimensions2;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EngineNativeOverlapHit
{
    public ulong Body;
    public byte IsTrigger;
    public byte Reserved0;
    public byte Reserved1;
    public byte Reserved2;
}
