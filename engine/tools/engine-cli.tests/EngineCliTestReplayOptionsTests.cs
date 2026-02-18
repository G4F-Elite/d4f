using System.Text.Json;
using Engine.Cli;
using Engine.Testing;

namespace Engine.Cli.Tests;

public sealed class EngineCliTestReplayOptionsTests
{
    [Fact]
    public void Run_TestCommand_ShouldUseReplayFile_WhenReplayOptionProvided()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            string replayPath = Path.Combine(tempRoot, "input", "recording.json");
            Directory.CreateDirectory(Path.GetDirectoryName(replayPath)!);
            ReplayRecordingCodec.Write(
                replayPath,
                new ReplayRecording(
                    Seed: 7777UL,
                    FixedDeltaSeconds: 0.02,
                    Frames:
                    [
                        new ReplayFrameInput(0, 0, 0f, 0f),
                        new ReplayFrameInput(1, 1, 1f, 0.5f),
                        new ReplayFrameInput(2, 1, 2f, 1.0f)
                    ],
                    NetworkEvents:
                    [
                        "custom:start"
                    ],
                    TimedNetworkEvents:
                    [
                        new ReplayTimedNetworkEvent(1, "custom:tick1")
                    ]));

            var runner = new RecordingCommandRunner();
            using var output = new StringWriter();
            using var error = new StringWriter();
            EngineCliApp app = new(output, error, runner);

            int code = app.Run(
            [
                "test",
                "--project", tempRoot,
                "--out", "artifacts/tests",
                "--capture-frame", "2",
                "--replay", "input/recording.json"
            ]);

            Assert.Equal(0, code);
            Assert.Single(runner.Invocations);
            Assert.Contains("Replay loaded:", output.ToString(), StringComparison.Ordinal);

            string replayArtifactPath = Path.Combine(tempRoot, "artifacts", "tests", "replay", "recording.json");
            using JsonDocument replayJson = JsonDocument.Parse(File.ReadAllText(replayArtifactPath));
            JsonElement root = replayJson.RootElement;

            Assert.Equal(7777UL, root.GetProperty("seed").GetUInt64());
            Assert.Equal(0.02, root.GetProperty("fixedDeltaSeconds").GetDouble(), 6);
            Assert.Equal(3, root.GetProperty("frames").GetArrayLength());

            JsonElement networkEvents = root.GetProperty("networkEvents");
            Assert.Contains(
                networkEvents.EnumerateArray().Select(static x => x.GetString()),
                static x => string.Equals(x, "custom:start", StringComparison.Ordinal));
            Assert.Contains(
                networkEvents.EnumerateArray().Select(static x => x.GetString()),
                static x => string.Equals(x, "capture.frame=2", StringComparison.Ordinal));
            Assert.Contains(
                networkEvents.EnumerateArray().Select(static x => x.GetString()),
                static x => string.Equals(x, "net.profile=net/multiplayer-profile.log", StringComparison.Ordinal));

            JsonElement timedEvents = root.GetProperty("timedNetworkEvents");
            Assert.Contains(
                timedEvents.EnumerateArray().Select(static x => (x.GetProperty("tick").GetInt64(), x.GetProperty("event").GetString())),
                static x => x == (1L, "custom:tick1"));
            Assert.Contains(
                timedEvents.EnumerateArray().Select(static x => (x.GetProperty("tick").GetInt64(), x.GetProperty("event").GetString())),
                static x => x == (0L, "capture.frame=2"));
            Assert.Contains(
                timedEvents.EnumerateArray().Select(static x => (x.GetProperty("tick").GetInt64(), x.GetProperty("event").GetString())),
                static x => x == (2L, "net.profile=net/multiplayer-profile.log"));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_TestCommand_ShouldFail_WhenReplayDoesNotContainCaptureTick()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            string replayPath = Path.Combine(tempRoot, "input", "recording.json");
            Directory.CreateDirectory(Path.GetDirectoryName(replayPath)!);
            ReplayRecordingCodec.Write(
                replayPath,
                new ReplayRecording(
                    Seed: 42UL,
                    FixedDeltaSeconds: 1.0 / 60.0,
                    Frames:
                    [
                        new ReplayFrameInput(0, 0, 0f, 0f),
                        new ReplayFrameInput(1, 1, 1f, 1f)
                    ]));

            var runner = new RecordingCommandRunner();
            using var output = new StringWriter();
            using var error = new StringWriter();
            EngineCliApp app = new(output, error, runner);

            int code = app.Run(
            [
                "test",
                "--project", tempRoot,
                "--out", "artifacts/tests",
                "--capture-frame", "10",
                "--replay", "input/recording.json"
            ]);

            Assert.Equal(1, code);
            Assert.Contains("Replay does not contain tick '10'", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"engine-cli-test-replay-options-{Guid.NewGuid():N}");
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
