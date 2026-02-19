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
                "--host", "hidden-window",
                "--capture-frame", "12",
                "--seed", "9001",
                "--fixed-dt", "0.02"
            ]);

            Assert.Equal(0, code);
            Assert.Single(runner.Invocations);

            string artifactsRoot = Path.Combine(tempRoot, "artifacts", "tests");
            Assert.True(File.Exists(Path.Combine(artifactsRoot, "screenshots", "frame-0012.png")));
            Assert.True(File.Exists(Path.Combine(artifactsRoot, "screenshots", "frame-0012.rgba8.bin")));
            Assert.True(File.Exists(Path.Combine(artifactsRoot, "screenshots", "frame-0012.rgba16f.bin")));
            Assert.True(File.Exists(Path.Combine(artifactsRoot, "dumps", "albedo-0012.png")));
            Assert.True(File.Exists(Path.Combine(artifactsRoot, "dumps", "normals-0012.png")));
            Assert.True(File.Exists(Path.Combine(artifactsRoot, "dumps", "depth-0012.png")));
            Assert.True(File.Exists(Path.Combine(artifactsRoot, "dumps", "shadow-0012.png")));
            string multiplayerPath = Path.Combine(artifactsRoot, "net", "multiplayer-demo.json");
            string profileLogPath = Path.Combine(artifactsRoot, "net", "multiplayer-profile.log");
            string snapshotBinaryPath = Path.Combine(artifactsRoot, "net", "multiplayer-snapshot.bin");
            string renderStatsPath = Path.Combine(artifactsRoot, "render", "frame-stats.json");
            string hostConfigPath = Path.Combine(artifactsRoot, "runtime", "test-host.json");
            string runtimePerfPath = Path.Combine(artifactsRoot, "runtime", "perf-metrics.json");
            Assert.True(File.Exists(multiplayerPath));
            Assert.True(File.Exists(profileLogPath));
            Assert.True(File.Exists(snapshotBinaryPath));
            Assert.True(File.Exists(renderStatsPath));
            Assert.True(File.Exists(hostConfigPath));
            Assert.True(File.Exists(runtimePerfPath));

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
            JsonElement timedNetworkEvents = replayJson.RootElement.GetProperty("timedNetworkEvents");
            Assert.Equal(2, timedNetworkEvents.GetArrayLength());
            Assert.Equal(0L, timedNetworkEvents[0].GetProperty("tick").GetInt64());
            Assert.Equal("capture.frame=12", timedNetworkEvents[0].GetProperty("event").GetString());
            Assert.Equal(12L, timedNetworkEvents[1].GetProperty("tick").GetInt64());
            Assert.Equal("net.profile=net/multiplayer-profile.log", timedNetworkEvents[1].GetProperty("event").GetString());

            using JsonDocument multiplayerJson = JsonDocument.Parse(File.ReadAllText(multiplayerPath));
            JsonElement multiplayerRoot = multiplayerJson.RootElement;
            Assert.Equal(9001UL, multiplayerRoot.GetProperty("seed").GetUInt64());
            Assert.Equal(2, multiplayerRoot.GetProperty("connectedClients").GetInt32());
            Assert.True(multiplayerRoot.GetProperty("serverEntityCount").GetInt32() > 0);
            Assert.True(multiplayerRoot.GetProperty("synchronized").GetBoolean());
            Assert.Equal(2, multiplayerRoot.GetProperty("clientStats").GetArrayLength());
            JsonElement ownershipStats = multiplayerRoot.GetProperty("ownershipStats");
            Assert.Equal(2, ownershipStats.GetArrayLength());
            int ownedTotal = 0;
            foreach (JsonElement ownership in ownershipStats.EnumerateArray())
            {
                int ownedCount = ownership.GetProperty("ownedEntityCount").GetInt32();
                Assert.True(ownedCount >= 0);
                ownedTotal += ownedCount;
            }

            Assert.Equal(multiplayerRoot.GetProperty("serverEntityCount").GetInt32(), ownedTotal);
            JsonElement serverStats = multiplayerRoot.GetProperty("serverStats");
            Assert.True(serverStats.GetProperty("messagesSent").GetInt32() > 0);
            Assert.True(serverStats.GetProperty("averageSendBandwidthKbps").GetDouble() > 0.0);
            Assert.True(serverStats.GetProperty("averageReceiveBandwidthKbps").GetDouble() >= 0.0);
            Assert.True(serverStats.GetProperty("peakSendBandwidthKbps").GetDouble() >= serverStats.GetProperty("averageSendBandwidthKbps").GetDouble());
            Assert.True(serverStats.GetProperty("peakReceiveBandwidthKbps").GetDouble() >= serverStats.GetProperty("averageReceiveBandwidthKbps").GetDouble());

            JsonElement runtimeTransport = multiplayerRoot.GetProperty("runtimeTransport");
            bool runtimeTransportEnabled = runtimeTransport.GetProperty("enabled").GetBoolean();
            bool runtimeTransportSucceeded = runtimeTransport.GetProperty("succeeded").GetBoolean();
            int runtimeServerMessages = runtimeTransport.GetProperty("serverMessagesReceived").GetInt32();
            int runtimeClientMessages = runtimeTransport.GetProperty("clientMessagesReceived").GetInt32();
            Assert.True(runtimeServerMessages >= 0);
            Assert.True(runtimeClientMessages >= 0);
            if (runtimeTransportEnabled)
            {
                Assert.True(runtimeTransportSucceeded);
                Assert.True(runtimeServerMessages > 0);
                Assert.True(runtimeClientMessages > 0);
            }
            else
            {
                Assert.False(runtimeTransportSucceeded);
                Assert.Equal(0, runtimeServerMessages);
                Assert.Equal(0, runtimeClientMessages);
            }

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
            byte[] snapshotBinary = File.ReadAllBytes(snapshotBinaryPath);
            Assert.True(snapshotBinary.Length > 0);
            using JsonDocument renderStatsJson = JsonDocument.Parse(File.ReadAllText(renderStatsPath));
            JsonElement renderStatsRoot = renderStatsJson.RootElement;
            Assert.True(renderStatsRoot.GetProperty("drawItemCount").GetInt32() > 0);
            Assert.True(renderStatsRoot.GetProperty("triangleCount").GetUInt64() > 0);
            Assert.True(renderStatsRoot.GetProperty("uploadBytes").GetUInt64() > 0);
            Assert.True(renderStatsRoot.GetProperty("gpuMemoryBytes").GetUInt64() > 0);
            using JsonDocument hostConfigJson = JsonDocument.Parse(File.ReadAllText(hostConfigPath));
            Assert.Equal("hidden-window", hostConfigJson.RootElement.GetProperty("mode").GetString());
            Assert.Equal(0.02, hostConfigJson.RootElement.GetProperty("fixedDeltaSeconds").GetDouble(), 6);
            using JsonDocument runtimePerfJson = JsonDocument.Parse(File.ReadAllText(runtimePerfPath));
            JsonElement runtimePerfRoot = runtimePerfJson.RootElement;
            string? runtimeBackend = runtimePerfRoot.GetProperty("backend").GetString();
            Assert.True(
                string.Equals(runtimeBackend, "native", StringComparison.Ordinal) ||
                string.Equals(runtimeBackend, "noop", StringComparison.Ordinal));
            int captureSampleCount = runtimePerfRoot.GetProperty("captureSampleCount").GetInt32();
            Assert.True(captureSampleCount >= 5);
            Assert.True(runtimePerfRoot.GetProperty("averageCaptureCpuMs").GetDouble() >= 0d);
            Assert.True(runtimePerfRoot.GetProperty("peakCaptureCpuMs").GetDouble() >= runtimePerfRoot.GetProperty("averageCaptureCpuMs").GetDouble());
            Assert.True(runtimePerfRoot.GetProperty("totalCaptureAllocatedBytes").GetInt64() >= 0L);
            Assert.True(runtimePerfRoot.GetProperty("peakCaptureAllocatedBytes").GetInt64() >= runtimePerfRoot.GetProperty("averageCaptureAllocatedBytes").GetInt64());
            Assert.Equal(3, runtimePerfRoot.GetProperty("releaseRendererInteropBudgetPerFrame").GetInt32());
            Assert.Equal(3, runtimePerfRoot.GetProperty("releasePhysicsInteropBudgetPerTick").GetInt32());

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
            bool hasSnapshotBinary = artifacts.EnumerateArray()
                .Any(static artifact => string.Equals(
                    artifact.GetProperty("kind").GetString(),
                    "multiplayer-snapshot-bin",
                    StringComparison.Ordinal));
            Assert.True(hasSnapshotBinary);
            bool hasRenderStatsLog = artifacts.EnumerateArray()
                .Any(static artifact => string.Equals(
                    artifact.GetProperty("kind").GetString(),
                    "render-stats-log",
                    StringComparison.Ordinal));
            Assert.True(hasRenderStatsLog);
            bool hasHostConfig = artifacts.EnumerateArray()
                .Any(static artifact => string.Equals(
                    artifact.GetProperty("kind").GetString(),
                    "test-host-config",
                    StringComparison.Ordinal));
            Assert.True(hasHostConfig);
            bool hasRuntimePerfMetrics = artifacts.EnumerateArray()
                .Any(static artifact => string.Equals(
                    artifact.GetProperty("kind").GetString(),
                    "runtime-perf-metrics",
                    StringComparison.Ordinal));
            Assert.True(hasRuntimePerfMetrics);
            bool hasScreenshotRgba16f = artifacts.EnumerateArray()
                .Any(static artifact => string.Equals(
                    artifact.GetProperty("kind").GetString(),
                    "screenshot-buffer-rgba16f",
                    StringComparison.Ordinal));
            Assert.True(hasScreenshotRgba16f);
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
