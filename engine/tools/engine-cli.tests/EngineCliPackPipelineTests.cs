using System.Formats.Tar;
using System.IO.Compression;
using System.Text.Json;
using Engine.Cli;

namespace Engine.Cli.Tests;

public sealed class EngineCliPackPipelineTests
{
    [Fact]
    public void Run_ShouldPackProjectInitializedFromTemplate_WithoutManualBootstrap()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            var runner = new RecordingCommandRunner();
            using var output = new StringWriter();
            using var error = new StringWriter();
            EngineCliApp app = new(output, error, runner);

            int initCode = app.Run(["init", "--name", "DemoGame", "--output", tempRoot]);
            Assert.Equal(0, initCode);

            string projectRoot = Path.Combine(tempRoot, "DemoGame");
            int packCode = app.Run(["pack", "--project", projectRoot, "--manifest", "assets/manifest.json"]);

            Assert.Equal(0, packCode);
            Assert.Single(runner.Invocations);
            CommandInvocation invocation = runner.Invocations[0];
            Assert.Equal("dotnet", invocation.ExecutablePath);
            Assert.Contains("publish", invocation.Arguments);
            Assert.Contains(
                invocation.Arguments,
                arg => string.Equals(
                    arg,
                    Path.Combine(projectRoot, "src", "DemoGame.Runtime", "DemoGame.Runtime.csproj"),
                    StringComparison.OrdinalIgnoreCase));

            Assert.True(File.Exists(Path.Combine(projectRoot, "dist", "package", "Content", "Game.pak")));
            Assert.True(File.Exists(Path.Combine(projectRoot, "dist", "package", "Content", "compiled.manifest.bin")));
            Assert.True(File.Exists(Path.Combine(projectRoot, "dist", "package", "config", "runtime.json")));
            Assert.False(Directory.Exists(Path.Combine(projectRoot, "dist", "package", "Content", "compiled")));

            string runtimeConfigPath = Path.Combine(projectRoot, "dist", "package", "config", "runtime.json");
            using JsonDocument runtimeConfig = JsonDocument.Parse(File.ReadAllText(runtimeConfigPath));
            Assert.Equal("pak-only", runtimeConfig.RootElement.GetProperty("contentMode").GetString());
            Assert.Equal("App", runtimeConfig.RootElement.GetProperty("appDirectory").GetString());
            Assert.Equal("dff_native.dll", runtimeConfig.RootElement.GetProperty("nativeLibrary").GetString());
            Assert.True(runtimeConfig.RootElement.GetProperty("nativeLibrarySearchPath").ValueKind is JsonValueKind.Null);
            Assert.False(runtimeConfig.RootElement.TryGetProperty("compiledManifest", out _));
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public void Run_ShouldInvokePublishAndCopyNativeAndZip_WhenPackOptionsConfigured()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareAssetManifest(tempRoot);
            string runtimeProjectPath = Path.Combine(tempRoot, "src", "Game.Runtime", "Game.Runtime.csproj");
            Directory.CreateDirectory(Path.GetDirectoryName(runtimeProjectPath)!);
            File.WriteAllText(runtimeProjectPath, "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");

            string nativeLibraryPath = Path.Combine(tempRoot, "native", "dff_native.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(nativeLibraryPath)!);
            File.WriteAllText(nativeLibraryPath, "native");

            var runner = new RecordingCommandRunner();
            using var output = new StringWriter();
            using var error = new StringWriter();
            EngineCliApp app = new(output, error, runner);

            int code = app.Run(
            [
                "pack",
                "--project", tempRoot,
                "--manifest", "assets/manifest.json",
                "--publish-project", "src/Game.Runtime/Game.Runtime.csproj",
                "--runtime", "win-x64",
                "--configuration", "Release",
                "--native-lib", "native/dff_native.dll",
                "--zip", "dist/build.zip"
            ]);

            Assert.Equal(0, code);
            Assert.Single(runner.Invocations);
            CommandInvocation invocation = runner.Invocations[0];
            Assert.Equal("dotnet", invocation.ExecutablePath);
            Assert.Equal(tempRoot, invocation.WorkingDirectory);
            Assert.Contains("publish", invocation.Arguments);
            Assert.Contains("-r", invocation.Arguments);
            Assert.Contains("win-x64", invocation.Arguments);

            Assert.True(File.Exists(Path.Combine(tempRoot, "dist", "build.zip")));
            Assert.True(File.Exists(Path.Combine(tempRoot, "dist", "package", "App", "dff_native.dll")));
            Assert.True(File.Exists(Path.Combine(tempRoot, "dist", "package", "Content", "compiled.manifest.bin")));
            Assert.True(File.Exists(Path.Combine(tempRoot, "dist", "package", "config", "runtime.json")));
            Assert.False(Directory.Exists(Path.Combine(tempRoot, "dist", "package", "Content", "compiled")));
            using JsonDocument runtimeConfig = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(tempRoot, "dist", "package", "config", "runtime.json")));
            Assert.Equal("win-x64", runtimeConfig.RootElement.GetProperty("runtime").GetString());
            Assert.Equal("dff_native.dll", runtimeConfig.RootElement.GetProperty("nativeLibrary").GetString());
            Assert.True(runtimeConfig.RootElement.GetProperty("nativeLibrarySearchPath").ValueKind is JsonValueKind.Null);
            Assert.Contains("Package archive created", output.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public void Run_ShouldPackLinuxAndCreateTarGzArchive_WhenRequested()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareAssetManifest(tempRoot);
            string runtimeProjectPath = Path.Combine(tempRoot, "src", "Game.Runtime", "Game.Runtime.csproj");
            Directory.CreateDirectory(Path.GetDirectoryName(runtimeProjectPath)!);
            File.WriteAllText(runtimeProjectPath, "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");

            string nativeLibraryPath = Path.Combine(tempRoot, "native", "libdff_native.so");
            Directory.CreateDirectory(Path.GetDirectoryName(nativeLibraryPath)!);
            File.WriteAllText(nativeLibraryPath, "native-linux");

            var runner = new RecordingCommandRunner();
            using var output = new StringWriter();
            using var error = new StringWriter();
            EngineCliApp app = new(output, error, runner);

            int code = app.Run(
            [
                "pack",
                "--project", tempRoot,
                "--manifest", "assets/manifest.json",
                "--publish-project", "src/Game.Runtime/Game.Runtime.csproj",
                "--runtime", "linux-x64",
                "--configuration", "Release",
                "--native-lib", "native/libdff_native.so",
                "--zip", "dist/build.tar.gz"
            ]);

            Assert.Equal(0, code);
            string archivePath = Path.Combine(tempRoot, "dist", "build.tar.gz");
            Assert.True(File.Exists(archivePath));
            Assert.Equal([0x1F, 0x8B], File.ReadAllBytes(archivePath).Take(2).ToArray());
            Assert.True(File.Exists(Path.Combine(tempRoot, "dist", "package", "App", "libdff_native.so")));
            using JsonDocument runtimeConfig = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(tempRoot, "dist", "package", "config", "runtime.json")));
            Assert.Equal("linux-x64", runtimeConfig.RootElement.GetProperty("runtime").GetString());
            Assert.Equal("App", runtimeConfig.RootElement.GetProperty("appDirectory").GetString());
            Assert.Equal("libdff_native.so", runtimeConfig.RootElement.GetProperty("nativeLibrary").GetString());
            Assert.Equal("$ORIGIN", runtimeConfig.RootElement.GetProperty("nativeLibrarySearchPath").GetString());

            string[] archiveEntries = ListTarGzEntries(archivePath);
            Assert.Contains("App/libdff_native.so", archiveEntries);
            Assert.Contains("Content/Game.pak", archiveEntries);
            Assert.Contains("Content/compiled.manifest.bin", archiveEntries);
            Assert.Contains("config/runtime.json", archiveEntries);
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public void Run_ShouldFailPack_WhenArchiveExtensionUnsupported()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareAssetManifest(tempRoot);
            string runtimeProjectPath = Path.Combine(tempRoot, "src", "Game.Runtime", "Game.Runtime.csproj");
            Directory.CreateDirectory(Path.GetDirectoryName(runtimeProjectPath)!);
            File.WriteAllText(runtimeProjectPath, "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");

            var runner = new RecordingCommandRunner();
            using var output = new StringWriter();
            using var error = new StringWriter();
            EngineCliApp app = new(output, error, runner);

            int code = app.Run(
            [
                "pack",
                "--project", tempRoot,
                "--manifest", "assets/manifest.json",
                "--publish-project", "src/Game.Runtime/Game.Runtime.csproj",
                "--runtime", "linux-x64",
                "--zip", "dist/build.7z"
            ]);

            Assert.Equal(1, code);
            Assert.Contains("Unsupported archive extension", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public void Run_ShouldFailPack_WhenPublishCommandFails()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareAssetManifest(tempRoot);
            string runtimeProjectPath = Path.Combine(tempRoot, "src", "Runtime", "Runtime.csproj");
            Directory.CreateDirectory(Path.GetDirectoryName(runtimeProjectPath)!);
            File.WriteAllText(runtimeProjectPath, "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");

            var runner = new RecordingCommandRunner { ExitCode = 3 };
            using var output = new StringWriter();
            using var error = new StringWriter();
            EngineCliApp app = new(output, error, runner);

            int code = app.Run(
            [
                "pack",
                "--project", tempRoot,
                "--manifest", "assets/manifest.json",
                "--publish-project", "src/Runtime/Runtime.csproj"
            ]);

            Assert.Equal(1, code);
            Assert.Contains("dotnet publish failed with exit code 3", error.ToString(), StringComparison.Ordinal);
            Assert.Single(runner.Invocations);
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public void Run_ShouldSkipPublish_WhenRuntimeProjectIsMissing()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareAssetManifest(tempRoot);
            var runner = new RecordingCommandRunner();
            using var output = new StringWriter();
            using var error = new StringWriter();
            EngineCliApp app = new(output, error, runner);

            int code = app.Run(
            [
                "pack",
                "--project", tempRoot,
                "--manifest", "assets/manifest.json",
                "--runtime", "win-x64"
            ]);

            Assert.Equal(0, code);
            Assert.Empty(runner.Invocations);
            Assert.Contains("Publish skipped: runtime .csproj was not found", output.ToString(), StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(tempRoot, "dist", "package", "Content", "Game.pak")));
            Assert.True(File.Exists(Path.Combine(tempRoot, "dist", "package", "config", "runtime.json")));
            Assert.Equal(string.Empty, error.ToString());
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    private static void PrepareAssetManifest(string rootPath)
    {
        string assetsDirectory = Path.Combine(rootPath, "assets");
        Directory.CreateDirectory(assetsDirectory);
        File.WriteAllText(Path.Combine(assetsDirectory, "example.txt"), "content");
        File.WriteAllText(
            Path.Combine(assetsDirectory, "manifest.json"),
            """
            {
              "version": 1,
              "assets": [
                {
                  "path": "example.txt",
                  "kind": "text"
                }
              ]
            }
            """);
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"engine-cli-pack-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static string[] ListTarGzEntries(string archivePath)
    {
        var entries = new List<string>();
        using FileStream compressed = File.OpenRead(archivePath);
        using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
        using var reader = new TarReader(gzip);
        TarEntry? entry;
        while ((entry = reader.GetNextEntry()) is not null)
        {
            if (string.IsNullOrWhiteSpace(entry.Name))
            {
                continue;
            }

            entries.Add(entry.Name.Replace('\\', '/'));
        }

        return entries.ToArray();
    }

    private sealed class RecordingCommandRunner : IExternalCommandRunner
    {
        public List<CommandInvocation> Invocations { get; } = [];

        public int ExitCode { get; init; }

        public int Run(
            string executablePath,
            IReadOnlyList<string> arguments,
            string workingDirectory,
            TextWriter stdout,
            TextWriter stderr)
        {
            Invocations.Add(new CommandInvocation(executablePath, arguments.ToArray(), workingDirectory));
            return ExitCode;
        }
    }

    private sealed record CommandInvocation(string ExecutablePath, string[] Arguments, string WorkingDirectory);
}
