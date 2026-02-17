using System.Globalization;
using System.Text.Json;
using Engine.AssetPipeline;
using Engine.Testing;

namespace Engine.Cli;

internal static class PreviewArtifactGenerator
{
    private const int PreviewWidth = 96;
    private const int PreviewHeight = 96;
    private const int WaveformWidth = 256;
    private const int WaveformHeight = 64;

    public static string Generate(string outputDirectory, IReadOnlyList<PakEntry> compiledEntries)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(compiledEntries);

        Directory.CreateDirectory(outputDirectory);
        var manifestEntries = new List<TestingArtifactEntry>(compiledEntries.Count * 3);

        foreach (PakEntry entry in compiledEntries)
        {
            string category = ResolvePreviewCategory(entry.Kind);
            string previewRelativePath = BuildPreviewRelativePath(category, entry.Path);
            string previewFullPath = Path.Combine(outputDirectory, previewRelativePath);
            GoldenImageBuffer previewImage = BuildAssetPreviewImage(entry);
            ArtifactOutputWriter.WriteRgbaPng(previewFullPath, previewImage);
            manifestEntries.Add(
                new TestingArtifactEntry(
                    Kind: $"{entry.Kind}-preview",
                    RelativePath: NormalizePath(previewRelativePath),
                    Description: $"Preview for '{entry.Path}' ({entry.Kind})."));

            if (!IsAudioKind(entry.Kind))
            {
                continue;
            }

            float[] waveformSamples = BuildWaveformSamples(entry.Path);
            string waveformImageRelativePath = BuildWaveformImageRelativePath(entry.Path);
            string waveformImageFullPath = Path.Combine(outputDirectory, waveformImageRelativePath);
            ArtifactOutputWriter.WriteRgbaPng(
                waveformImageFullPath,
                BuildWaveformImage(waveformSamples));
            manifestEntries.Add(
                new TestingArtifactEntry(
                    Kind: "audio-waveform-preview",
                    RelativePath: NormalizePath(waveformImageRelativePath),
                    Description: $"Waveform preview image for '{entry.Path}'."));

            string waveformMetadataRelativePath = BuildWaveformMetadataRelativePath(entry.Path);
            string waveformMetadataFullPath = Path.Combine(outputDirectory, waveformMetadataRelativePath);
            WriteWaveformMetadata(waveformMetadataFullPath, entry, waveformSamples);
            manifestEntries.Add(
                new TestingArtifactEntry(
                    Kind: "audio-waveform",
                    RelativePath: NormalizePath(waveformMetadataRelativePath),
                    Description: $"Waveform preview metadata for '{entry.Path}'."));
        }

        return ArtifactOutputWriter.WriteManifest(outputDirectory, manifestEntries);
    }

    private static GoldenImageBuffer BuildAssetPreviewImage(PakEntry entry)
    {
        uint seed = ComputeSeed($"{entry.Kind}|{entry.Path}");
        string kind = entry.Kind.Trim().ToLowerInvariant();
        return kind switch
        {
            "mesh" => BuildMeshPreview(seed),
            "texture" => BuildTexturePreview(seed),
            "material" => BuildMaterialPreview(seed),
            _ => BuildGenericPreview(seed)
        };
    }

    private static GoldenImageBuffer BuildMeshPreview(uint seed)
    {
        return BuildImage(PreviewWidth, PreviewHeight, (x, y) =>
        {
            float nx = (x - (PreviewWidth / 2f)) / (PreviewWidth / 2f);
            float ny = (y - (PreviewHeight / 2f)) / (PreviewHeight / 2f);
            float radial = MathF.Sqrt(nx * nx + ny * ny);
            float shape = MathF.Max(0f, 1f - radial);
            float light = MathF.Max(0.2f, 0.8f - ny * 0.5f);
            byte r = ToByte((int)(40 + shape * light * 160 + (seed & 0x1Fu)));
            byte g = ToByte((int)(50 + shape * light * 180 + ((seed >> 4) & 0x1Fu)));
            byte b = ToByte((int)(70 + shape * light * 200 + ((seed >> 8) & 0x1Fu)));
            return (r, g, b, 255);
        });
    }

    private static GoldenImageBuffer BuildTexturePreview(uint seed)
    {
        int tile = 8 + (int)(seed % 8u);
        return BuildImage(PreviewWidth, PreviewHeight, (x, y) =>
        {
            bool checker = ((x / tile) + (y / tile)) % 2 == 0;
            byte baseValue = checker ? (byte)220 : (byte)90;
            byte noise = (byte)((Hash(seed, (uint)x, (uint)y) >> 3) & 0x1F);
            byte r = ToByte(baseValue + noise);
            byte g = ToByte(baseValue - noise / 2);
            byte b = ToByte(60 + (noise * 3));
            return (r, g, b, 255);
        });
    }

    private static GoldenImageBuffer BuildMaterialPreview(uint seed)
    {
        int stripe = 6 + (int)((seed >> 3) % 10u);
        return BuildImage(PreviewWidth, PreviewHeight, (x, y) =>
        {
            bool alt = (x / stripe) % 2 == 0;
            int roughBand = (y * 255) / (PreviewHeight - 1);
            byte r = ToByte((alt ? 180 : 80) + roughBand / 8);
            byte g = ToByte((alt ? 120 : 60) + roughBand / 6);
            byte b = ToByte((alt ? 70 : 30) + roughBand / 5);
            return (r, g, b, 255);
        });
    }

    private static GoldenImageBuffer BuildGenericPreview(uint seed)
    {
        return BuildImage(PreviewWidth, PreviewHeight, (x, y) =>
        {
            byte r = ToByte((x * 255) / (PreviewWidth - 1) + (int)(seed & 0x0F));
            byte g = ToByte((y * 255) / (PreviewHeight - 1) + (int)((seed >> 4) & 0x0F));
            byte b = ToByte(((x + y) * 255) / (PreviewWidth + PreviewHeight - 2));
            return (r, g, b, 255);
        });
    }

    private static float[] BuildWaveformSamples(string sourceAssetPath)
    {
        uint seed = ComputeSeed($"audio:{sourceAssetPath}");
        float phaseA = ((seed & 0xFFu) / 255f) * MathF.PI;
        float phaseB = (((seed >> 8) & 0xFFu) / 255f) * MathF.PI;
        float freqA = 2.0f + ((seed >> 16) & 0x7u);
        float freqB = 5.0f + ((seed >> 20) & 0x7u);
        var samples = new float[WaveformWidth];
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i / (float)(samples.Length - 1);
            float a = MathF.Sin((t * freqA * MathF.PI * 2f) + phaseA) * 0.65f;
            float b = MathF.Sin((t * freqB * MathF.PI * 2f) + phaseB) * 0.35f;
            float n = (((Hash(seed, (uint)i, 17u) & 0xFFu) / 255f) - 0.5f) * 0.12f;
            samples[i] = Math.Clamp(a + b + n, -1f, 1f);
        }

        return samples;
    }

    private static GoldenImageBuffer BuildWaveformImage(IReadOnlyList<float> samples)
    {
        var rgba = new byte[WaveformWidth * WaveformHeight * 4];
        FillSolid(rgba, WaveformWidth, WaveformHeight, 12, 18, 26, 255);
        int midY = WaveformHeight / 2;

        for (int x = 0; x < WaveformWidth; x++)
        {
            SetPixel(rgba, WaveformWidth, WaveformHeight, x, midY, 48, 60, 74, 255);
        }

        for (int x = 0; x < WaveformWidth; x++)
        {
            int sampleIndex = Math.Min(samples.Count - 1, x);
            float value = samples[sampleIndex];
            int y = Math.Clamp(
                midY - (int)MathF.Round(value * ((WaveformHeight / 2f) - 2f)),
                0,
                WaveformHeight - 1);
            int minY = Math.Min(midY, y);
            int maxY = Math.Max(midY, y);
            for (int lineY = minY; lineY <= maxY; lineY++)
            {
                SetPixel(rgba, WaveformWidth, WaveformHeight, x, lineY, 64, 220, 210, 255);
            }
        }

        return new GoldenImageBuffer(WaveformWidth, WaveformHeight, rgba);
    }

    private static GoldenImageBuffer BuildImage(
        int width,
        int height,
        Func<int, int, (byte R, byte G, byte B, byte A)> pixel)
    {
        var rgba = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                (byte r, byte g, byte b, byte a) = pixel(x, y);
                int index = ((y * width) + x) * 4;
                rgba[index] = r;
                rgba[index + 1] = g;
                rgba[index + 2] = b;
                rgba[index + 3] = a;
            }
        }

        return new GoldenImageBuffer(width, height, rgba);
    }

    private static void WriteWaveformMetadata(
        string path,
        PakEntry entry,
        IReadOnlyList<float> samples)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        int sampleRate = 48_000;
        double duration = samples.Count / (double)sampleRate;
        string json = JsonSerializer.Serialize(
            new
            {
                source = entry.Path,
                kind = entry.Kind,
                sampleRate,
                durationSeconds = duration.ToString("F6", CultureInfo.InvariantCulture),
                samples = samples
            },
            ArtifactOutputWriter.SerializerOptions);
        File.WriteAllText(path, json);
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
        string safeName = BuildSafeAssetToken(NormalizePath(sourceAssetPath));
        return Path.Combine(category, $"{safeName}.png");
    }

    private static string BuildWaveformImageRelativePath(string sourceAssetPath)
    {
        string safeName = BuildSafeAssetToken(NormalizePath(sourceAssetPath));
        return Path.Combine("audio", $"{safeName}.waveform.png");
    }

    private static string BuildWaveformMetadataRelativePath(string sourceAssetPath)
    {
        string safeName = BuildSafeAssetToken(NormalizePath(sourceAssetPath));
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

    private static uint ComputeSeed(string value)
    {
        uint hash = 2166136261u;
        foreach (char ch in value)
        {
            hash ^= ch;
            hash *= 16777619u;
        }

        return hash;
    }

    private static uint Hash(uint seed, uint x, uint y)
    {
        uint value = seed ^ (x * 374761393u) ^ (y * 668265263u);
        value ^= value >> 13;
        value *= 1274126177u;
        value ^= value >> 16;
        return value;
    }

    private static byte ToByte(int value)
    {
        if (value < 0)
        {
            return 0;
        }

        return value > 255 ? (byte)255 : (byte)value;
    }

    private static void FillSolid(
        byte[] rgba,
        int width,
        int height,
        byte r,
        byte g,
        byte b,
        byte a)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                SetPixel(rgba, width, height, x, y, r, g, b, a);
            }
        }
    }

    private static void SetPixel(
        byte[] rgba,
        int width,
        int height,
        int x,
        int y,
        byte r,
        byte g,
        byte b,
        byte a)
    {
        if ((uint)x >= (uint)width || (uint)y >= (uint)height)
        {
            return;
        }

        int index = ((y * width) + x) * 4;
        rgba[index] = r;
        rgba[index + 1] = g;
        rgba[index + 2] = b;
        rgba[index + 3] = a;
    }
}
