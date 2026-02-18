using System.Text.Json;

namespace Engine.Testing;

public sealed record ReplayFrameInput(
    long Tick,
    uint ButtonsMask,
    float MouseX,
    float MouseY);

public sealed record ReplayTimedNetworkEvent(
    long Tick,
    string Event);

public sealed record ReplayRecording(
    ulong Seed,
    double FixedDeltaSeconds,
    IReadOnlyList<ReplayFrameInput> Frames,
    IReadOnlyList<string>? NetworkEvents = null,
    IReadOnlyList<ReplayTimedNetworkEvent>? TimedNetworkEvents = null);

public static class ReplayRecordingCodec
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static void Write(string outputPath, ReplayRecording recording)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(recording);
        ValidateReplay(recording);

        string? directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonSerializer.Serialize(recording, SerializerOptions);
        File.WriteAllText(outputPath, json);
    }

    public static ReplayRecording Read(string inputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);

        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException($"Replay file was not found: {inputPath}", inputPath);
        }

        ReplayRecording? replay = JsonSerializer.Deserialize<ReplayRecording>(
            File.ReadAllText(inputPath),
            SerializerOptions);
        if (replay is null)
        {
            throw new InvalidDataException($"Replay file '{inputPath}' is empty or invalid.");
        }

        ValidateReplay(replay);
        return replay;
    }

    private static void ValidateReplay(ReplayRecording replay)
    {
        if (replay.FixedDeltaSeconds <= 0.0)
        {
            throw new InvalidDataException("Replay fixed delta seconds must be positive.");
        }

        if (replay.Frames is null)
        {
            throw new InvalidDataException("Replay must define frames.");
        }

        if (replay.Frames.Count == 0)
        {
            throw new InvalidDataException("Replay must contain at least one frame.");
        }

        long previousTick = -1;
        for (int i = 0; i < replay.Frames.Count; i++)
        {
            ReplayFrameInput frame = replay.Frames[i]
                ?? throw new InvalidDataException($"Replay frame at index {i} is null.");
            if (frame.Tick < 0)
            {
                throw new InvalidDataException($"Replay frame tick at index {i} must be non-negative.");
            }

            if (frame.Tick <= previousTick)
            {
                throw new InvalidDataException(
                    $"Replay frames must have strictly increasing ticks. Tick {frame.Tick} at index {i} is not greater than {previousTick}.");
            }

            if (!float.IsFinite(frame.MouseX) || !float.IsFinite(frame.MouseY))
            {
                throw new InvalidDataException($"Replay frame mouse coordinates at index {i} must be finite.");
            }

            previousTick = frame.Tick;
        }

        if (replay.NetworkEvents is not null)
        {
            for (int i = 0; i < replay.NetworkEvents.Count; i++)
            {
                string? evt = replay.NetworkEvents[i];
                if (string.IsNullOrWhiteSpace(evt))
                {
                    throw new InvalidDataException($"Replay network event at index {i} cannot be empty.");
                }
            }
        }

        if (replay.TimedNetworkEvents is not null)
        {
            long previousTimedTick = -1;
            for (int i = 0; i < replay.TimedNetworkEvents.Count; i++)
            {
                ReplayTimedNetworkEvent evt = replay.TimedNetworkEvents[i]
                    ?? throw new InvalidDataException($"Replay timed network event at index {i} is null.");
                if (evt.Tick < 0)
                {
                    throw new InvalidDataException($"Replay timed network event tick at index {i} must be non-negative.");
                }

                if (evt.Tick < previousTimedTick)
                {
                    throw new InvalidDataException(
                        $"Replay timed network events must be sorted by non-decreasing tick. Tick {evt.Tick} at index {i} is less than {previousTimedTick}.");
                }

                if (string.IsNullOrWhiteSpace(evt.Event))
                {
                    throw new InvalidDataException($"Replay timed network event payload at index {i} cannot be empty.");
                }

                previousTimedTick = evt.Tick;
            }
        }
    }
}
