using System.Text.Json;
using Engine.Cli;

namespace Engine.Cli.Tests;

public sealed class EngineCliTestCaptureOptionsTests
{
    [Fact]
    public void Run_TestCommand_ShouldHonorCaptureAndReplayOptions()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            var runner = new RecordingCommandRunner();
            using var output = new StringWriter();
            using var error = new StringWriter();
            EngineCliApp app = new(output, error, runner);

            int code = app.Run(
            [
                "test",
                "--project", tempRoot,
                "--out", "artifacts/tests",
                "--capture-frame", "12",
                "--seed", "9001",
                "--fixed-dt", "0.02"
            ]);

            Assert.Equal(0, code);
            Assert.Single(runner.Invocations);

            string artifactsRoot = Path.Combine(tempRoot, "artifacts", "tests");
            Assert.True(File.Exists(Path.Combine(artifactsRoot, "screenshots", "frame-0012.png")));
            Assert.True(File.Exists(Path.Combine(artifactsRoot, "screenshots", "frame-0012.rgba8.bin")));
            Assert.True(File.Exists(Path.Combine(artifactsRoot, "dumps", "albedo-0012.png")));
            Assert.True(File.Exists(Path.Combine(artifactsRoot, "dumps", "normals-0012.png")));
            Assert.True(File.Exists(Path.Combine(artifactsRoot, "dumps", "depth-0012.png")));
            Assert.True(File.Exists(Path.Combine(artifactsRoot, "dumps", "shadow-0012.png")));
            string multiplayerPath = Path.Combine(artifactsRoot, "net", "multiplayer-demo.json");
            Assert.True(File.Exists(multiplayerPath));

            string replayPath = Path.Combine(artifactsRoot, "replay", "recording.json");
            Assert.True(File.Exists(replayPath));
            using JsonDocument replayJson = JsonDocument.Parse(File.ReadAllText(replayPath));
            Assert.Equal(9001UL, replayJson.RootElement.GetProperty("seed").GetUInt64());
            Assert.Equal(0.02, replayJson.RootElement.GetProperty("fixedDeltaSeconds").GetDouble(), 6);

            JsonElement frames = replayJson.RootElement.GetProperty("frames");
            Assert.Equal(13, frames.GetArrayLength());
            Assert.Equal(12L, frames[12].GetProperty("tick").GetInt64());

            JsonElement networkEvents = replayJson.RootElement.GetProperty("networkEvents");
            Assert.Single(networkEvents.EnumerateArray());
            Assert.Equal("capture.frame=12", networkEvents[0].GetString());

            using JsonDocument multiplayerJson = JsonDocument.Parse(File.ReadAllText(multiplayerPath));
            JsonElement multiplayerRoot = multiplayerJson.RootElement;
            Assert.Equal(9001UL, multiplayerRoot.GetProperty("seed").GetUInt64());
            Assert.Equal(2, multiplayerRoot.GetProperty("connectedClients").GetInt32());
            Assert.True(multiplayerRoot.GetProperty("serverEntityCount").GetInt32() > 0);
            Assert.True(multiplayerRoot.GetProperty("synchronized").GetBoolean());
            Assert.Equal(2, multiplayerRoot.GetProperty("clientStats").GetArrayLength());
            Assert.True(multiplayerRoot.GetProperty("serverStats").GetProperty("messagesSent").GetInt32() > 0);

            string manifestPath = Path.Combine(artifactsRoot, "manifest.json");
            using JsonDocument manifestJson = JsonDocument.Parse(File.ReadAllText(manifestPath));
            JsonElement artifacts = manifestJson.RootElement.GetProperty("artifacts");
            bool hasMultiplayerEntry = artifacts.EnumerateArray()
                .Any(static artifact => string.Equals(
                    artifact.GetProperty("kind").GetString(),
                    "multiplayer-demo",
                    StringComparison.Ordinal));
            Assert.True(hasMultiplayerEntry);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"engine-cli-capture-options-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class RecordingCommandRunner : IExternalCommandRunner
    {
        public List<CommandInvocation> Invocations { get; } = [];

        public int Run(
            string executablePath,
            IReadOnlyList<string> arguments,
            string workingDirectory,
            TextWriter stdout,
            TextWriter stderr)
        {
            Invocations.Add(new CommandInvocation(executablePath, arguments.ToArray(), workingDirectory));
            return 0;
        }
    }

    private sealed record CommandInvocation(string ExecutablePath, string[] Arguments, string WorkingDirectory);
}
