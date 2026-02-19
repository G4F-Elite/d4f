using System.Text.Json;
using Engine.App;
using Engine.AssetPipeline;
using Engine.Net;
using Engine.Testing;

namespace Engine.Cli;

public sealed partial class EngineCliApp
{
    private int HandleNfrProof(NfrProofCommand command)
    {
        string projectDirectory = Path.GetFullPath(command.ProjectDirectory);
        if (!Directory.Exists(projectDirectory))
        {
            _stderr.WriteLine($"Project directory does not exist: {projectDirectory}");
            return 1;
        }

        string? runtimeProjectPath = ResolvePublishProjectPath(projectDirectory, configuredPath: null);
        string? buildTargetPath = ResolveDotnetBuildTarget(projectDirectory, runtimeProjectPath);
        if (buildTargetPath is null)
        {
            _stderr.WriteLine($"Runtime .csproj was not found under '{Path.Combine(projectDirectory, "src")}'.");
            _stderr.WriteLine("No solution or project file found in project root for NFR proof fallback.");
            return 1;
        }

        if (runtimeProjectPath is null)
        {
            _stdout.WriteLine($"Runtime .csproj not found under '{Path.Combine(projectDirectory, "src")}', using build target '{buildTargetPath}'.");
        }

        var failures = new List<string>();

        int buildExitCode = _commandRunner.Run(
            "dotnet",
            ["build", buildTargetPath, "-c", command.Configuration, "--nologo"],
            projectDirectory,
            _stdout,
            _stderr);
        bool buildSucceeded = buildExitCode == 0;
        if (!buildSucceeded)
        {
            failures.Add($"NFR proof build step failed with exit code {buildExitCode}.");
        }

        string testTargetPath = ResolveDotnetTestTarget(projectDirectory, runtimeProjectPath);

        int testExitCode = _commandRunner.Run(
            "dotnet",
            ["test", testTargetPath, "-c", command.Configuration, "--nologo"],
            projectDirectory,
            _stdout,
            _stderr);
        bool testsSucceeded = testExitCode == 0;
        if (!testsSucceeded)
        {
            failures.Add($"NFR proof test step failed with exit code {testExitCode}.");
        }

        NfrReleaseProofArtifactSummary artifactSummary = BuildNfrArtifactSummary(projectDirectory, failures);
        bool isSuccess = failures.Count == 0;

        string outputPath = AssetPipelineService.ResolveRelativePath(projectDirectory, command.OutputPath);
        string? outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        var document = new NfrReleaseProofDocument(
            GeneratedAtUtc: DateTime.UtcNow,
            Configuration: command.Configuration,
            RuntimeProjectPath: buildTargetPath,
            Build: new NfrReleaseProofStepResult(buildSucceeded, buildExitCode),
            Tests: new NfrReleaseProofStepResult(testsSucceeded, testExitCode),
            Checks: artifactSummary.Checks,
            RuntimePerfSummary: artifactSummary.RuntimePerfSummary,
            RenderStatsSummary: artifactSummary.RenderStatsSummary,
            RuntimeTransportSummary: artifactSummary.RuntimeTransportSummary,
            IsSuccess: isSuccess);

        string json = JsonSerializer.Serialize(document, ArtifactOutputWriter.SerializerOptions);
        File.WriteAllText(outputPath, json);
        _stdout.WriteLine($"NFR release proof written: {outputPath}");

        if (isSuccess)
        {
            _stdout.WriteLine("NFR release proof checks passed.");
            return 0;
        }

        foreach (string failure in failures)
        {
            _stderr.WriteLine(failure);
        }

        return 1;
    }

    private static NfrReleaseProofArtifactSummary BuildNfrArtifactSummary(string projectDirectory, List<string> failures)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectDirectory);
        ArgumentNullException.ThrowIfNull(failures);

        string runtimePerfPath = AssetPipelineService.ResolveRelativePath(projectDirectory, Path.Combine("artifacts", "tests", "runtime", "perf-metrics.json"));
        string renderStatsPath = AssetPipelineService.ResolveRelativePath(projectDirectory, Path.Combine("artifacts", "tests", "render", "frame-stats.json"));
        string multiplayerSummaryPath = AssetPipelineService.ResolveRelativePath(projectDirectory, Path.Combine("artifacts", "tests", "net", "multiplayer-demo.json"));
        string netProfilePath = AssetPipelineService.ResolveRelativePath(projectDirectory, Path.Combine("artifacts", "tests", "net", "multiplayer-profile.log"));
        string snapshotBinaryPath = AssetPipelineService.ResolveRelativePath(projectDirectory, Path.Combine("artifacts", "tests", "net", "multiplayer-snapshot.bin"));
        string rpcBinaryPath = AssetPipelineService.ResolveRelativePath(projectDirectory, Path.Combine("artifacts", "tests", "net", "multiplayer-rpc.bin"));
        string replayPath = AssetPipelineService.ResolveRelativePath(projectDirectory, Path.Combine("artifacts", "tests", "replay", "recording.json"));
        string manifestPath = AssetPipelineService.ResolveRelativePath(projectDirectory, Path.Combine("artifacts", "tests", "manifest.json"));

        RuntimePerfMetricsSummary? runtimePerfSummary = null;
        bool runtimePerfMetricsOk = false;
        bool releaseInteropBudgetsMatch = false;
        if (!File.Exists(runtimePerfPath))
        {
            failures.Add($"NFR proof missing artifact: {runtimePerfPath}");
        }
        else
        {
            try
            {
                RuntimePerfMetricsSummary metrics = ReadRuntimePerfMetricsSummary(runtimePerfPath);
                runtimePerfSummary = metrics;
                runtimePerfMetricsOk = true;
                InteropBudgetOptions releaseBudgets = InteropBudgetOptions.ReleaseStrict;
                releaseInteropBudgetsMatch = metrics.ReleaseRendererInteropBudgetPerFrame == releaseBudgets.MaxRendererCallsPerFrame &&
                    metrics.ReleasePhysicsInteropBudgetPerTick == releaseBudgets.MaxPhysicsCallsPerTick;
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException)
            {
                failures.Add($"NFR proof runtime perf metrics invalid: {ex.Message}");
            }
        }

        RenderStatsSummary? renderStatsSummary = null;
        bool renderStatsOk = false;
        if (!File.Exists(renderStatsPath))
        {
            failures.Add($"NFR proof missing artifact: {renderStatsPath}");
        }
        else
        {
            try
            {
                RenderStatsSummary renderStats = ReadRenderStatsSummary(renderStatsPath);
                renderStatsSummary = renderStats;
                renderStatsOk = true;
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or JsonException)
            {
                failures.Add($"NFR proof render stats invalid: {ex.Message}");
            }
        }

        MultiplayerRuntimeTransportSummary? runtimeTransportSummary = null;
        bool multiplayerSummaryOk = false;
        if (!File.Exists(multiplayerSummaryPath))
        {
            failures.Add($"NFR proof missing artifact: {multiplayerSummaryPath}");
        }
        else
        {
            try
            {
                MultiplayerRuntimeTransportSummary summary = ReadMultiplayerRuntimeTransportSummary(multiplayerSummaryPath);
                runtimeTransportSummary = summary;
                multiplayerSummaryOk = true;
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or JsonException)
            {
                failures.Add($"NFR proof multiplayer summary invalid: {ex.Message}");
            }
        }

        bool netProfileLogOk = false;
        if (!File.Exists(netProfilePath))
        {
            failures.Add($"NFR proof missing artifact: {netProfilePath}");
        }
        else
        {
            try
            {
                string[] lines = File.ReadAllLines(netProfilePath)
                    .Where(static line => !string.IsNullOrWhiteSpace(line))
                    .ToArray();
                netProfileLogOk = lines.Length > 0;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failures.Add($"NFR proof net profile log invalid: {ex.Message}");
            }
        }

        bool snapshotBinaryOk = false;
        if (!File.Exists(snapshotBinaryPath))
        {
            failures.Add($"NFR proof missing artifact: {snapshotBinaryPath}");
        }
        else
        {
            try
            {
                _ = NetSnapshotBinaryCodec.Decode(File.ReadAllBytes(snapshotBinaryPath));
                snapshotBinaryOk = true;
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException)
            {
                failures.Add($"NFR proof multiplayer snapshot invalid: {ex.Message}");
            }
        }

        bool rpcBinaryOk = false;
        if (!File.Exists(rpcBinaryPath))
        {
            failures.Add($"NFR proof missing artifact: {rpcBinaryPath}");
        }
        else
        {
            try
            {
                _ = NetRpcBinaryCodec.Decode(File.ReadAllBytes(rpcBinaryPath));
                rpcBinaryOk = true;
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException)
            {
                failures.Add($"NFR proof multiplayer RPC invalid: {ex.Message}");
            }
        }

        bool replayRecordingOk = false;
        if (!File.Exists(replayPath))
        {
            failures.Add($"NFR proof missing artifact: {replayPath}");
        }
        else
        {
            try
            {
                ReplayRecording replay = ReplayRecordingCodec.Read(replayPath);
                replayRecordingOk = replay.Frames.Count > 0;
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException)
            {
                failures.Add($"NFR proof replay recording invalid: {ex.Message}");
            }
        }

        bool artifactsManifestOk = false;
        if (!File.Exists(manifestPath))
        {
            failures.Add($"NFR proof missing artifact: {manifestPath}");
        }
        else
        {
            try
            {
                TestingArtifactManifest manifest = TestingArtifactManifestCodec.Read(manifestPath);
                if (manifest.Artifacts.Count == 0)
                {
                    failures.Add("NFR proof artifacts manifest invalid: manifest does not contain any artifacts.");
                }
                else
                {
                    string[] requiredKinds =
                    [
                        "screenshot",
                        "screenshot-buffer",
                        "screenshot-buffer-rgba16f",
                        "screenshot-buffer-rgba16f-exr",
                        "albedo",
                        "albedo-buffer",
                        "normals",
                        "normals-buffer",
                        "depth",
                        "depth-buffer",
                        "roughness",
                        "roughness-buffer",
                        "shadow",
                        "shadow-buffer",
                        "multiplayer-demo",
                        "net-profile-log",
                        "multiplayer-snapshot-bin",
                        "multiplayer-rpc-bin",
                        "render-stats-log",
                        "test-host-config",
                        "runtime-perf-metrics",
                        "replay"
                    ];
                    var entriesByKind = manifest.Artifacts
                        .GroupBy(static entry => entry.Kind, StringComparer.Ordinal)
                        .ToDictionary(static group => group.Key, static group => group.ToArray(), StringComparer.Ordinal);

                    string[] missingKinds = requiredKinds.Where(required => !entriesByKind.ContainsKey(required)).ToArray();
                    if (missingKinds.Length > 0)
                    {
                        failures.Add($"NFR proof artifacts manifest missing required kinds: [{string.Join(", ", missingKinds)}].");
                    }

                    string manifestDirectory = Path.GetDirectoryName(manifestPath) ?? projectDirectory;
                    var missingArtifactFiles = new List<string>();
                    var invalidArtifactFiles = new List<string>();
                    foreach (string requiredKind in requiredKinds)
                    {
                        if (!entriesByKind.TryGetValue(requiredKind, out TestingArtifactEntry[]? entriesForKind) || entriesForKind.Length == 0)
                        {
                            continue;
                        }

                        string relativePath = entriesForKind[0].RelativePath;
                        string resolvedArtifactPath = AssetPipelineService.ResolveRelativePath(manifestDirectory, relativePath);
                        if (!File.Exists(resolvedArtifactPath))
                        {
                            missingArtifactFiles.Add($"{requiredKind} ({resolvedArtifactPath})");
                            continue;
                        }

                        if (string.Equals(requiredKind, "screenshot-buffer-rgba16f-exr", StringComparison.Ordinal))
                        {
                            try
                            {
                                byte[] exrPayload = File.ReadAllBytes(resolvedArtifactPath);
                                _ = ExrHeaderValidation.ValidateRgba16Float(exrPayload, "NFR proof RGBA16F EXR artifact");
                            }
                            catch (Exception ex) when (ex is IOException or InvalidDataException)
                            {
                                invalidArtifactFiles.Add($"{requiredKind} ({resolvedArtifactPath}): {ex.Message}");
                            }
                        }
                    }

                    if (missingArtifactFiles.Count > 0)
                    {
                        failures.Add($"NFR proof artifacts manifest missing required artifact files: [{string.Join(", ", missingArtifactFiles)}].");
                    }

                    if (invalidArtifactFiles.Count > 0)
                    {
                        failures.Add($"NFR proof artifacts manifest invalid required artifact files: [{string.Join(", ", invalidArtifactFiles)}].");
                    }

                    if (missingKinds.Length == 0 && missingArtifactFiles.Count == 0 && invalidArtifactFiles.Count == 0)
                    {
                        artifactsManifestOk = true;
                    }
                }
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException)
            {
                failures.Add($"NFR proof artifacts manifest invalid: {ex.Message}");
            }
        }

        var checks = new NfrReleaseProofChecks(
            RuntimePerfMetrics: runtimePerfMetricsOk,
            RenderStats: renderStatsOk,
            MultiplayerSummary: multiplayerSummaryOk,
            NetProfileLog: netProfileLogOk,
            MultiplayerSnapshotBinary: snapshotBinaryOk,
            MultiplayerRpcBinary: rpcBinaryOk,
            ReplayRecording: replayRecordingOk,
            ArtifactsManifest: artifactsManifestOk,
            ReleaseInteropBudgetsMatch: releaseInteropBudgetsMatch,
            AllArtifactsPresent: runtimePerfMetricsOk && renderStatsOk && multiplayerSummaryOk && netProfileLogOk && snapshotBinaryOk && rpcBinaryOk && replayRecordingOk && artifactsManifestOk);

        NfrReleaseProofRuntimePerfSummary? serializedRuntimePerf = runtimePerfSummary is null
            ? null
            : new NfrReleaseProofRuntimePerfSummary(
                runtimePerfSummary.Value.Backend,
                runtimePerfSummary.Value.CaptureSampleCount,
                runtimePerfSummary.Value.AverageCaptureCpuMs,
                runtimePerfSummary.Value.PeakCaptureAllocatedBytes,
                runtimePerfSummary.Value.ZeroAllocationCapturePath,
                runtimePerfSummary.Value.ReleaseRendererInteropBudgetPerFrame,
                runtimePerfSummary.Value.ReleasePhysicsInteropBudgetPerTick);

        NfrReleaseProofRenderStatsSummary? serializedRenderStats = renderStatsSummary is null
            ? null
            : new NfrReleaseProofRenderStatsSummary(
                renderStatsSummary.Value.DrawItemCount,
                renderStatsSummary.Value.UiItemCount,
                renderStatsSummary.Value.TriangleCount,
                renderStatsSummary.Value.UploadBytes,
                renderStatsSummary.Value.GpuMemoryBytes,
                renderStatsSummary.Value.PresentCount);

        NfrReleaseProofRuntimeTransportSummary? serializedRuntimeTransport = runtimeTransportSummary is null
            ? null
            : new NfrReleaseProofRuntimeTransportSummary(
                runtimeTransportSummary.Value.Enabled,
                runtimeTransportSummary.Value.Succeeded,
                runtimeTransportSummary.Value.ServerMessagesReceived,
                runtimeTransportSummary.Value.ClientMessagesReceived);

        return new NfrReleaseProofArtifactSummary(checks, serializedRuntimePerf, serializedRenderStats, serializedRuntimeTransport);
    }

    private sealed record NfrReleaseProofDocument(
        DateTime GeneratedAtUtc,
        string Configuration,
        string RuntimeProjectPath,
        NfrReleaseProofStepResult Build,
        NfrReleaseProofStepResult Tests,
        NfrReleaseProofChecks Checks,
        NfrReleaseProofRuntimePerfSummary? RuntimePerfSummary,
        NfrReleaseProofRenderStatsSummary? RenderStatsSummary,
        NfrReleaseProofRuntimeTransportSummary? RuntimeTransportSummary,
        bool IsSuccess);

    private sealed record NfrReleaseProofStepResult(bool Succeeded, int ExitCode);

    private sealed record NfrReleaseProofChecks(
        bool RuntimePerfMetrics,
        bool RenderStats,
        bool MultiplayerSummary,
        bool NetProfileLog,
        bool MultiplayerSnapshotBinary,
        bool MultiplayerRpcBinary,
        bool ReplayRecording,
        bool ArtifactsManifest,
        bool ReleaseInteropBudgetsMatch,
        bool AllArtifactsPresent);

    private sealed record NfrReleaseProofArtifactSummary(
        NfrReleaseProofChecks Checks,
        NfrReleaseProofRuntimePerfSummary? RuntimePerfSummary,
        NfrReleaseProofRenderStatsSummary? RenderStatsSummary,
        NfrReleaseProofRuntimeTransportSummary? RuntimeTransportSummary);

    private sealed record NfrReleaseProofRuntimePerfSummary(
        string Backend,
        int CaptureSampleCount,
        double AverageCaptureCpuMs,
        long PeakCaptureAllocatedBytes,
        bool ZeroAllocationCapturePath,
        int ReleaseRendererInteropBudgetPerFrame,
        int ReleasePhysicsInteropBudgetPerTick);

    private sealed record NfrReleaseProofRenderStatsSummary(
        uint DrawItemCount,
        uint UiItemCount,
        ulong TriangleCount,
        ulong UploadBytes,
        ulong GpuMemoryBytes,
        ulong PresentCount);

    private sealed record NfrReleaseProofRuntimeTransportSummary(
        bool Enabled,
        bool Succeeded,
        int ServerMessagesReceived,
        int ClientMessagesReceived);
}
