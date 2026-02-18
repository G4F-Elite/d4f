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
            string profileLogPath = Path.Combine(artifactsRoot, "net", "multiplayer-profile.log");
            Assert.True(File.Exists(multiplayerPath));
            Assert.True(File.Exists(profileLogPath));

            string replayPath = Path.Combine(artifactsRoot, "replay", "recording.json");
            Assert.True(File.Exists(replayPath));
            using JsonDocument replayJson = JsonDocument.Parse(File.ReadAllText(replayPath));
            Assert.Equal(9001UL, replayJson.RootElement.GetProperty("seed").GetUInt64());
            Assert.Equal(0.02, replayJson.RootElement.GetProperty("fixedDeltaSeconds").GetDouble(), 6);

            JsonElement frames = replayJson.RootElement.GetProperty("frames");
            Assert.Equal(13, frames.GetArrayLength());
            Assert.Equal(12L, frames[12].GetProperty("tick").GetInt64());

            JsonElement networkEvents = replayJson.RootElement.GetProperty("networkEvents");
            Assert.Equal(2, networkEvents.GetArrayLength());
            Assert.Equal("capture.frame=12", networkEvents[0].GetString());
            Assert.Equal("net.profile=net/multiplayer-profile.log", networkEvents[1].GetString());

            using JsonDocument multiplayerJson = JsonDocument.Parse(File.ReadAllText(multiplayerPath));
            JsonElement multiplayerRoot = multiplayerJson.RootElement;
            Assert.Equal(9001UL, multiplayerRoot.GetProperty("seed").GetUInt64());
            Assert.Equal(2, multiplayerRoot.GetProperty("connectedClients").GetInt32());
            Assert.True(multiplayerRoot.GetProperty("serverEntityCount").GetInt32() > 0);
            Assert.True(multiplayerRoot.GetProperty("synchronized").GetBoolean());
            Assert.Equal(2, multiplayerRoot.GetProperty("clientStats").GetArrayLength());
            JsonElement serverStats = multiplayerRoot.GetProperty("serverStats");
            Assert.True(serverStats.GetProperty("messagesSent").GetInt32() > 0);
            Assert.True(serverStats.GetProperty("averageSendBandwidthKbps").GetDouble() > 0.0);
            Assert.True(serverStats.GetProperty("averageReceiveBandwidthKbps").GetDouble() >= 0.0);
            Assert.True(serverStats.GetProperty("peakSendBandwidthKbps").GetDouble() >= serverStats.GetProperty("averageSendBandwidthKbps").GetDouble());
            Assert.True(serverStats.GetProperty("peakReceiveBandwidthKbps").GetDouble() >= serverStats.GetProperty("averageReceiveBandwidthKbps").GetDouble());
            foreach (JsonElement clientStats in multiplayerRoot.GetProperty("clientStats").EnumerateArray())
            {
                JsonElement stats = clientStats.GetProperty("stats");
                Assert.True(stats.GetProperty("averageSendBandwidthKbps").GetDouble() >= 0.0);
                Assert.True(stats.GetProperty("averageReceiveBandwidthKbps").GetDouble() >= 0.0);
                Assert.True(stats.GetProperty("peakSendBandwidthKbps").GetDouble() >= stats.GetProperty("averageSendBandwidthKbps").GetDouble());
                Assert.True(stats.GetProperty("peakReceiveBandwidthKbps").GetDouble() >= stats.GetProperty("averageReceiveBandwidthKbps").GetDouble());
            }

            string profileLog = File.ReadAllText(profileLogPath);
            Assert.Contains("server bytesSent=", profileLog, StringComparison.Ordinal);
            Assert.Contains("rttMs=", profileLog, StringComparison.Ordinal);
            Assert.Contains("lossPercent=", profileLog, StringComparison.Ordinal);
            Assert.Contains("sendKbps=", profileLog, StringComparison.Ordinal);
            Assert.Contains("receiveKbps=", profileLog, StringComparison.Ordinal);
            Assert.Contains("peakSendKbps=", profileLog, StringComparison.Ordinal);
            Assert.Contains("peakReceiveKbps=", profileLog, StringComparison.Ordinal);
            Assert.Contains("client-", profileLog, StringComparison.Ordinal);

            string manifestPath = Path.Combine(artifactsRoot, "manifest.json");
            using JsonDocument manifestJson = JsonDocument.Parse(File.ReadAllText(manifestPath));
            JsonElement artifacts = manifestJson.RootElement.GetProperty("artifacts");
            bool hasMultiplayerEntry = artifacts.EnumerateArray()
                .Any(static artifact => string.Equals(
                    artifact.GetProperty("kind").GetString(),
                    "multiplayer-demo",
                    StringComparison.Ordinal));
            Assert.True(hasMultiplayerEntry);
            bool hasNetProfileLog = artifacts.EnumerateArray()
                .Any(static artifact => string.Equals(
                    artifact.GetProperty("kind").GetString(),
                    "net-profile-log",
                    StringComparison.Ordinal));
            Assert.True(hasNetProfileLog);
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
