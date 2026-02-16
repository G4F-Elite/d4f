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
            Assert.True(File.Exists(Path.Combine(projectRoot, "dist", "package", "config", "runtime.json")));
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
            Assert.True(File.Exists(Path.Combine(tempRoot, "dist", "package", "config", "runtime.json")));
            Assert.Contains("Package archive created", output.ToString(), StringComparison.Ordinal);
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
