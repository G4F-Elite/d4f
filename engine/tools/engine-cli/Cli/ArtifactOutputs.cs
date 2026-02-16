using System.Text.Json;
using Engine.AssetPipeline;

namespace Engine.Cli;

internal sealed record ArtifactManifest(DateTime GeneratedAtUtc, IReadOnlyList<ArtifactManifestEntry> Artifacts);

internal sealed record ArtifactManifestEntry(
    string Kind,
    string Path,
    string? SourceAssetPath,
    string Description);

internal static class ArtifactOutputWriter
{
    internal static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly byte[] PlaceholderPngBytes = Convert.FromHexString(
        "89504E470D0A1A0A0000000D49484452000000010000000108060000001F15C4890000000A49444154789C6360000000020001E221BC330000000049454E44AE426082");

    public static string WriteManifest(string outputDirectory, IReadOnlyList<ArtifactManifestEntry> entries)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(entries);

        Directory.CreateDirectory(outputDirectory);
        string manifestPath = Path.Combine(outputDirectory, "manifest.json");
        var manifest = new ArtifactManifest(DateTime.UtcNow, entries);
        string content = JsonSerializer.Serialize(manifest, SerializerOptions);
        File.WriteAllText(manifestPath, content);
        return manifestPath;
    }

    public static void WritePlaceholderPng(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string directory = Path.GetDirectoryName(path) ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(path, PlaceholderPngBytes);
    }
}

internal static class PreviewArtifactGenerator
{
    public static string Generate(string outputDirectory, IReadOnlyList<PakEntry> compiledEntries)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(compiledEntries);

        Directory.CreateDirectory(outputDirectory);
        var manifestEntries = new List<ArtifactManifestEntry>(compiledEntries.Count);

        foreach (PakEntry entry in compiledEntries)
        {
            string category = ResolvePreviewCategory(entry.Kind);
            string previewRelativePath = BuildPreviewRelativePath(category, entry.Path);
            string previewFullPath = Path.Combine(outputDirectory, previewRelativePath);
            ArtifactOutputWriter.WritePlaceholderPng(previewFullPath);
            manifestEntries.Add(
                new ArtifactManifestEntry(
                    Kind: $"{entry.Kind}-preview",
                    Path: NormalizePath(previewRelativePath),
                    SourceAssetPath: entry.Path,
                    Description: $"Preview for '{entry.Path}' ({entry.Kind})."));

            if (IsAudioKind(entry.Kind))
            {
                string waveformRelativePath = BuildWaveformRelativePath(entry.Path);
                string waveformFullPath = Path.Combine(outputDirectory, waveformRelativePath);
                string waveformDirectory = Path.GetDirectoryName(waveformFullPath) ?? outputDirectory;
                Directory.CreateDirectory(waveformDirectory);
                File.WriteAllText(
                    waveformFullPath,
                    JsonSerializer.Serialize(
                        new
                        {
                            source = entry.Path,
                            kind = entry.Kind,
                            samples = new[] { 0.0f, 0.25f, -0.2f, 0.1f, 0.0f }
                        },
                        ArtifactOutputWriter.SerializerOptions));
                manifestEntries.Add(
                    new ArtifactManifestEntry(
                        Kind: "audio-waveform",
                        Path: NormalizePath(waveformRelativePath),
                        SourceAssetPath: entry.Path,
                        Description: $"Waveform preview metadata for '{entry.Path}'."));
            }
        }

        return ArtifactOutputWriter.WriteManifest(outputDirectory, manifestEntries);
    }

    private static string ResolvePreviewCategory(string kind)
    {
        if (string.Equals(kind, "mesh", StringComparison.OrdinalIgnoreCase))
        {
            return "meshes";
        }

        if (string.Equals(kind, "texture", StringComparison.OrdinalIgnoreCase))
        {
            return "textures";
        }

        if (string.Equals(kind, "material", StringComparison.OrdinalIgnoreCase))
        {
            return "materials";
        }

        if (IsAudioKind(kind))
        {
            return "audio";
        }

        return "misc";
    }

    private static bool IsAudioKind(string kind)
    {
        return string.Equals(kind, "audio", StringComparison.OrdinalIgnoreCase)
            || string.Equals(kind, "sound", StringComparison.OrdinalIgnoreCase)
            || string.Equals(kind, "music", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildPreviewRelativePath(string category, string sourceAssetPath)
    {
        string normalizedAssetPath = NormalizePath(sourceAssetPath);
        string safeName = BuildSafeAssetToken(normalizedAssetPath);
        return Path.Combine(category, $"{safeName}.png");
    }

    private static string BuildWaveformRelativePath(string sourceAssetPath)
    {
        string normalizedAssetPath = NormalizePath(sourceAssetPath);
        string safeName = BuildSafeAssetToken(normalizedAssetPath);
        return Path.Combine("audio", $"{safeName}.waveform.json");
    }

    private static string BuildSafeAssetToken(string sourceAssetPath)
    {
        string withoutExtension = Path.ChangeExtension(sourceAssetPath, null) ?? sourceAssetPath;
        return withoutExtension
            .Replace('/', '_')
            .Replace('\\', '_')
            .Replace(' ', '_');
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }
}

internal static class TestArtifactGenerator
{
    public static string Generate(string outputDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        string[] capturePaths =
        [
            Path.Combine("screenshots", "frame-0001.png"),
            Path.Combine("dumps", "albedo-0001.png"),
            Path.Combine("dumps", "normals-0001.png"),
            Path.Combine("dumps", "depth-0001.png"),
            Path.Combine("dumps", "shadow-0001.png")
        ];

        var manifestEntries = new List<ArtifactManifestEntry>(capturePaths.Length + 1);
        foreach (string relativePath in capturePaths)
        {
            string fullPath = Path.Combine(outputDirectory, relativePath);
            ArtifactOutputWriter.WritePlaceholderPng(fullPath);
            manifestEntries.Add(
                new ArtifactManifestEntry(
                    Kind: "capture",
                    Path: relativePath.Replace('\\', '/'),
                    SourceAssetPath: null,
                    Description: "Deterministic placeholder capture."));
        }

        string replayPath = Path.Combine(outputDirectory, "replay", "recording.json");
        string replayDirectory = Path.GetDirectoryName(replayPath) ?? outputDirectory;
        Directory.CreateDirectory(replayDirectory);
        File.WriteAllText(
            replayPath,
            JsonSerializer.Serialize(
                new
                {
                    deterministic = true,
                    fixedDt = 0.016666667,
                    seed = 1337,
                    ticks = 60
                },
                ArtifactOutputWriter.SerializerOptions));
        manifestEntries.Add(
            new ArtifactManifestEntry(
                Kind: "replay",
                Path: "replay/recording.json",
                SourceAssetPath: null,
                Description: "Record/replay metadata."));

        return ArtifactOutputWriter.WriteManifest(outputDirectory, manifestEntries);
    }
}
