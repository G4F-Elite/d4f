using System.IO;
using Engine.Content;
using Engine.Core.Abstractions;

namespace Engine.Tests.Content;

public sealed class PackagedRuntimeContentBootstrapTests
{
    [Fact]
    public void GetDefaultRuntimeConfigPath_ShouldResolveUnderParentConfigDirectory()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            string appDirectory = Path.Combine(tempRoot, "package", "App");
            Directory.CreateDirectory(appDirectory);
            string expected = Path.GetFullPath(Path.Combine(appDirectory, "..", "config", "runtime.json"));

            string actual = PackagedRuntimeContentBootstrap.GetDefaultRuntimeConfigPath(appDirectory);

            Assert.Equal(expected, actual);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void ConfigureFromRuntimeConfig_ShouldMountPakAndEnablePakOnlyMode()
    {
        string packageRoot = CreatePackagedRuntime(
            """
            {
              "contentMode": "pak-only",
              "contentPak": "Content/Game.pak"
            }
            """);
        var runtime = new RecordingContentRuntime();

        try
        {
            string configPath = Path.Combine(packageRoot, "config", "runtime.json");
            string expectedPakPath = Path.GetFullPath(Path.Combine(packageRoot, "Content", "Game.pak"));

            MountedContentAssetsProvider provider =
                PackagedRuntimeContentBootstrap.ConfigureFromRuntimeConfig(runtime, configPath);

            Assert.NotNull(provider);
            Assert.Equal(expectedPakPath, runtime.LastMountedPakPath);
            Assert.Equal(AssetsRuntimeMode.PakOnly, Assets.GetRuntimeMode());
        }
        finally
        {
            Assets.Reset();
            Directory.Delete(packageRoot, recursive: true);
        }
    }

    [Fact]
    public void ConfigureFromRuntimeConfig_ShouldThrowWhenRuntimeConfigIsMissing()
    {
        var runtime = new RecordingContentRuntime();
        string missingPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}", "runtime.json");

        FileNotFoundException exception = Assert.Throws<FileNotFoundException>(() =>
            PackagedRuntimeContentBootstrap.ConfigureFromRuntimeConfig(runtime, missingPath));

        Assert.Equal(Path.GetFullPath(missingPath), exception.FileName);
    }

    [Fact]
    public void ConfigureFromRuntimeConfig_ShouldThrowWhenRuntimeModeIsUnsupported()
    {
        string packageRoot = CreatePackagedRuntime(
            """
            {
              "contentMode": "development",
              "contentPak": "Content/Game.pak"
            }
            """);
        var runtime = new RecordingContentRuntime();

        try
        {
            string configPath = Path.Combine(packageRoot, "config", "runtime.json");

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
                PackagedRuntimeContentBootstrap.ConfigureFromRuntimeConfig(runtime, configPath));

            Assert.Contains("Unsupported runtime content mode", exception.Message, StringComparison.Ordinal);
            Assert.Null(runtime.LastMountedPakPath);
        }
        finally
        {
            Assets.Reset();
            Directory.Delete(packageRoot, recursive: true);
        }
    }

    [Fact]
    public void ConfigureFromRuntimeConfig_ShouldThrowWhenContentPakPathIsMissing()
    {
        string packageRoot = CreatePackagedRuntime(
            """
            {
              "contentMode": "pak-only"
            }
            """);
        var runtime = new RecordingContentRuntime();

        try
        {
            string configPath = Path.Combine(packageRoot, "config", "runtime.json");

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
                PackagedRuntimeContentBootstrap.ConfigureFromRuntimeConfig(runtime, configPath));

            Assert.Contains("'contentPak' property", exception.Message, StringComparison.Ordinal);
            Assert.Null(runtime.LastMountedPakPath);
        }
        finally
        {
            Assets.Reset();
            Directory.Delete(packageRoot, recursive: true);
        }
    }

    [Fact]
    public void ConfigureFromRuntimeConfig_ShouldThrowWhenPakFileIsMissing()
    {
        string packageRoot = CreatePackagedRuntime(
            """
            {
              "contentMode": "pak-only",
              "contentPak": "Content/Unknown.pak"
            }
            """);
        var runtime = new RecordingContentRuntime();

        try
        {
            string configPath = Path.Combine(packageRoot, "config", "runtime.json");

            FileNotFoundException exception = Assert.Throws<FileNotFoundException>(() =>
                PackagedRuntimeContentBootstrap.ConfigureFromRuntimeConfig(runtime, configPath));

            Assert.EndsWith(
                Path.Combine("Content", "Unknown.pak"),
                exception.FileName ?? string.Empty,
                StringComparison.OrdinalIgnoreCase);
            Assert.Null(runtime.LastMountedPakPath);
        }
        finally
        {
            Assets.Reset();
            Directory.Delete(packageRoot, recursive: true);
        }
    }

    [Fact]
    public void ConfigureFromRuntimeConfig_ShouldThrowWhenJsonIsMalformed()
    {
        string packageRoot = CreateTempDirectory();
        try
        {
            string configDirectory = Path.Combine(packageRoot, "config");
            Directory.CreateDirectory(configDirectory);
            string configPath = Path.Combine(configDirectory, "runtime.json");
            File.WriteAllText(configPath, "{ \"contentMode\": ");
            var runtime = new RecordingContentRuntime();

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
                PackagedRuntimeContentBootstrap.ConfigureFromRuntimeConfig(runtime, configPath));

            Assert.Contains("invalid JSON", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Assets.Reset();
            Directory.Delete(packageRoot, recursive: true);
        }
    }

    private static string CreatePackagedRuntime(string runtimeConfigJson)
    {
        string packageRoot = CreateTempDirectory();
        string configDirectory = Path.Combine(packageRoot, "config");
        string contentDirectory = Path.Combine(packageRoot, "Content");
        Directory.CreateDirectory(configDirectory);
        Directory.CreateDirectory(contentDirectory);
        File.WriteAllText(Path.Combine(configDirectory, "runtime.json"), runtimeConfigJson);
        File.WriteAllBytes(Path.Combine(contentDirectory, "Game.pak"), [0x01, 0x02, 0x03]);
        return packageRoot;
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"engine-content-bootstrap-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class RecordingContentRuntime : IContentRuntimeFacade
    {
        public string? LastMountedPakPath { get; private set; }

        public void MountPak(string pakPath)
        {
            LastMountedPakPath = Path.GetFullPath(pakPath);
        }

        public void MountDirectory(string directoryPath)
        {
            throw new NotSupportedException("Development directory mounts are not used by packaged runtime bootstrap.");
        }

        public byte[] ReadFile(string assetPath)
        {
            throw new NotSupportedException("Asset reads are not used by packaged runtime bootstrap.");
        }
    }
}
