using Engine.Testing;

namespace Engine.Tests.Testing;

public sealed class ReplayRecordingCodecValidationTests
{
    [Fact]
    public void Write_ShouldFail_WhenFramesAreEmpty()
    {
        string root = CreateTempDirectory();
        try
        {
            string replayPath = Path.Combine(root, "replay-empty-frames.json");
            var replay = new ReplayRecording(
                Seed: 1,
                FixedDeltaSeconds: 1.0 / 60.0,
                Frames: []);

            InvalidDataException exception = Assert.Throws<InvalidDataException>(
                () => ReplayRecordingCodec.Write(replayPath, replay));

            Assert.Contains("at least one frame", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Write_ShouldFail_WhenFrameTicksAreNotStrictlyIncreasing()
    {
        string root = CreateTempDirectory();
        try
        {
            string replayPath = Path.Combine(root, "replay-non-monotonic-ticks.json");
            var replay = new ReplayRecording(
                Seed: 7,
                FixedDeltaSeconds: 1.0 / 60.0,
                Frames:
                [
                    new ReplayFrameInput(0, 0, 0f, 0f),
                    new ReplayFrameInput(0, 1, 1f, 1f)
                ]);

            InvalidDataException exception = Assert.Throws<InvalidDataException>(
                () => ReplayRecordingCodec.Write(replayPath, replay));

            Assert.Contains("strictly increasing ticks", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Write_ShouldFail_WhenFrameMouseCoordinatesAreNotFinite()
    {
        string root = CreateTempDirectory();
        try
        {
            string replayPath = Path.Combine(root, "replay-non-finite-mouse.json");
            var replay = new ReplayRecording(
                Seed: 10,
                FixedDeltaSeconds: 1.0 / 60.0,
                Frames:
                [
                    new ReplayFrameInput(0, 0, float.NaN, 0f)
                ]);

            InvalidDataException exception = Assert.Throws<InvalidDataException>(
                () => ReplayRecordingCodec.Write(replayPath, replay));

            Assert.Contains("mouse coordinates", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Write_ShouldFail_WhenNetworkEventIsEmpty()
    {
        string root = CreateTempDirectory();
        try
        {
            string replayPath = Path.Combine(root, "replay-empty-network-event.json");
            var replay = new ReplayRecording(
                Seed: 3,
                FixedDeltaSeconds: 1.0 / 60.0,
                Frames:
                [
                    new ReplayFrameInput(0, 0, 0f, 0f)
                ],
                NetworkEvents:
                [
                    "spawn",
                    " "
                ]);

            InvalidDataException exception = Assert.Throws<InvalidDataException>(
                () => ReplayRecordingCodec.Write(replayPath, replay));

            Assert.Contains("network event", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Read_ShouldFail_WhenTimedNetworkEventsAreNotSorted()
    {
        string root = CreateTempDirectory();
        try
        {
            string replayPath = Path.Combine(root, "replay-unsorted-timed-events.json");
            File.WriteAllText(
                replayPath,
                """
                {
                  "seed": 11,
                  "fixedDeltaSeconds": 0.0166666667,
                  "frames": [
                    {
                      "tick": 0,
                      "buttonsMask": 0,
                      "mouseX": 0.0,
                      "mouseY": 0.0
                    },
                    {
                      "tick": 1,
                      "buttonsMask": 1,
                      "mouseX": 1.0,
                      "mouseY": 1.0
                    }
                  ],
                  "timedNetworkEvents": [
                    {
                      "tick": 2,
                      "event": "later"
                    },
                    {
                      "tick": 1,
                      "event": "earlier"
                    }
                  ]
                }
                """);

            InvalidDataException exception = Assert.Throws<InvalidDataException>(
                () => ReplayRecordingCodec.Read(replayPath));

            Assert.Contains("sorted", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"engine-testing-replay-validation-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
