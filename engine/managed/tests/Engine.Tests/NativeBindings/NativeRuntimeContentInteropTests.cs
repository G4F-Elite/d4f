using Engine.NativeBindings.Internal;
using Engine.NativeBindings.Internal.Interop;

namespace Engine.Tests.NativeBindings;

public sealed class NativeRuntimeContentInteropTests
{
    [Fact]
    public void ContentMount_ShouldForwardPakAndDirectoryPaths()
    {
        var backend = new FakeNativeInteropApi();
        using var runtime = new NativeRuntime(backend);

        runtime.ContentMountPak("build/content/Game.pak");
        runtime.ContentMountDirectory("assets");

        Assert.Equal("build/content/Game.pak", backend.LastMountedPakPath);
        Assert.Equal("assets", backend.LastMountedDirectoryPath);
        Assert.Equal(1, backend.CountCall("content_mount_pak"));
        Assert.Equal(1, backend.CountCall("content_mount_directory"));
    }

    [Fact]
    public void ContentMount_ShouldValidateInput()
    {
        var backend = new FakeNativeInteropApi();
        using var runtime = new NativeRuntime(backend);

        Assert.Throws<ArgumentException>(() => runtime.ContentMountPak(""));
        Assert.Throws<ArgumentException>(() => runtime.ContentMountDirectory(" "));
        Assert.Equal(0, backend.CountCall("content_mount_pak"));
        Assert.Equal(0, backend.CountCall("content_mount_directory"));
    }

    [Fact]
    public void ContentReadFile_ShouldUseTwoPhaseReadAndReturnPayload()
    {
        var backend = new FakeNativeInteropApi();
        backend.ContentFilesToReturn["textures/hero.bin"] = [1, 2, 3, 4, 5];
        using var runtime = new NativeRuntime(backend);

        byte[] payload = runtime.ContentReadFile("textures/hero.bin");

        Assert.Equal([1, 2, 3, 4, 5], payload);
        Assert.Equal(2, backend.CountCall("content_read_file"));
    }

    [Fact]
    public void ContentReadFile_ShouldThrowOnNativeFailure()
    {
        var backend = new FakeNativeInteropApi
        {
            ContentReadFileStatus = EngineNativeStatus.InternalError
        };
        using var runtime = new NativeRuntime(backend);

        NativeCallException exception = Assert.Throws<NativeCallException>(() => runtime.ContentReadFile("missing.bin"));
        Assert.Contains("content_read_file", exception.Message, StringComparison.Ordinal);
    }
}
