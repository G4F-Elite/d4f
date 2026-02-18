using System.Runtime.InteropServices;
using Engine.NativeBindings.Internal.Interop;

namespace Engine.Tests.NativeBindings;

public sealed class NativeStringViewAbiLayoutTests
{
    [Fact]
    public void StringViewLayout_ShouldMatchExpectedPointerSizedLayout()
    {
        Assert.Equal(IntPtr.Size * 2, Marshal.SizeOf<EngineNativeStringView>());
        Assert.Equal(IntPtr.Zero, Marshal.OffsetOf<EngineNativeStringView>(nameof(EngineNativeStringView.Data)));
        Assert.Equal((IntPtr)IntPtr.Size, Marshal.OffsetOf<EngineNativeStringView>(nameof(EngineNativeStringView.Length)));
    }
}
