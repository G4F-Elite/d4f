using System.Text.Json;

namespace Engine.Testing;

public sealed record ReplayFrameInput(
    long Tick,
    uint ButtonsMask,
    float MouseX,
    float MouseY);

public sealed record ReplayRecording(
    ulong Seed,
    double FixedDeltaSeconds,
    IReadOnlyList<ReplayFrameInput> Frames,
    IReadOnlyList<string>? NetworkEvents = null);

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

        if (recording.FixedDeltaSeconds <= 0.0)
        {
            throw new InvalidDataException("Replay fixed delta seconds must be positive.");
        }

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

        if (replay.FixedDeltaSeconds <= 0.0)
        {
            throw new InvalidDataException("Replay fixed delta seconds must be positive.");
        }

        if (replay.Frames is null)
        {
            throw new InvalidDataException("Replay must define frames.");
        }

        return replay;
    }
}
