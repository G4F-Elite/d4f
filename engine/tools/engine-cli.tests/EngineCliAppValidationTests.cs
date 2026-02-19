using System.Text.Json;
using Engine.Cli;

namespace Engine.Cli.Tests;

public sealed class EngineCliAppValidationTests
{
    [Fact]
    public void Run_ShouldFailBuild_WhenProjectDirectoryMissing()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        EngineCliApp app = new(output, error);

        int code = app.Run(["build", "--project", "missing-project"]);

        Assert.Equal(1, code);
        Assert.Contains("Project directory does not exist", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Run_ShouldFailRun_WhenRuntimeProjectMissing()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(tempRoot, "src"));

            using var output = new StringWriter();
            using var error = new StringWriter();
            EngineCliApp app = new(output, error);

            int code = app.Run(["run", "--project", tempRoot]);

            Assert.Equal(1, code);
            Assert.Contains("Runtime .csproj was not found", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public void Run_ShouldReportDebugView_WhenFlagProvided()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            string runtimeProjectPath = PrepareRuntimeProject(tempRoot, "DemoRuntime");
            var runner = new RecordingCommandRunner();

            using var output = new StringWriter();
            using var error = new StringWriter();
            EngineCliApp app = new(output, error, runner);

            int code = app.Run(["run", "--project", tempRoot, "--debug-view", "depth"]);

            Assert.Equal(0, code);
            Assert.Single(runner.Invocations);
            CommandInvocation invocation = runner.Invocations[0];
            Assert.Equal("dotnet", invocation.ExecutablePath);
            Assert.Equal(tempRoot, invocation.WorkingDirectory);
            Assert.Contains("run", invocation.Arguments);
            Assert.Contains("--project", invocation.Arguments);
            Assert.Contains(runtimeProjectPath, invocation.Arguments);
            Assert.Contains("--debug-view", invocation.Arguments);
            Assert.Contains("depth", invocation.Arguments);

            string stdout = output.ToString();
            Assert.Contains("Run completed for", stdout, StringComparison.Ordinal);
            Assert.Contains("debug view: depth", stdout, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(string.Empty, error.ToString());
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public void Run_ShouldInvokeDotnetBuild_WhenRuntimeProjectExists()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            string runtimeProjectPath = PrepareRuntimeProject(tempRoot, "BuildRuntime");
            var runner = new RecordingCommandRunner();
            using var output = new StringWriter();
            using var error = new StringWriter();
            EngineCliApp app = new(output, error, runner);

            int code = app.Run(["build", "--project", tempRoot, "--configuration", "Release"]);

            Assert.Equal(0, code);
            Assert.Single(runner.Invocations);
            CommandInvocation invocation = runner.Invocations[0];
            Assert.Equal("dotnet", invocation.ExecutablePath);
            Assert.Equal(tempRoot, invocation.WorkingDirectory);
            Assert.Contains("build", invocation.Arguments);
            Assert.Contains(runtimeProjectPath, invocation.Arguments);
            Assert.Contains("-c", invocation.Arguments);
            Assert.Contains("Release", invocation.Arguments);
            Assert.Contains("Build completed", output.ToString(), StringComparison.Ordinal);
            Assert.Equal(string.Empty, error.ToString());
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public void Run_ShouldFailPack_WhenManifestMissing()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            using var output = new StringWriter();
            using var error = new StringWriter();
            EngineCliApp app = new(output, error);

            int code = app.Run(["pack", "--project", tempRoot, "--manifest", "assets/manifest.json"]);

            Assert.Equal(1, code);
            Assert.Contains("Manifest file was not found", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public void Run_ShouldFailMultiplayerDemo_WhenProjectDirectoryMissing()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        EngineCliApp app = new(output, error);

        int code = app.Run(["multiplayer", "demo", "--project", "missing-project"]);

        Assert.Equal(1, code);
        Assert.Contains("Project directory does not exist", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Run_ShouldCreateMultiplayerRuntimeArtifacts_WhenCommandSucceeds()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            using var output = new StringWriter();
            using var error = new StringWriter();
            EngineCliApp app = new(output, error);

            int code = app.Run(
            [
                "multiplayer",
                "demo",
                "--project", tempRoot,
                "--out", "artifacts/runtime-multiplayer",
                "--seed", "9001",
                "--fixed-dt", "0.0166667"
            ]);

            Assert.Equal(0, code);

            string outputRoot = Path.Combine(tempRoot, "artifacts", "runtime-multiplayer");
            string summaryPath = Path.Combine(outputRoot, "net", "multiplayer-demo.json");
            string profilePath = Path.Combine(outputRoot, "net", "multiplayer-profile.log");
            string snapshotPath = Path.Combine(outputRoot, "net", "multiplayer-snapshot.bin");
            string rpcPath = Path.Combine(outputRoot, "net", "multiplayer-rpc.bin");

            Assert.True(File.Exists(summaryPath));
            Assert.True(File.Exists(profilePath));
            Assert.True(File.Exists(snapshotPath));
            Assert.True(File.Exists(rpcPath));

            using JsonDocument summary = JsonDocument.Parse(File.ReadAllText(summaryPath));
            JsonElement root = summary.RootElement;
            Assert.Equal(9001L, root.GetProperty("seed").GetInt64());
            Assert.True(root.GetProperty("connectedClients").GetInt32() >= 2);

            JsonElement runtimeTransport = root.GetProperty("runtimeTransport");
            Assert.True(runtimeTransport.TryGetProperty("enabled", out _));
            Assert.True(runtimeTransport.TryGetProperty("succeeded", out _));

            string stdout = output.ToString();
            Assert.Contains("Multiplayer runtime demo artifacts created", stdout, StringComparison.Ordinal);
            Assert.Contains("Native transport:", stdout, StringComparison.Ordinal);
            Assert.Equal(string.Empty, error.ToString());
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public void Run_ShouldFailMultiplayerOrchestration_WhenCliProjectMissing()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            using var output = new StringWriter();
            using var error = new StringWriter();
            EngineCliApp app = new(output, error);

            int code = app.Run(
            [
                "multiplayer",
                "orchestrate",
                "--project", tempRoot,
                "--cli-project", Path.Combine(tempRoot, "missing", "Engine.Cli.csproj")
            ]);

            Assert.Equal(1, code);
            Assert.Contains("CLI project file does not exist", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public void Run_ShouldCreateMultiplayerOrchestrationSummary_WhenNodeProcessesSucceed()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            string cliProjectPath = Path.Combine(tempRoot, "tools", "engine-cli", "Engine.Cli.csproj");
            Directory.CreateDirectory(Path.GetDirectoryName(cliProjectPath)!);
            File.WriteAllText(cliProjectPath, "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");

            var runner = new RecordingCommandRunner
            {
                OnRun = (executablePath, arguments, _, _, _) =>
                {
                    if (!string.Equals(executablePath, "dotnet", StringComparison.OrdinalIgnoreCase) ||
                        !arguments.Contains("multiplayer") ||
                        !arguments.Contains("demo"))
                    {
                        return;
                    }

                    int outIndex = Array.FindIndex(arguments.ToArray(), static arg => string.Equals(arg, "--out", StringComparison.Ordinal));
                    Assert.True(outIndex >= 0);
                    Assert.True(outIndex + 1 < arguments.Count);
                    string roleOutputDirectory = arguments[outIndex + 1];
                    string summaryPath = Path.Combine(roleOutputDirectory, "net", "multiplayer-demo.json");
                    Directory.CreateDirectory(Path.GetDirectoryName(summaryPath)!);
                    File.WriteAllText(
                        summaryPath,
                        """
                        {
                          "runtimeTransport": {
                            "enabled": true,
                            "succeeded": true,
                            "serverMessagesReceived": 3,
                            "clientMessagesReceived": 4
                          }
                        }
                        """);
                }
            };

            using var output = new StringWriter();
            using var error = new StringWriter();
            EngineCliApp app = new(output, error, runner);

            int code = app.Run(
            [
                "multiplayer",
                "orchestrate",
                "--project", tempRoot,
                "--out", "artifacts/runtime-multiplayer-orchestration",
                "--configuration", "Release",
                "--seed", "9001",
                "--fixed-dt", "0.0166667",
                "--require-native-transport", "true",
                "--cli-project", cliProjectPath
            ]);

            Assert.Equal(0, code);
            Assert.Equal(3, runner.Invocations.Count);

            string summaryPath = Path.Combine(tempRoot, "artifacts", "runtime-multiplayer-orchestration", "net", "multiplayer-orchestration.json");
            Assert.True(File.Exists(summaryPath));

            using JsonDocument summary = JsonDocument.Parse(File.ReadAllText(summaryPath));
            JsonElement root = summary.RootElement;
            Assert.True(root.GetProperty("allSucceeded").GetBoolean());
            JsonElement nodes = root.GetProperty("nodes");
            Assert.Equal(3, nodes.GetArrayLength());

            string stdout = output.ToString();
            Assert.Contains("Multiplayer orchestration summary written", stdout, StringComparison.Ordinal);
            Assert.Equal(string.Empty, error.ToString());
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public void Run_ShouldFailNfrProof_WhenRuntimeProjectMissing()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(tempRoot, "src"));

            using var output = new StringWriter();
            using var error = new StringWriter();
            EngineCliApp app = new(output, error);

            int code = app.Run(["nfr", "proof", "--project", tempRoot]);

            Assert.Equal(1, code);
            Assert.Contains("Runtime .csproj was not found", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public void Run_ShouldWriteNfrProof_WhenChecksPass()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            _ = PrepareRuntimeProject(tempRoot, "NfrRuntime");
            var runner = new RecordingCommandRunner();

            using var output = new StringWriter();
            using var error = new StringWriter();
            EngineCliApp app = new(output, error, runner);

            int testCode = app.Run(["test", "--project", tempRoot, "--out", "artifacts/tests", "--configuration", "Release"]);
            Assert.Equal(0, testCode);

            int code = app.Run(
            [
                "nfr",
                "proof",
                "--project", tempRoot,
                "--out", "artifacts/nfr/release-proof.json",
                "--configuration", "Release"
            ]);

            Assert.Equal(0, code);
            string proofPath = Path.Combine(tempRoot, "artifacts", "nfr", "release-proof.json");
            Assert.True(File.Exists(proofPath));

            using JsonDocument proof = JsonDocument.Parse(File.ReadAllText(proofPath));
            JsonElement root = proof.RootElement;
            Assert.True(root.GetProperty("isSuccess").GetBoolean());
            Assert.Equal("Release", root.GetProperty("configuration").GetString());
            JsonElement checks = root.GetProperty("checks");
            Assert.True(checks.GetProperty("allArtifactsPresent").GetBoolean());
            Assert.True(checks.GetProperty("releaseInteropBudgetsMatch").GetBoolean());

            string stdout = output.ToString();
            Assert.Contains("NFR release proof written", stdout, StringComparison.Ordinal);
            Assert.Contains("NFR release proof checks passed.", stdout, StringComparison.Ordinal);
            Assert.Equal(string.Empty, error.ToString());
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public void Run_ShouldCreateProjectStructure_WhenNewValid()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            using var output = new StringWriter();
            using var error = new StringWriter();
            EngineCliApp app = new(output, error);

            int code = app.Run(["new", "--name", "DemoGame", "--output", tempRoot]);

            Assert.Equal(0, code);
            string projectRoot = Path.Combine(tempRoot, "DemoGame");
            Assert.True(Directory.Exists(Path.Combine(projectRoot, "src")));
            Assert.True(Directory.Exists(Path.Combine(projectRoot, "assets")));
            Assert.True(Directory.Exists(Path.Combine(projectRoot, "GameAssets")));
            Assert.True(Directory.Exists(Path.Combine(projectRoot, "tests")));
            Assert.True(File.Exists(Path.Combine(projectRoot, "project.json")));
            Assert.True(File.Exists(Path.Combine(projectRoot, "src", "DemoGame.Runtime", "DemoGame.Runtime.csproj")));
            Assert.True(File.Exists(Path.Combine(projectRoot, "src", "DemoGame.Runtime", "Program.cs")));

            string manifestPath = Path.Combine(projectRoot, "assets", "manifest.json");
            string manifestJson = File.ReadAllText(manifestPath);
            Assert.Contains("\"version\": 1", manifestJson, StringComparison.Ordinal);
            Assert.Contains("\"path\": \"example.txt\"", manifestJson, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public void Run_ShouldBakeCompiledAssetsAndManifest()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareAssetManifest(tempRoot);

            using var output = new StringWriter();
            using var error = new StringWriter();
            EngineCliApp app = new(output, error);

            int code = app.Run(["bake", "--project", tempRoot, "--manifest", "assets/manifest.json"]);

            Assert.Equal(0, code);
            Assert.True(File.Exists(Path.Combine(tempRoot, "build", "content", "Game.pak")));
            Assert.True(File.Exists(Path.Combine(tempRoot, "build", "content", "compiled", "text", "example.txt.bin")));
            Assert.True(File.Exists(Path.Combine(tempRoot, "build", "content", "compiled.manifest.bin")));
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public void Run_ShouldCreatePreviewArtifactsManifest()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareAssetManifest(tempRoot);

            using var output = new StringWriter();
            using var error = new StringWriter();
            EngineCliApp app = new(output, error);

            int code = app.Run(
            [
                "preview",
                "--project", tempRoot,
                "--manifest", "assets/manifest.json",
                "--out", "artifacts/preview"
            ]);

            Assert.Equal(0, code);
            string previewRoot = Path.Combine(tempRoot, "artifacts", "preview");
            string manifestPath = Path.Combine(previewRoot, "manifest.json");
            Assert.True(File.Exists(manifestPath));

            using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
            JsonElement artifacts = manifest.RootElement.GetProperty("artifacts");
            Assert.True(artifacts.GetArrayLength() >= 1);
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public void Run_ShouldCreateTestArtifacts_WhenDotnetTestSucceeds()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            var runner = new RecordingCommandRunner();
            using var output = new StringWriter();
            using var error = new StringWriter();
            EngineCliApp app = new(output, error, runner);

            int code = app.Run(["test", "--project", tempRoot, "--out", "artifacts/tests", "--configuration", "Debug"]);

            Assert.Equal(0, code);
            Assert.Single(runner.Invocations);
            CommandInvocation invocation = runner.Invocations[0];
            Assert.Equal("dotnet", invocation.ExecutablePath);
            Assert.Contains("test", invocation.Arguments);
            Assert.Contains("-c", invocation.Arguments);
            Assert.Contains("Debug", invocation.Arguments);

            Assert.True(File.Exists(Path.Combine(tempRoot, "artifacts", "tests", "manifest.json")));
            string screenshotPath = Path.Combine(tempRoot, "artifacts", "tests", "screenshots", "frame-0001.png");
            Assert.True(File.Exists(screenshotPath));
            Assert.True(File.Exists(Path.Combine(tempRoot, "artifacts", "tests", "dumps", "albedo-0001.png")));
            Assert.True(File.Exists(Path.Combine(tempRoot, "artifacts", "tests", "screenshots", "frame-0001.rgba8.bin")));
            Assert.True(File.Exists(Path.Combine(tempRoot, "artifacts", "tests", "screenshots", "frame-0001.rgba16f.bin")));
            Assert.True(File.Exists(Path.Combine(tempRoot, "artifacts", "tests", "screenshots", "frame-0001.rgba16f.exr")));
            Assert.Contains("Render stats: draw=", output.ToString(), StringComparison.Ordinal);

            (uint width, uint height) = ReadPngDimensions(screenshotPath);
            Assert.Equal((uint)64, width);
            Assert.Equal((uint)64, height);
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public void Run_ShouldFailTest_WhenDotnetTestFails()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            var runner = new RecordingCommandRunner { ExitCode = 3 };
            using var output = new StringWriter();
            using var error = new StringWriter();
            EngineCliApp app = new(output, error, runner);

            int code = app.Run(["test", "--project", tempRoot, "--out", "artifacts/tests"]);

            Assert.Equal(1, code);
            Assert.Contains("dotnet test failed with exit code 3", error.ToString(), StringComparison.Ordinal);
            Assert.Single(runner.Invocations);
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public void Run_ShouldFailDoctor_WhenRequiredFilesMissing()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            var runner = new RecordingCommandRunner();
            using var output = new StringWriter();
            using var error = new StringWriter();
            EngineCliApp app = new(output, error, runner);

            int code = app.Run(["doctor", "--project", tempRoot]);

            Assert.Equal(1, code);
            Assert.Contains("Missing required path", error.ToString(), StringComparison.Ordinal);
            Assert.Equal(2, runner.Invocations.Count);
            Assert.Equal("dotnet", runner.Invocations[0].ExecutablePath);
            Assert.Equal("cmake", runner.Invocations[1].ExecutablePath);
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public void Run_ShouldPassDoctor_WhenChecksSucceed()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(tempRoot, "assets"));
            Directory.CreateDirectory(Path.Combine(tempRoot, "src"));
            File.WriteAllText(Path.Combine(tempRoot, "project.json"), "{}");
            File.WriteAllText(Path.Combine(tempRoot, "assets", "manifest.json"), """
            {
              "version": 1,
              "assets": [
                { "path": "example.txt", "kind": "text" }
              ]
            }
            """);

            var runner = new RecordingCommandRunner();
            using var output = new StringWriter();
            using var error = new StringWriter();
            EngineCliApp app = new(output, error, runner);

            int code = app.Run(["doctor", "--project", tempRoot]);

            Assert.Equal(0, code);
            Assert.Equal(2, runner.Invocations.Count);
            Assert.Contains("Doctor checks passed.", output.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public void Run_ShouldCreateApiDump_WhenHeaderValid()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            string headerPath = Path.Combine(tempRoot, "engine_native.h");
            File.WriteAllText(
                headerPath,
                """
                #define ENGINE_NATIVE_API_VERSION 9u
                ENGINE_NATIVE_API int engine_create(const void* desc);
                """);

            using var output = new StringWriter();
            using var error = new StringWriter();
            EngineCliApp app = new(output, error);

            int code = app.Run(
            [
                "api",
                "dump",
                "--header", headerPath,
                "--out", Path.Combine(tempRoot, "api", "dump.json")
            ]);

            Assert.Equal(0, code);
            string dumpPath = Path.Combine(tempRoot, "api", "dump.json");
            Assert.True(File.Exists(dumpPath));
            string json = File.ReadAllText(dumpPath);
            Assert.Contains("\"apiVersion\": 9", json, StringComparison.Ordinal);
            Assert.Contains("\"name\": \"engine_create\"", json, StringComparison.Ordinal);
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

    private static string PrepareRuntimeProject(string rootPath, string runtimeName)
    {
        string runtimeDirectory = Path.Combine(rootPath, "src", runtimeName);
        Directory.CreateDirectory(runtimeDirectory);
        string projectPath = Path.Combine(runtimeDirectory, $"{runtimeName}.csproj");
        File.WriteAllText(
            projectPath,
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net9.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        return projectPath;
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"engine-cli-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static (uint Width, uint Height) ReadPngDimensions(string filePath)
    {
        byte[] bytes = File.ReadAllBytes(filePath);
        Assert.True(bytes.Length >= 24);
        Assert.Equal((byte)0x89, bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'N', bytes[2]);
        Assert.Equal((byte)'G', bytes[3]);
        Assert.Equal((byte)'I', bytes[12]);
        Assert.Equal((byte)'H', bytes[13]);
        Assert.Equal((byte)'D', bytes[14]);
        Assert.Equal((byte)'R', bytes[15]);

        uint width = ((uint)bytes[16] << 24) | ((uint)bytes[17] << 16) | ((uint)bytes[18] << 8) | bytes[19];
        uint height = ((uint)bytes[20] << 24) | ((uint)bytes[21] << 16) | ((uint)bytes[22] << 8) | bytes[23];
        return (width, height);
    }

    private sealed class RecordingCommandRunner : IExternalCommandRunner
    {
        public List<CommandInvocation> Invocations { get; } = [];

        public int ExitCode { get; init; }

        public Action<string, IReadOnlyList<string>, string, TextWriter, TextWriter>? OnRun { get; init; }

        public int Run(
            string executablePath,
            IReadOnlyList<string> arguments,
            string workingDirectory,
            TextWriter stdout,
            TextWriter stderr)
        {
            Invocations.Add(new CommandInvocation(executablePath, arguments.ToArray(), workingDirectory));
            OnRun?.Invoke(executablePath, arguments, workingDirectory, stdout, stderr);
            return ExitCode;
        }
    }

    private sealed record CommandInvocation(string ExecutablePath, string[] Arguments, string WorkingDirectory);
}
