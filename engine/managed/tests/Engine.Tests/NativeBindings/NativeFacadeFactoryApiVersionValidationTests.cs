using System.IO;
using Engine.NativeBindings;
using Engine.NativeBindings.Internal.Interop;
using Xunit;

namespace Engine.Tests.NativeBindings;

public sealed class NativeFacadeFactoryApiVersionValidationTests
{
    [Fact]
    public void NativeRuntime_ShouldFailBeforeEngineCreate_WhenApiVersionMismatches()
    {
        var backend = new FakeNativeInteropApi
        {
            NativeApiVersionToReturn = EngineNativeConstants.ApiVersion + 1u
        };

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => NativeFacadeFactory.CreateNativeFacadeSet(backend));

        Assert.Contains("version mismatch", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(["engine_get_native_api_version"], backend.Calls);
    }
}
