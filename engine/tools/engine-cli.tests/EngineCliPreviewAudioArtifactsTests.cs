using System.Text.Json;
using Engine.Cli;

namespace Engine.Cli.Tests;

public sealed class EngineCliPreviewAudioArtifactsTests
{
    [Fact]
    public void Run_Preview_ShouldGenerateWaveformArtifacts_ForSoundAssets()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareSoundManifest(tempRoot);

            using var output = new StringWriter();
            using var error = new StringWriter();
            var app = new EngineCliApp(output, error);

            int code = app.Run(
            [
                "preview",
                "--project", tempRoot,
                "--manifest", "assets/manifest.json",
                "--out", "artifacts/preview"
            ]);

            Assert.Equal(0, code);

            string previewRoot = Path.Combine(tempRoot, "artifacts", "preview");
            string baseToken = "audio_ping";
            string soundPreviewPath = Path.Combine(previewRoot, "audio", $"{baseToken}.png");
            string waveformImagePath = Path.Combine(previewRoot, "audio", $"{baseToken}.waveform.png");
            string waveformMetadataPath = Path.Combine(previewRoot, "audio", $"{baseToken}.waveform.json");
            string compressedWaveformPath = Path.Combine(previewRoot, "audio", $"{baseToken}.waveform.pcm16.gz");

            Assert.True(File.Exists(soundPreviewPath));
            Assert.True(File.Exists(waveformImagePath));
            Assert.True(File.Exists(waveformMetadataPath));
            Assert.True(File.Exists(compressedWaveformPath));

            using JsonDocument waveformMetadata = JsonDocument.Parse(File.ReadAllText(waveformMetadataPath));
            JsonElement waveformRoot = waveformMetadata.RootElement;
            Assert.Equal("audio/ping.wav", waveformRoot.GetProperty("source").GetString());
            Assert.Equal("sound", waveformRoot.GetProperty("kind").GetString());
            Assert.Equal("audio/audio_ping.waveform.pcm16.gz", waveformRoot.GetProperty("compressedPreviewPath").GetString());

            string manifestPath = Path.Combine(previewRoot, "manifest.json");
            using JsonDocument manifestJson = JsonDocument.Parse(File.ReadAllText(manifestPath));
            JsonElement artifacts = manifestJson.RootElement.GetProperty("artifacts");

            Assert.Contains(artifacts.EnumerateArray(), static entry =>
                entry.GetProperty("kind").GetString() == "sound-preview");
            Assert.Contains(artifacts.EnumerateArray(), static entry =>
                entry.GetProperty("kind").GetString() == "audio-waveform-preview");
            Assert.Contains(artifacts.EnumerateArray(), static entry =>
                entry.GetProperty("kind").GetString() == "audio-waveform");
            Assert.Contains(artifacts.EnumerateArray(), static entry =>
                entry.GetProperty("kind").GetString() == "audio-compressed-preview");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static void PrepareSoundManifest(string rootPath)
    {
        string assetsDirectory = Path.Combine(rootPath, "assets");
        Directory.CreateDirectory(Path.Combine(assetsDirectory, "audio"));
        File.WriteAllBytes(Path.Combine(assetsDirectory, "audio", "ping.wav"), [0x52, 0x49, 0x46, 0x46, 0x57, 0x41, 0x56, 0x45]);
        File.WriteAllText(
            Path.Combine(assetsDirectory, "manifest.json"),
            """
            {
              "version": 1,
              "assets": [
                {
                  "path": "audio/ping.wav",
                  "kind": "sound"
                }
              ]
            }
            """);
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"engine-cli-preview-audio-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
