using System.Globalization;
using System.Text.Json;
using Engine.AssetPipeline;
using Engine.Core.Handles;
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

internal sealed record TestCaptureArtifact(string RelativeCapturePath, string RelativeBufferPath);

internal sealed record TestArtifactsOutput(string ManifestPath, IReadOnlyList<TestCaptureArtifact> Captures);
internal sealed record RenderStatsArtifact(
    uint DrawItemCount,
    uint UiItemCount,
    ulong TriangleCount,
    ulong UploadBytes,
    ulong GpuMemoryBytes,
    ulong PresentCount);

internal sealed record TestArtifactGenerationOptions(
    int CaptureFrame,
    ulong ReplaySeed,
    double FixedDeltaSeconds,
    TestHostMode HostMode)
{
    public static TestArtifactGenerationOptions Default { get; } = new(1, 1337UL, 1.0 / 60.0, TestHostMode.HeadlessOffscreen);

    public TestArtifactGenerationOptions Validate()
    {
        if (CaptureFrame <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(CaptureFrame), "Capture frame must be greater than zero.");
        }

        if (FixedDeltaSeconds <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(FixedDeltaSeconds), "Fixed delta must be greater than zero.");
        }

        if (!Enum.IsDefined(HostMode))
        {
            throw new InvalidDataException($"Unsupported test host mode: {HostMode}.");
        }

        return this;
    }
}

internal static class TestArtifactGenerator
{
    private sealed record CaptureDefinition(string Kind, string RelativeCapturePath, byte PatternSeed);

    private const uint CaptureWidth = 64u;
    private const uint CaptureHeight = 64u;

    public static TestArtifactsOutput Generate(string outputDirectory)
    {
        return Generate(outputDirectory, TestArtifactGenerationOptions.Default);
    }

    public static TestArtifactsOutput Generate(
        string outputDirectory,
        TestArtifactGenerationOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(options);
        _ = options.Validate();

        CaptureDefinition[] captureDefinitions = BuildCaptureDefinitions(options.CaptureFrame);
        var manifestEntries = new List<TestingArtifactEntry>(captureDefinitions.Length * 2 + 1);
        var captures = new List<TestCaptureArtifact>(captureDefinitions.Length);
        IRenderingFacade captureFacade = NoopRenderingFacade.Instance;

        foreach (CaptureDefinition definition in captureDefinitions)
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

        MultiplayerDemoArtifactOutput multiplayerArtifacts = MultiplayerDemoArtifactGenerator.Generate(
            outputDirectory,
            options.ReplaySeed,
            options.FixedDeltaSeconds);
        string hostConfigRelativePath = Path.Combine("runtime", "test-host.json");
        string hostConfigFullPath = Path.Combine(outputDirectory, hostConfigRelativePath);
        WriteTestHostConfig(hostConfigFullPath, options.HostMode, options.FixedDeltaSeconds);
        string renderStatsRelativePath = Path.Combine("render", "frame-stats.json");
        string renderStatsFullPath = Path.Combine(outputDirectory, renderStatsRelativePath);
        RenderStatsArtifact renderStats = CaptureRenderStats(captureFacade);
        WriteRenderStats(renderStatsFullPath, renderStats);

        string replayRelativePath = Path.Combine("replay", "recording.json");
        string replayFullPath = Path.Combine(outputDirectory, replayRelativePath);
        ReplayRecordingCodec.Write(
            replayFullPath,
            new ReplayRecording(
                Seed: options.ReplaySeed,
                FixedDeltaSeconds: options.FixedDeltaSeconds,
                Frames: BuildReplayFrames(options.CaptureFrame),
                NetworkEvents:
                [
                    $"capture.frame={options.CaptureFrame}",
                    $"net.profile={multiplayerArtifacts.ProfileLogRelativePath}"
                ]));
        manifestEntries.Add(
            new TestingArtifactEntry(
                Kind: "replay",
                RelativePath: NormalizePath(replayRelativePath),
                Description: "Record/replay metadata."));
        manifestEntries.Add(
            new TestingArtifactEntry(
                Kind: "test-host-config",
                RelativePath: NormalizePath(hostConfigRelativePath),
                Description: "Test execution mode metadata (headless/offscreen or hidden window)."));

        manifestEntries.Add(
            new TestingArtifactEntry(
                Kind: "multiplayer-demo",
                RelativePath: multiplayerArtifacts.SummaryRelativePath,
                Description: "Authoritative server + 2 clients procedural replication summary with network metrics."));
        manifestEntries.Add(
            new TestingArtifactEntry(
                Kind: "net-profile-log",
                RelativePath: multiplayerArtifacts.ProfileLogRelativePath,
                Description: "Network profiling log with RTT/loss/bandwidth counters for server and clients."));
        manifestEntries.Add(
            new TestingArtifactEntry(
                Kind: "render-stats-log",
                RelativePath: NormalizePath(renderStatsRelativePath),
                Description: "Render counters log with draw calls, triangles, upload bytes and GPU memory estimate."));

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

    private static CaptureDefinition[] BuildCaptureDefinitions(int captureFrame)
    {
        string frameToken = captureFrame.ToString("D4", CultureInfo.InvariantCulture);
        return
        [
            new("screenshot", Path.Combine("screenshots", $"frame-{frameToken}.png"), 11),
            new("albedo", Path.Combine("dumps", $"albedo-{frameToken}.png"), 37),
            new("normals", Path.Combine("dumps", $"normals-{frameToken}.png"), 73),
            new("depth", Path.Combine("dumps", $"depth-{frameToken}.png"), 101),
            new("shadow", Path.Combine("dumps", $"shadow-{frameToken}.png"), 151)
        ];
    }

    private static ReplayFrameInput[] BuildReplayFrames(int captureFrame)
    {
        var frames = new ReplayFrameInput[captureFrame + 1];
        for (int tick = 0; tick <= captureFrame; tick++)
        {
            uint buttons = tick == 0 ? 0u : 1u;
            float mouseX = tick * 0.2f;
            float mouseY = tick * 0.1f;
            frames[tick] = new ReplayFrameInput(tick, buttons, mouseX, mouseY);
        }

        return frames;
    }

    private static RenderStatsArtifact CaptureRenderStats(IRenderingFacade renderingFacade)
    {
        if (renderingFacade is not NoopRenderingFacade)
        {
            return new RenderStatsArtifact(0u, 0u, 0u, 0u, 0u, 0u);
        }

        MeshHandle mesh = default;
        TextureHandle texture = default;
        MaterialHandle material = default;

        try
        {
            mesh = renderingFacade.CreateMeshFromCpu(
                positions:
                [
                    0f, 0f, 0f,
                    1f, 0f, 0f,
                    0f, 1f, 0f,
                    1f, 1f, 0f
                ],
                indices: [0u, 1u, 2u, 2u, 1u, 3u]);
            texture = renderingFacade.CreateTextureFromCpu(1u, 1u, [255, 255, 255, 255]);
            material = renderingFacade.CreateMaterialFromBlob([1]);

            using (renderingFacade.BeginFrame(2048, 64))
            {
                var packet = new RenderPacket(
                    frameNumber: 0,
                    drawCommands:
                    [
                        new DrawCommand(
                            new EntityId(index: 1, generation: 1u),
                            mesh,
                            material,
                            texture)
                    ]);
                renderingFacade.Submit(packet);
            }

            renderingFacade.Present();
            RenderingFrameStats stats = renderingFacade.GetLastFrameStats();
            return new RenderStatsArtifact(
                stats.DrawItemCount,
                stats.UiItemCount,
                stats.TriangleCount,
                stats.UploadBytes,
                stats.GpuMemoryBytes,
                stats.PresentCount);
        }
        finally
        {
            if (mesh.IsValid)
            {
                renderingFacade.DestroyResource(mesh.Value);
            }

            if (texture.IsValid)
            {
                renderingFacade.DestroyResource(texture.Value);
            }

            if (material.IsValid)
            {
                renderingFacade.DestroyResource(material.Value);
            }

            using (renderingFacade.BeginFrame(1024, 64))
            {
                renderingFacade.Submit(RenderPacket.Empty(1));
            }

            renderingFacade.Present();
        }
    }

    private static void WriteRenderStats(string outputPath, RenderStatsArtifact stats)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(stats);

        string? directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonSerializer.Serialize(stats, ArtifactOutputWriter.SerializerOptions);
        File.WriteAllText(outputPath, json);
    }

    private static void WriteTestHostConfig(
        string outputPath,
        TestHostMode hostMode,
        double fixedDeltaSeconds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        if (!Enum.IsDefined(hostMode))
        {
            throw new InvalidDataException($"Unsupported test host mode: {hostMode}.");
        }

        string? directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string modeLabel = hostMode == TestHostMode.HeadlessOffscreen
            ? "headless-offscreen"
            : "hidden-window";
        string json = JsonSerializer.Serialize(
            new
            {
                mode = modeLabel,
                fixedDeltaSeconds
            },
            ArtifactOutputWriter.SerializerOptions);
        File.WriteAllText(outputPath, json);
    }
}
