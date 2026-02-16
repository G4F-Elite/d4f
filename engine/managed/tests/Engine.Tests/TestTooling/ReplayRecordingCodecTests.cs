using Engine.Testing;

namespace Engine.Tests.Testing;

public sealed class ReplayRecordingCodecTests
{
    [Fact]
    public void WriteAndRead_ShouldRoundtripReplay()
    {
        string root = CreateTempDirectory();
        try
        {
            string replayPath = Path.Combine(root, "replay.json");
            var replay = new ReplayRecording(
                Seed: 1337,
                FixedDeltaSeconds: 1.0 / 60.0,
                Frames:
                [
                    new ReplayFrameInput(0, 1, 0.0f, 0.0f),
                    new ReplayFrameInput(1, 3, 5.0f, 6.0f)
                ],
                NetworkEvents:
                [
                    "spawn:player1",
                    "rpc:jump"
                ]);

            ReplayRecordingCodec.Write(replayPath, replay);
            ReplayRecording loaded = ReplayRecordingCodec.Read(replayPath);

            Assert.Equal(replay.Seed, loaded.Seed);
            Assert.Equal(replay.FixedDeltaSeconds, loaded.FixedDeltaSeconds);
            Assert.Equal(replay.Frames.Count, loaded.Frames.Count);
            Assert.Equal(replay.Frames[1], loaded.Frames[1]);
            Assert.Equal(replay.NetworkEvents, loaded.NetworkEvents);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Write_ShouldFail_WhenFixedDeltaInvalid()
    {
        string root = CreateTempDirectory();
        try
        {
            string replayPath = Path.Combine(root, "bad-replay.json");
            var replay = new ReplayRecording(
                Seed: 1,
                FixedDeltaSeconds: 0.0,
                Frames: []);

            Assert.Throws<InvalidDataException>(() => ReplayRecordingCodec.Write(replayPath, replay));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Read_ShouldFail_WhenFileMissing()
    {
        string path = Path.Combine(Path.GetTempPath(), $"missing-replay-{Guid.NewGuid():N}.json");
        Assert.Throws<FileNotFoundException>(() => ReplayRecordingCodec.Read(path));
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"engine-testing-replay-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
