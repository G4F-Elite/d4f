using System.Text.Json;

namespace Engine.Testing;

public sealed record TestingArtifactEntry(
    string Kind,
    string RelativePath,
    string Description);

public sealed record TestingArtifactManifest(
    DateTime GeneratedAtUtc,
    IReadOnlyList<TestingArtifactEntry> Artifacts);

public static class TestingArtifactManifestCodec
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static void Write(string outputPath, TestingArtifactManifest manifest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(manifest);

        string? directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonSerializer.Serialize(manifest, SerializerOptions);
        File.WriteAllText(outputPath, json);
    }

    public static TestingArtifactManifest Read(string inputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);

        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException($"Artifact manifest was not found: {inputPath}", inputPath);
        }

        TestingArtifactManifest? manifest = JsonSerializer.Deserialize<TestingArtifactManifest>(
            File.ReadAllText(inputPath),
            SerializerOptions);
        if (manifest is null)
        {
            throw new InvalidDataException($"Artifact manifest '{inputPath}' is empty or invalid.");
        }

        if (manifest.Artifacts is null)
        {
            throw new InvalidDataException("Artifact manifest must contain artifacts list.");
        }

        return manifest;
    }
}
