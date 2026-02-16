using Engine.NativeBindings.Internal.Interop;
using Xunit;

namespace Engine.Tests.NativeBindings;

public sealed class NativeInteropApiVersionTests
{
    [Fact]
    public void FakeInteropReportsConfiguredNativeApiVersion()
    {
        const uint expectedVersion = 123u;
        var backend = new FakeNativeInteropApi
        {
            NativeApiVersionToReturn = expectedVersion
        };

        INativeInteropApi interop = backend;

        uint actualVersion = interop.EngineGetNativeApiVersion();

        Assert.Equal(expectedVersion, actualVersion);
        Assert.Equal(1, backend.CountCall("engine_get_native_api_version"));
    }
}
