using System;
using System.Runtime.InteropServices;
using Engine.NativeBindings.Internal.Interop;
using Engine.Rendering;
using Xunit;

namespace Engine.Tests.NativeBindings;

public sealed class RenderingAbiLayoutTests
{
    [Fact]
    public void NativeDrawItemLayout_MatchesInteropStruct()
    {
        Assert.Equal(Marshal.SizeOf<EngineNativeDrawItem>(), Marshal.SizeOf<NativeDrawItem>());
        Assert.Equal(Marshal.OffsetOf<EngineNativeDrawItem>(nameof(EngineNativeDrawItem.Mesh)), Marshal.OffsetOf<NativeDrawItem>(nameof(NativeDrawItem.Mesh)));
        Assert.Equal(Marshal.OffsetOf<EngineNativeDrawItem>(nameof(EngineNativeDrawItem.Material)), Marshal.OffsetOf<NativeDrawItem>(nameof(NativeDrawItem.Material)));
        Assert.Equal(Marshal.OffsetOf<EngineNativeDrawItem>(nameof(EngineNativeDrawItem.World00)), Marshal.OffsetOf<NativeDrawItem>(nameof(NativeDrawItem.World00)));
        Assert.Equal(Marshal.OffsetOf<EngineNativeDrawItem>(nameof(EngineNativeDrawItem.World33)), Marshal.OffsetOf<NativeDrawItem>(nameof(NativeDrawItem.World33)));
        Assert.Equal(Marshal.OffsetOf<EngineNativeDrawItem>(nameof(EngineNativeDrawItem.SortKeyHigh)), Marshal.OffsetOf<NativeDrawItem>(nameof(NativeDrawItem.SortKeyHigh)));
        Assert.Equal(Marshal.OffsetOf<EngineNativeDrawItem>(nameof(EngineNativeDrawItem.SortKeyLow)), Marshal.OffsetOf<NativeDrawItem>(nameof(NativeDrawItem.SortKeyLow)));
    }

    [Fact]
    public void NativeUiDrawItemLayout_MatchesInteropStruct()
    {
        Assert.Equal(Marshal.SizeOf<EngineNativeUiDrawItem>(), Marshal.SizeOf<NativeUiDrawItem>());
        Assert.Equal(Marshal.OffsetOf<EngineNativeUiDrawItem>(nameof(EngineNativeUiDrawItem.Texture)), Marshal.OffsetOf<NativeUiDrawItem>(nameof(NativeUiDrawItem.Texture)));
        Assert.Equal(Marshal.OffsetOf<EngineNativeUiDrawItem>(nameof(EngineNativeUiDrawItem.VertexOffset)), Marshal.OffsetOf<NativeUiDrawItem>(nameof(NativeUiDrawItem.VertexOffset)));
        Assert.Equal(Marshal.OffsetOf<EngineNativeUiDrawItem>(nameof(EngineNativeUiDrawItem.VertexCount)), Marshal.OffsetOf<NativeUiDrawItem>(nameof(NativeUiDrawItem.VertexCount)));
        Assert.Equal(Marshal.OffsetOf<EngineNativeUiDrawItem>(nameof(EngineNativeUiDrawItem.IndexOffset)), Marshal.OffsetOf<NativeUiDrawItem>(nameof(NativeUiDrawItem.IndexOffset)));
        Assert.Equal(Marshal.OffsetOf<EngineNativeUiDrawItem>(nameof(EngineNativeUiDrawItem.IndexCount)), Marshal.OffsetOf<NativeUiDrawItem>(nameof(NativeUiDrawItem.IndexCount)));
    }
}
