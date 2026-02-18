using System;
using Engine.Core.Abstractions;
using Engine.NativeBindings;
using Engine.NativeBindings.Internal.Interop;
using Xunit;

namespace Engine.Tests.NativeBindings;

public sealed class NativeFacadeFactoryContentTests
{
    [Fact]
    public void CreateContentRuntimeFacade_ValidatesInputPaths()
    {
        IContentRuntimeFacade content = NativeFacadeFactory.CreateContentRuntimeFacade();

        Assert.Throws<ArgumentException>(() => content.MountPak(" "));
        Assert.Throws<ArgumentException>(() => content.MountDirectory(string.Empty));
        Assert.Throws<ArgumentException>(() => content.ReadFile("\t"));
    }

    [Fact]
    public void NativeFacadeSetContent_DelegatesToNativeRuntime()
    {
        var backend = new FakeNativeInteropApi();
        backend.ContentFilesToReturn["assets/config.bin"] = [10, 20, 30, 40];
        using var nativeSet = NativeFacadeFactory.CreateNativeFacadeSet(backend);

        nativeSet.Content.MountPak("D:/content/Game.pak");
        nativeSet.Content.MountDirectory("D:/content/dev");
        byte[] payload = nativeSet.Content.ReadFile("assets/config.bin");

        Assert.Equal("D:/content/Game.pak", backend.LastMountedPakPath);
        Assert.Equal("D:/content/dev", backend.LastMountedDirectoryPath);
        Assert.Equal([10, 20, 30, 40], payload);
        Assert.Equal(2, backend.CountCall("content_read_file"));
    }

    [Fact]
    public void NativeFacadeSetContent_ThrowsNativeCallException_OnMountPakFailure()
    {
        var backend = new FakeNativeInteropApi
        {
            ContentMountPakStatus = EngineNativeStatus.InternalError
        };
        using var nativeSet = NativeFacadeFactory.CreateNativeFacadeSet(backend);

        var exception = Assert.Throws<NativeCallException>(() => nativeSet.Content.MountPak("D:/broken.pak"));
        Assert.Equal("content_mount_pak", exception.Operation);
        Assert.Equal(EngineNativeStatus.InternalError, exception.Status);
    }
}
