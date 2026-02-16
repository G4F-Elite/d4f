using System.Text.Json;
using Engine.AssetPipeline;
using Engine.Rendering;
using Engine.Testing;

namespace Engine.Cli;

internal static class ArtifactOutputWriter
{
    internal static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly byte[] PlaceholderPngBytes = Convert.FromHexString(
        "89504E470D0A1A0A0000000D49484452000000010000000108060000001F15C4890000000A49444154789C6360000000020001E221BC330000000049454E44AE426082");

    public static void WritePlaceholderPng(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(path, PlaceholderPngBytes);
    }

    public static void WriteRgbaPng(string path, GoldenImageBuffer image)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        RgbaPngCodec.Write(path, image);
    }

    public static string WriteManifest(string outputDirectory, IReadOnlyList<TestingArtifactEntry> entries)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(entries);

        Directory.CreateDirectory(outputDirectory);
        string manifestPath = Path.Combine(outputDirectory, "manifest.json");
        var manifest = new TestingArtifactManifest(DateTime.UtcNow, entries);
        TestingArtifactManifestCodec.Write(manifestPath, manifest);
        return manifestPath;
    }
}

internal static class PreviewArtifactGenerator
{
    public static string Generate(string outputDirectory, IReadOnlyList<PakEntry> compiledEntries)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(compiledEntries);

        Directory.CreateDirectory(outputDirectory);
        var manifestEntries = new List<TestingArtifactEntry>(compiledEntries.Count);

        foreach (PakEntry entry in compiledEntries)
        {
            string category = ResolvePreviewCategory(entry.Kind);
            string previewRelativePath = BuildPreviewRelativePath(category, entry.Path);
            string previewFullPath = Path.Combine(outputDirectory, previewRelativePath);
            ArtifactOutputWriter.WritePlaceholderPng(previewFullPath);
            manifestEntries.Add(
                new TestingArtifactEntry(
                    Kind: $"{entry.Kind}-preview",
                    RelativePath: NormalizePath(previewRelativePath),
                    Description: $"Preview for '{entry.Path}' ({entry.Kind})."));

            if (IsAudioKind(entry.Kind))
            {
                string waveformRelativePath = BuildWaveformRelativePath(entry.Path);
                string waveformFullPath = Path.Combine(outputDirectory, waveformRelativePath);
                string? waveformDirectory = Path.GetDirectoryName(waveformFullPath);
                if (!string.IsNullOrWhiteSpace(waveformDirectory))
                {
                    Directory.CreateDirectory(waveformDirectory);
                }

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
                    new TestingArtifactEntry(
                        Kind: "audio-waveform",
                        RelativePath: NormalizePath(waveformRelativePath),
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

internal sealed record TestCaptureArtifact(string RelativeCapturePath, string RelativeBufferPath);

internal sealed record TestArtifactsOutput(string ManifestPath, IReadOnlyList<TestCaptureArtifact> Captures);

internal static class TestArtifactGenerator
{
    private sealed record CaptureDefinition(string Kind, string RelativeCapturePath, byte PatternSeed);

    private const uint CaptureWidth = 64u;
    private const uint CaptureHeight = 64u;

    private static readonly CaptureDefinition[] CaptureDefinitions =
    [
        new("screenshot", Path.Combine("screenshots", "frame-0001.png"), 11),
        new("albedo", Path.Combine("dumps", "albedo-0001.png"), 37),
        new("normals", Path.Combine("dumps", "normals-0001.png"), 73),
        new("depth", Path.Combine("dumps", "depth-0001.png"), 101),
        new("shadow", Path.Combine("dumps", "shadow-0001.png"), 151)
    ];

    public static TestArtifactsOutput Generate(string outputDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        var manifestEntries = new List<TestingArtifactEntry>(CaptureDefinitions.Length * 2 + 1);
        var captures = new List<TestCaptureArtifact>(CaptureDefinitions.Length);
        IRenderingFacade captureFacade = NoopRenderingFacade.Instance;

        foreach (CaptureDefinition definition in CaptureDefinitions)
        {
            GoldenImageBuffer image = CaptureBuffer(captureFacade, definition);
            string captureFullPath = Path.Combine(outputDirectory, definition.RelativeCapturePath);
            ArtifactOutputWriter.WriteRgbaPng(captureFullPath, image);
            manifestEntries.Add(
                new TestingArtifactEntry(
                    Kind: definition.Kind,
                    RelativePath: NormalizePath(definition.RelativeCapturePath),
                    Description: "Deterministic runtime capture."));

            string relativeBufferPath = Path.ChangeExtension(definition.RelativeCapturePath, ".rgba8.bin")
                ?? throw new InvalidDataException($"Unable to compute buffer path for '{definition.RelativeCapturePath}'.");
            string fullBufferPath = Path.Combine(outputDirectory, relativeBufferPath);
            GoldenImageBufferFileCodec.Write(fullBufferPath, image);
            manifestEntries.Add(
                new TestingArtifactEntry(
                    Kind: $"{definition.Kind}-buffer",
                    RelativePath: NormalizePath(relativeBufferPath),
                    Description: "Deterministic runtime raw capture buffer for golden comparison."));
            captures.Add(new TestCaptureArtifact(NormalizePath(definition.RelativeCapturePath), NormalizePath(relativeBufferPath)));
        }

        string replayRelativePath = Path.Combine("replay", "recording.json");
        string replayFullPath = Path.Combine(outputDirectory, replayRelativePath);
        ReplayRecordingCodec.Write(
            replayFullPath,
            new ReplayRecording(
                Seed: 1337,
                FixedDeltaSeconds: 1.0 / 60.0,
                Frames:
                [
                    new ReplayFrameInput(0, 0, 0.0f, 0.0f),
                    new ReplayFrameInput(1, 1, 0.2f, 0.1f),
                    new ReplayFrameInput(2, 1, 0.3f, 0.2f)
                ]));
        manifestEntries.Add(
            new TestingArtifactEntry(
                Kind: "replay",
                RelativePath: NormalizePath(replayRelativePath),
                Description: "Record/replay metadata."));

        string manifestPath = ArtifactOutputWriter.WriteManifest(outputDirectory, manifestEntries);
        return new TestArtifactsOutput(manifestPath, captures);
    }

    private static GoldenImageBuffer CaptureBuffer(IRenderingFacade captureFacade, CaptureDefinition definition)
    {
        byte[] rgba = captureFacade.CaptureFrameRgba8(CaptureWidth, CaptureHeight);
        byte[] transformed = new byte[rgba.Length];
        Buffer.BlockCopy(rgba, 0, transformed, 0, rgba.Length);

        // Keep captures deterministic but distinct per artifact kind.
        for (int i = 0; i < transformed.Length; i += 4)
        {
            transformed[i] ^= definition.PatternSeed;
            transformed[i + 1] = unchecked((byte)(transformed[i + 1] + definition.PatternSeed / 2));
        }

        return new GoldenImageBuffer(checked((int)CaptureWidth), checked((int)CaptureHeight), transformed);
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }
}
