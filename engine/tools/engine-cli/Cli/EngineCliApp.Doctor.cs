using System.Text.Json;
using Engine.App;
using Engine.AssetPipeline;
using Engine.Net;

namespace Engine.Cli;

public sealed partial class EngineCliApp
{
    private int HandleDoctor(DoctorCommand command)
    {
        string projectDirectory = Path.GetFullPath(command.ProjectDirectory);
        if (!Directory.Exists(projectDirectory))
        {
            _stderr.WriteLine($"Project directory does not exist: {projectDirectory}");
            return 1;
        }

        string[] requiredPaths =
        [
            Path.Combine(projectDirectory, "project.json"),
            Path.Combine(projectDirectory, "assets", "manifest.json"),
            Path.Combine(projectDirectory, "src")
        ];

        var failures = new List<string>();
        foreach (string requiredPath in requiredPaths)
        {
            bool exists = requiredPath.EndsWith("src", StringComparison.OrdinalIgnoreCase)
                ? Directory.Exists(requiredPath)
                : File.Exists(requiredPath);
            if (!exists)
            {
                failures.Add($"Missing required path: {requiredPath}");
            }
        }

        ToolVersionCheckResult dotnetCheck = ExecuteToolVersionCheck("dotnet", projectDirectory);
        if (!dotnetCheck.IsSuccess)
        {
            failures.Add(dotnetCheck.BuildFailureMessage("dotnet"));
        }

        ToolVersionCheckResult cmakeCheck = ExecuteToolVersionCheck("cmake", projectDirectory);
        if (!cmakeCheck.IsSuccess)
        {
            failures.Add(cmakeCheck.BuildFailureMessage("cmake"));
        }

        ValidateRuntimePerfMetrics(command, projectDirectory, failures);
        ValidateMultiplayerRuntimeTransport(command, projectDirectory, failures);
        ValidateMultiplayerSnapshotBinary(command, projectDirectory, failures);
        ValidateMultiplayerRpcBinary(command, projectDirectory, failures);
        ValidateMultiplayerOrchestrationArtifact(command, projectDirectory, failures);
        ValidateCaptureRgba16FloatBinary(command, projectDirectory, failures);
        ValidateRenderStatsArtifact(command, projectDirectory, failures);
        ValidateTestHostConfigArtifact(command, projectDirectory, failures);
        ValidateNetProfileLogArtifact(command, projectDirectory, failures);
        ValidateReplayRecordingArtifact(command, projectDirectory, failures);
        ValidateArtifactsManifest(command, projectDirectory, failures);
        ValidateReleaseProofArtifact(command, projectDirectory, failures);

        if (failures.Count > 0)
        {
            foreach (string failure in failures)
            {
                _stderr.WriteLine(failure);
            }

            return 1;
        }

        _stdout.WriteLine("Doctor checks passed.");
        return 0;
    }

    private void ValidateMultiplayerRuntimeTransport(
        DoctorCommand command,
        string projectDirectory,
        List<string> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);

        bool explicitPath = !string.IsNullOrWhiteSpace(command.MultiplayerDemoSummaryPath);
        string relativeOrConfiguredPath = explicitPath
            ? command.MultiplayerDemoSummaryPath!
            : Path.Combine("artifacts", "tests", "net", "multiplayer-demo.json");
        string resolvedPath = AssetPipelineService.ResolveRelativePath(projectDirectory, relativeOrConfiguredPath);

        if (!File.Exists(resolvedPath))
        {
            if (command.RequireRuntimeTransportSuccess || explicitPath)
            {
                failures.Add($"Multiplayer demo summary artifact was not found: {resolvedPath}");
            }
            else
            {
                _stdout.WriteLine($"Multiplayer demo summary artifact not found, skipping check: {resolvedPath}");
            }

            return;
        }

        MultiplayerRuntimeTransportSummary summary;
        try
        {
            summary = ReadMultiplayerRuntimeTransportSummary(resolvedPath);
        }
        catch (InvalidDataException ex)
        {
            failures.Add(ex.Message);
            return;
        }

        _stdout.WriteLine(
            $"Multiplayer runtime transport: enabled={summary.Enabled}, succeeded={summary.Succeeded}, serverMessages={summary.ServerMessagesReceived}, clientMessages={summary.ClientMessagesReceived}.");

        if (!command.RequireRuntimeTransportSuccess)
        {
            return;
        }

        if (!summary.Enabled)
        {
            failures.Add("Multiplayer runtime transport check failed: runtime transport is disabled in summary artifact.");
            return;
        }

        if (!summary.Succeeded)
        {
            failures.Add("Multiplayer runtime transport check failed: runtime transport did not succeed.");
            return;
        }

        if (summary.ServerMessagesReceived <= 0 || summary.ClientMessagesReceived <= 0)
        {
            failures.Add("Multiplayer runtime transport check failed: message counters must be greater than zero.");
        }
    }

    private void ValidateMultiplayerSnapshotBinary(
        DoctorCommand command,
        string projectDirectory,
        List<string> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);

        bool explicitPath = !string.IsNullOrWhiteSpace(command.MultiplayerSnapshotBinaryPath);
        string relativeOrConfiguredPath = explicitPath
            ? command.MultiplayerSnapshotBinaryPath!
            : Path.Combine("artifacts", "tests", "net", "multiplayer-snapshot.bin");
        string resolvedPath = AssetPipelineService.ResolveRelativePath(projectDirectory, relativeOrConfiguredPath);

        if (!File.Exists(resolvedPath))
        {
            if (command.VerifyMultiplayerSnapshotBinary || explicitPath)
            {
                failures.Add($"Multiplayer snapshot binary artifact was not found: {resolvedPath}");
            }
            else
            {
                _stdout.WriteLine($"Multiplayer snapshot binary artifact not found, skipping check: {resolvedPath}");
            }

            return;
        }

        try
        {
            byte[] payload = File.ReadAllBytes(resolvedPath);
            NetSnapshot snapshot = NetSnapshotBinaryCodec.Decode(payload);
            _stdout.WriteLine(
                $"Multiplayer snapshot binary: tick={snapshot.Tick}, entities={snapshot.Entities.Count}.");
            if (command.VerifyMultiplayerSnapshotBinary && snapshot.Entities.Count == 0)
            {
                failures.Add("Multiplayer snapshot binary check failed: decoded snapshot is empty.");
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            failures.Add($"Multiplayer snapshot binary check failed: {ex.Message}");
        }
    }

    private void ValidateMultiplayerRpcBinary(
        DoctorCommand command,
        string projectDirectory,
        List<string> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);

        bool explicitPath = !string.IsNullOrWhiteSpace(command.MultiplayerRpcBinaryPath);
        string relativeOrConfiguredPath = explicitPath
            ? command.MultiplayerRpcBinaryPath!
            : Path.Combine("artifacts", "tests", "net", "multiplayer-rpc.bin");
        string resolvedPath = AssetPipelineService.ResolveRelativePath(projectDirectory, relativeOrConfiguredPath);

        if (!File.Exists(resolvedPath))
        {
            if (command.VerifyMultiplayerRpcBinary || explicitPath)
            {
                failures.Add($"Multiplayer RPC binary artifact was not found: {resolvedPath}");
            }
            else
            {
                _stdout.WriteLine($"Multiplayer RPC binary artifact not found, skipping check: {resolvedPath}");
            }

            return;
        }

        try
        {
            byte[] payload = File.ReadAllBytes(resolvedPath);
            NetRpcMessage message = NetRpcBinaryCodec.Decode(payload);
            _stdout.WriteLine(
                $"Multiplayer RPC binary: rpc={message.RpcName}, entity={message.EntityId}, channel={message.Channel}, payloadBytes={message.Payload.Length}.");
            if (command.VerifyMultiplayerRpcBinary && message.Payload.Length == 0)
            {
                failures.Add("Multiplayer RPC binary check failed: decoded RPC payload is empty.");
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            failures.Add($"Multiplayer RPC binary check failed: {ex.Message}");
        }
    }

    private void ValidateMultiplayerOrchestrationArtifact(
        DoctorCommand command,
        string projectDirectory,
        List<string> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);

        bool explicitPath = !string.IsNullOrWhiteSpace(command.MultiplayerOrchestrationPath);
        string relativeOrConfiguredPath = explicitPath
            ? command.MultiplayerOrchestrationPath!
            : Path.Combine("artifacts", "runtime-multiplayer-orchestration", "net", "multiplayer-orchestration.json");
        string resolvedPath = AssetPipelineService.ResolveRelativePath(projectDirectory, relativeOrConfiguredPath);

        if (!File.Exists(resolvedPath))
        {
            if (command.VerifyMultiplayerOrchestration || explicitPath)
            {
                failures.Add($"Multiplayer orchestration artifact was not found: {resolvedPath}");
            }
            else
            {
                _stdout.WriteLine($"Multiplayer orchestration artifact not found, skipping check: {resolvedPath}");
            }

            return;
        }

        try
        {
            string json = File.ReadAllText(resolvedPath);
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;

            bool allSucceeded = ReadRequiredBool(root, "allSucceeded", "Multiplayer orchestration artifact");
            bool requireNativeTransport = ReadRequiredBool(root, "requireNativeTransportSuccess", "Multiplayer orchestration artifact");
            if (!root.TryGetProperty("nodes", out JsonElement nodes) || nodes.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException("Multiplayer orchestration artifact is missing array property 'nodes'.");
            }

            int nodeCount = nodes.GetArrayLength();
            bool hasServer = false;
            bool hasClient1 = false;
            bool hasClient2 = false;
            bool allNodeExitCodesZero = true;
            bool allNodeNativeTransportSucceeded = true;
            for (int i = 0; i < nodeCount; i++)
            {
                JsonElement node = nodes[i];
                string role = ReadRequiredString(node, "role", "Multiplayer orchestration node");
                int exitCode = ReadRequiredInt(node, "exitCode", "Multiplayer orchestration node");
                bool runtimeTransportSucceeded = ReadRequiredBool(node, "runtimeTransportSucceeded", "Multiplayer orchestration node");
                hasServer |= string.Equals(role, "server", StringComparison.Ordinal);
                hasClient1 |= string.Equals(role, "client-1", StringComparison.Ordinal);
                hasClient2 |= string.Equals(role, "client-2", StringComparison.Ordinal);
                allNodeExitCodesZero &= exitCode == 0;
                allNodeNativeTransportSucceeded &= runtimeTransportSucceeded;
            }

            _stdout.WriteLine(
                $"Multiplayer orchestration: nodes={nodeCount}, allSucceeded={allSucceeded}, requireNativeTransport={requireNativeTransport}.");

            if (!command.VerifyMultiplayerOrchestration)
            {
                return;
            }

            bool requiredRolesPresent = hasServer && hasClient1 && hasClient2;
            bool nativeTransportSatisfied = !requireNativeTransport || allNodeNativeTransportSucceeded;
            if (nodeCount < 3 || !requiredRolesPresent || !allSucceeded || !allNodeExitCodesZero || !nativeTransportSatisfied)
            {
                failures.Add("Multiplayer orchestration check failed: expected server/client-1/client-2 nodes, zero exits, success=true and native transport success when required.");
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException)
        {
            failures.Add($"Multiplayer orchestration check failed: {ex.Message}");
        }
    }

    private void ValidateCaptureRgba16FloatBinary(
        DoctorCommand command,
        string projectDirectory,
        List<string> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);

        bool explicitPath = !string.IsNullOrWhiteSpace(command.CaptureRgba16FloatBinaryPath);
        string relativeOrConfiguredPath = explicitPath
            ? command.CaptureRgba16FloatBinaryPath!
            : Path.Combine("artifacts", "tests", "screenshots", "frame-0001.rgba16f.bin");
        string resolvedPath = AssetPipelineService.ResolveRelativePath(projectDirectory, relativeOrConfiguredPath);

        if (!File.Exists(resolvedPath))
        {
            if (command.VerifyCaptureRgba16FloatBinary || explicitPath)
            {
                failures.Add($"Capture RGBA16F binary artifact was not found: {resolvedPath}");
            }
            else
            {
                _stdout.WriteLine($"Capture RGBA16F binary artifact not found, skipping check: {resolvedPath}");
            }

            return;
        }

        try
        {
            byte[] payload = File.ReadAllBytes(resolvedPath);
            if (payload.Length == 0)
            {
                throw new InvalidDataException("Capture RGBA16F binary payload is empty.");
            }

            if ((payload.Length % 8) != 0)
            {
                throw new InvalidDataException($"Capture RGBA16F binary payload length must be divisible by 8 bytes per pixel, got {payload.Length}.");
            }

            int pixelCount = payload.Length / 8;
            _stdout.WriteLine($"Capture RGBA16F binary: pixels={pixelCount}, bytes={payload.Length}.");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            failures.Add($"Capture RGBA16F binary check failed: {ex.Message}");
        }
    }

    private void ValidateRenderStatsArtifact(
        DoctorCommand command,
        string projectDirectory,
        List<string> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);

        bool explicitPath = !string.IsNullOrWhiteSpace(command.RenderStatsArtifactPath);
        string relativeOrConfiguredPath = explicitPath
            ? command.RenderStatsArtifactPath!
            : Path.Combine("artifacts", "tests", "render", "frame-stats.json");
        string resolvedPath = AssetPipelineService.ResolveRelativePath(projectDirectory, relativeOrConfiguredPath);

        if (!File.Exists(resolvedPath))
        {
            if (command.VerifyRenderStatsArtifact || explicitPath)
            {
                failures.Add($"Render stats artifact was not found: {resolvedPath}");
            }
            else
            {
                _stdout.WriteLine($"Render stats artifact not found, skipping check: {resolvedPath}");
            }

            return;
        }

        try
        {
            string json = File.ReadAllText(resolvedPath);
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;

            int drawItemCount = ReadRequiredInt(root, "drawItemCount", "Render stats artifact");
            ulong triangleCount = ReadRequiredUInt64(root, "triangleCount", "Render stats artifact");
            ulong uploadBytes = ReadRequiredUInt64(root, "uploadBytes", "Render stats artifact");
            ulong gpuMemoryBytes = ReadRequiredUInt64(root, "gpuMemoryBytes", "Render stats artifact");
            ulong presentCount = ReadRequiredUInt64(root, "presentCount", "Render stats artifact");

            _stdout.WriteLine(
                $"Render stats: drawItems={drawItemCount}, triangles={triangleCount}, uploadBytes={uploadBytes}, gpuMemoryBytes={gpuMemoryBytes}, presents={presentCount}.");

            if (!command.VerifyRenderStatsArtifact)
            {
                return;
            }

            if (drawItemCount <= 0 || triangleCount == 0UL || uploadBytes == 0UL || gpuMemoryBytes == 0UL || presentCount == 0UL)
            {
                failures.Add("Render stats check failed: draw/triangle/upload/gpu memory/present counters must all be greater than zero.");
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException)
        {
            failures.Add($"Render stats check failed: {ex.Message}");
        }
    }

    private void ValidateTestHostConfigArtifact(
        DoctorCommand command,
        string projectDirectory,
        List<string> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);

        bool explicitPath = !string.IsNullOrWhiteSpace(command.TestHostConfigPath);
        string relativeOrConfiguredPath = explicitPath
            ? command.TestHostConfigPath!
            : Path.Combine("artifacts", "tests", "runtime", "test-host.json");
        string resolvedPath = AssetPipelineService.ResolveRelativePath(projectDirectory, relativeOrConfiguredPath);

        if (!File.Exists(resolvedPath))
        {
            if (command.VerifyTestHostConfig || explicitPath)
            {
                failures.Add($"Test host config artifact was not found: {resolvedPath}");
            }
            else
            {
                _stdout.WriteLine($"Test host config artifact not found, skipping check: {resolvedPath}");
            }

            return;
        }

        try
        {
            string json = File.ReadAllText(resolvedPath);
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;

            string mode = ReadRequiredString(root, "mode", "Test host config artifact");
            if (!string.Equals(mode, "headless-offscreen", StringComparison.Ordinal) &&
                !string.Equals(mode, "hidden-window", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Test host config artifact has unsupported mode '{mode}'.");
            }

            double fixedDeltaSeconds = ReadRequiredDouble(root, "fixedDeltaSeconds", "Test host config artifact");
            if (!double.IsFinite(fixedDeltaSeconds) || fixedDeltaSeconds <= 0.0)
            {
                throw new InvalidDataException("Test host config artifact has invalid positive number 'fixedDeltaSeconds'.");
            }

            _stdout.WriteLine($"Test host config: mode={mode}, fixedDeltaSeconds={fixedDeltaSeconds:F6}.");
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException)
        {
            failures.Add($"Test host config check failed: {ex.Message}");
        }
    }

    private void ValidateNetProfileLogArtifact(
        DoctorCommand command,
        string projectDirectory,
        List<string> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);

        bool explicitPath = !string.IsNullOrWhiteSpace(command.NetProfileLogPath);
        string relativeOrConfiguredPath = explicitPath
            ? command.NetProfileLogPath!
            : Path.Combine("artifacts", "tests", "net", "multiplayer-profile.log");
        string resolvedPath = AssetPipelineService.ResolveRelativePath(projectDirectory, relativeOrConfiguredPath);

        if (!File.Exists(resolvedPath))
        {
            if (command.VerifyNetProfileLog || explicitPath)
            {
                failures.Add($"Net profile log artifact was not found: {resolvedPath}");
            }
            else
            {
                _stdout.WriteLine($"Net profile log artifact not found, skipping check: {resolvedPath}");
            }

            return;
        }

        try
        {
            string[] lines = File.ReadAllLines(resolvedPath)
                .Where(static line => !string.IsNullOrWhiteSpace(line))
                .ToArray();
            _stdout.WriteLine($"Net profile log: lines={lines.Length}.");

            if (!command.VerifyNetProfileLog)
            {
                return;
            }

            bool hasServerLine = lines.Any(static line => line.Contains("server bytesSent=", StringComparison.Ordinal));
            bool hasClientLine = lines.Any(static line => line.StartsWith("client-", StringComparison.Ordinal));
            bool hasRuntimeTransportLine = lines.Any(static line => line.StartsWith("runtime-transport ", StringComparison.Ordinal));
            if (!hasServerLine || !hasClientLine || !hasRuntimeTransportLine)
            {
                failures.Add("Net profile log check failed: expected runtime-transport, server and client lines.");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            failures.Add($"Net profile log check failed: {ex.Message}");
        }
    }

    private void ValidateReplayRecordingArtifact(
        DoctorCommand command,
        string projectDirectory,
        List<string> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);

        bool explicitPath = !string.IsNullOrWhiteSpace(command.ReplayRecordingPath);
        string relativeOrConfiguredPath = explicitPath
            ? command.ReplayRecordingPath!
            : Path.Combine("artifacts", "tests", "replay", "recording.json");
        string resolvedPath = AssetPipelineService.ResolveRelativePath(projectDirectory, relativeOrConfiguredPath);

        if (!File.Exists(resolvedPath))
        {
            if (command.VerifyReplayRecording || explicitPath)
            {
                failures.Add($"Replay recording artifact was not found: {resolvedPath}");
            }
            else
            {
                _stdout.WriteLine($"Replay recording artifact not found, skipping check: {resolvedPath}");
            }

            return;
        }

        try
        {
            string json = File.ReadAllText(resolvedPath);
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;

            double fixedDeltaSeconds = ReadRequiredDouble(root, "fixedDeltaSeconds", "Replay recording artifact");
            if (!double.IsFinite(fixedDeltaSeconds) || fixedDeltaSeconds <= 0.0)
            {
                throw new InvalidDataException("Replay recording artifact has invalid positive number 'fixedDeltaSeconds'.");
            }

            if (!root.TryGetProperty("frames", out JsonElement frames) || frames.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException("Replay recording artifact is missing array property 'frames'.");
            }

            int frameCount = frames.GetArrayLength();
            for (int i = 0; i < frameCount; i++)
            {
                JsonElement frame = frames[i];
                if (!frame.TryGetProperty("tick", out JsonElement tickElement) || !tickElement.TryGetInt64(out long tick) || tick < 0L)
                {
                    throw new InvalidDataException("Replay recording artifact contains invalid non-negative frame tick value.");
                }
            }

            if (!root.TryGetProperty("networkEvents", out JsonElement networkEvents) || networkEvents.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException("Replay recording artifact is missing array property 'networkEvents'.");
            }

            int networkEventCount = networkEvents.GetArrayLength();
            bool hasCaptureEvent = false;
            bool hasProfileEvent = false;
            for (int i = 0; i < networkEventCount; i++)
            {
                if (networkEvents[i].ValueKind != JsonValueKind.String)
                {
                    throw new InvalidDataException("Replay recording artifact contains non-string network event value.");
                }

                string value = networkEvents[i].GetString() ?? string.Empty;
                hasCaptureEvent |= value.StartsWith("capture.frame=", StringComparison.Ordinal);
                hasProfileEvent |= value.StartsWith("net.profile=", StringComparison.Ordinal);
            }

            if (!root.TryGetProperty("timedNetworkEvents", out JsonElement timedNetworkEvents) || timedNetworkEvents.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException("Replay recording artifact is missing array property 'timedNetworkEvents'.");
            }

            int timedNetworkEventCount = timedNetworkEvents.GetArrayLength();
            for (int i = 0; i < timedNetworkEventCount; i++)
            {
                JsonElement timed = timedNetworkEvents[i];
                if (!timed.TryGetProperty("tick", out JsonElement tickElement) || !tickElement.TryGetInt64(out long tick) || tick < 0L)
                {
                    throw new InvalidDataException("Replay recording artifact contains invalid non-negative timed network event tick.");
                }

                if (!timed.TryGetProperty("event", out JsonElement eventElement) || eventElement.ValueKind != JsonValueKind.String)
                {
                    throw new InvalidDataException("Replay recording artifact contains invalid timed network event value.");
                }
            }

            _stdout.WriteLine(
                $"Replay recording: frames={frameCount}, networkEvents={networkEventCount}, timedNetworkEvents={timedNetworkEventCount}, fixedDeltaSeconds={fixedDeltaSeconds:F6}.");

            if (!command.VerifyReplayRecording)
            {
                return;
            }

            if (frameCount == 0 || networkEventCount == 0 || timedNetworkEventCount == 0 || !hasCaptureEvent || !hasProfileEvent)
            {
                failures.Add("Replay recording check failed: frames/events arrays must be non-empty and include capture/profile markers.");
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException)
        {
            failures.Add($"Replay recording check failed: {ex.Message}");
        }
    }

    private void ValidateArtifactsManifest(
        DoctorCommand command,
        string projectDirectory,
        List<string> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);

        bool explicitPath = !string.IsNullOrWhiteSpace(command.ArtifactsManifestPath);
        string relativeOrConfiguredPath = explicitPath
            ? command.ArtifactsManifestPath!
            : Path.Combine("artifacts", "tests", "manifest.json");
        string resolvedPath = AssetPipelineService.ResolveRelativePath(projectDirectory, relativeOrConfiguredPath);

        if (!File.Exists(resolvedPath))
        {
            if (command.VerifyArtifactsManifest || explicitPath)
            {
                failures.Add($"Artifacts manifest was not found: {resolvedPath}");
            }
            else
            {
                _stdout.WriteLine($"Artifacts manifest not found, skipping check: {resolvedPath}");
            }

            return;
        }

        try
        {
            string json = File.ReadAllText(resolvedPath);
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (!root.TryGetProperty("artifacts", out JsonElement artifacts) || artifacts.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException("Artifacts manifest is missing array property 'artifacts'.");
            }

            var kinds = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonElement artifact in artifacts.EnumerateArray())
            {
                string kind = ReadRequiredString(artifact, "kind", "Artifacts manifest artifact entry");
                kinds.Add(kind);
            }

            _stdout.WriteLine($"Artifacts manifest: entries={kinds.Count}.");
            if (!command.VerifyArtifactsManifest)
            {
                return;
            }

            string[] requiredKinds =
            [
                "screenshot",
                "screenshot-buffer",
                "screenshot-buffer-rgba16f",
                "multiplayer-demo",
                "net-profile-log",
                "multiplayer-snapshot-bin",
                "multiplayer-rpc-bin",
                "render-stats-log",
                "test-host-config",
                "runtime-perf-metrics",
                "replay"
            ];

            string[] missingKinds = requiredKinds.Where(required => !kinds.Contains(required)).ToArray();
            if (missingKinds.Length > 0)
            {
                failures.Add($"Artifacts manifest check failed: missing required kinds [{string.Join(", ", missingKinds)}].");
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException)
        {
            failures.Add($"Artifacts manifest check failed: {ex.Message}");
        }
    }

    private void ValidateReleaseProofArtifact(
        DoctorCommand command,
        string projectDirectory,
        List<string> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);

        bool explicitPath = !string.IsNullOrWhiteSpace(command.ReleaseProofPath);
        string relativeOrConfiguredPath = explicitPath
            ? command.ReleaseProofPath!
            : Path.Combine("artifacts", "nfr", "release-proof.json");
        string resolvedPath = AssetPipelineService.ResolveRelativePath(projectDirectory, relativeOrConfiguredPath);

        if (!File.Exists(resolvedPath))
        {
            if (command.VerifyReleaseProof || explicitPath)
            {
                failures.Add($"NFR release proof artifact was not found: {resolvedPath}");
            }
            else
            {
                _stdout.WriteLine($"NFR release proof artifact not found, skipping check: {resolvedPath}");
            }

            return;
        }

        try
        {
            string json = File.ReadAllText(resolvedPath);
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;

            bool isSuccess = ReadRequiredBool(root, "isSuccess", "NFR release proof artifact");
            string configuration = ReadRequiredString(root, "configuration", "NFR release proof artifact");
            if (!root.TryGetProperty("checks", out JsonElement checks) || checks.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("NFR release proof artifact is missing object property 'checks'.");
            }

            bool runtimePerfMetrics = ReadRequiredBool(checks, "runtimePerfMetrics", "NFR release proof checks");
            bool renderStats = ReadRequiredBool(checks, "renderStats", "NFR release proof checks");
            bool multiplayerSummary = ReadRequiredBool(checks, "multiplayerSummary", "NFR release proof checks");
            bool netProfileLog = ReadRequiredBool(checks, "netProfileLog", "NFR release proof checks");
            bool multiplayerSnapshotBinary = ReadRequiredBool(checks, "multiplayerSnapshotBinary", "NFR release proof checks");
            bool multiplayerRpcBinary = ReadRequiredBool(checks, "multiplayerRpcBinary", "NFR release proof checks");
            bool replayRecording = ReadRequiredBool(checks, "replayRecording", "NFR release proof checks");
            bool artifactsManifest = ReadRequiredBool(checks, "artifactsManifest", "NFR release proof checks");
            bool releaseInteropBudgetsMatch = ReadRequiredBool(checks, "releaseInteropBudgetsMatch", "NFR release proof checks");
            bool allArtifactsPresent = ReadRequiredBool(checks, "allArtifactsPresent", "NFR release proof checks");

            _stdout.WriteLine(
                $"NFR release proof: success={isSuccess}, configuration={configuration}, allArtifacts={allArtifactsPresent}, releaseBudgets={releaseInteropBudgetsMatch}.");

            if (!command.VerifyReleaseProof)
            {
                return;
            }

            bool allChecksPassed = isSuccess &&
                allArtifactsPresent &&
                releaseInteropBudgetsMatch &&
                runtimePerfMetrics &&
                renderStats &&
                multiplayerSummary &&
                netProfileLog &&
                multiplayerSnapshotBinary &&
                multiplayerRpcBinary &&
                replayRecording &&
                artifactsManifest;
            if (!allChecksPassed)
            {
                failures.Add("NFR release proof check failed: expected successful proof with all checks passing.");
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException)
        {
            failures.Add($"NFR release proof check failed: {ex.Message}");
        }
    }

    private void ValidateRuntimePerfMetrics(
        DoctorCommand command,
        string projectDirectory,
        List<string> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);

        bool explicitPath = !string.IsNullOrWhiteSpace(command.RuntimePerfMetricsPath);
        string relativeOrConfiguredPath = explicitPath
            ? command.RuntimePerfMetricsPath!
            : Path.Combine("artifacts", "tests", "runtime", "perf-metrics.json");
        string resolvedPath = AssetPipelineService.ResolveRelativePath(projectDirectory, relativeOrConfiguredPath);

        if (!File.Exists(resolvedPath))
        {
            if (explicitPath)
            {
                failures.Add($"Runtime perf metrics artifact was not found: {resolvedPath}");
            }
            else
            {
                _stdout.WriteLine($"Runtime perf metrics artifact not found, skipping check: {resolvedPath}");
            }

            return;
        }

        RuntimePerfMetricsSummary metrics;
        try
        {
            metrics = ReadRuntimePerfMetricsSummary(resolvedPath);
        }
        catch (InvalidDataException ex)
        {
            failures.Add(ex.Message);
            return;
        }

        if (command.MaxAverageCaptureCpuMs.HasValue &&
            metrics.AverageCaptureCpuMs > command.MaxAverageCaptureCpuMs.Value)
        {
            failures.Add(
                $"Runtime perf budget exceeded: average capture CPU {metrics.AverageCaptureCpuMs:F4}ms > {command.MaxAverageCaptureCpuMs.Value:F4}ms.");
        }

        if (command.MaxPeakCaptureAllocatedBytes.HasValue &&
            metrics.PeakCaptureAllocatedBytes > command.MaxPeakCaptureAllocatedBytes.Value)
        {
            failures.Add(
                $"Runtime perf budget exceeded: peak capture allocation {metrics.PeakCaptureAllocatedBytes} bytes > {command.MaxPeakCaptureAllocatedBytes.Value} bytes.");
        }

        if (command.RequireZeroAllocationCapturePath && !metrics.ZeroAllocationCapturePath)
        {
            failures.Add("Runtime perf budget exceeded: capture path is not zero-allocation.");
        }

        InteropBudgetOptions releaseBudgets = InteropBudgetOptions.ReleaseStrict;
        if (metrics.ReleaseRendererInteropBudgetPerFrame != releaseBudgets.MaxRendererCallsPerFrame)
        {
            failures.Add(
                $"Runtime perf metrics mismatch: release renderer interop budget {metrics.ReleaseRendererInteropBudgetPerFrame} != expected {releaseBudgets.MaxRendererCallsPerFrame}.");
        }

        if (metrics.ReleasePhysicsInteropBudgetPerTick != releaseBudgets.MaxPhysicsCallsPerTick)
        {
            failures.Add(
                $"Runtime perf metrics mismatch: release physics interop budget {metrics.ReleasePhysicsInteropBudgetPerTick} != expected {releaseBudgets.MaxPhysicsCallsPerTick}.");
        }

        _stdout.WriteLine(
            $"Runtime perf metrics: backend={metrics.Backend}, samples={metrics.CaptureSampleCount}, avgCaptureCpuMs={metrics.AverageCaptureCpuMs:F4}, peakCaptureAllocBytes={metrics.PeakCaptureAllocatedBytes}, zeroAlloc={metrics.ZeroAllocationCapturePath}.");
    }

    private static RuntimePerfMetricsSummary ReadRuntimePerfMetricsSummary(string statsPath)
    {
        string json = File.ReadAllText(statsPath);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        string backend = ReadRequiredString(root, "backend", "Runtime perf metrics artifact");
        int captureSampleCount = ReadRequiredInt(root, "captureSampleCount", "Runtime perf metrics artifact");
        if (captureSampleCount <= 0)
        {
            throw new InvalidDataException("Runtime perf metrics artifact has invalid positive integer 'captureSampleCount'.");
        }

        double averageCaptureCpuMs = ReadRequiredDouble(root, "averageCaptureCpuMs", "Runtime perf metrics artifact");
        if (!double.IsFinite(averageCaptureCpuMs) || averageCaptureCpuMs < 0.0)
        {
            throw new InvalidDataException("Runtime perf metrics artifact has invalid non-negative number 'averageCaptureCpuMs'.");
        }

        long peakCaptureAllocatedBytes = ReadRequiredLong(root, "peakCaptureAllocatedBytes", "Runtime perf metrics artifact");
        if (peakCaptureAllocatedBytes < 0L)
        {
            throw new InvalidDataException("Runtime perf metrics artifact has invalid non-negative integer 'peakCaptureAllocatedBytes'.");
        }

        bool zeroAllocationCapturePath = ReadRequiredBool(root, "zeroAllocationCapturePath", "Runtime perf metrics artifact");

        int releaseRendererInteropBudgetPerFrame = ReadRequiredInt(root, "releaseRendererInteropBudgetPerFrame", "Runtime perf metrics artifact");
        int releasePhysicsInteropBudgetPerTick = ReadRequiredInt(root, "releasePhysicsInteropBudgetPerTick", "Runtime perf metrics artifact");
        if (releaseRendererInteropBudgetPerFrame <= 0 || releasePhysicsInteropBudgetPerTick <= 0)
        {
            throw new InvalidDataException("Runtime perf metrics artifact has invalid positive release interop budget values.");
        }

        return new RuntimePerfMetricsSummary(
            backend,
            captureSampleCount,
            averageCaptureCpuMs,
            peakCaptureAllocatedBytes,
            zeroAllocationCapturePath,
            releaseRendererInteropBudgetPerFrame,
            releasePhysicsInteropBudgetPerTick);
    }

    private static MultiplayerRuntimeTransportSummary ReadMultiplayerRuntimeTransportSummary(string summaryPath)
    {
        string json = File.ReadAllText(summaryPath);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        if (!root.TryGetProperty("runtimeTransport", out JsonElement runtimeTransport) || runtimeTransport.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Multiplayer summary artifact is missing object property 'runtimeTransport'.");
        }

        bool enabled = ReadRequiredBool(runtimeTransport, "enabled", "Multiplayer summary artifact runtimeTransport");
        bool succeeded = ReadRequiredBool(runtimeTransport, "succeeded", "Multiplayer summary artifact runtimeTransport");
        int serverMessagesReceived = ReadRequiredInt(runtimeTransport, "serverMessagesReceived", "Multiplayer summary artifact runtimeTransport");
        int clientMessagesReceived = ReadRequiredInt(runtimeTransport, "clientMessagesReceived", "Multiplayer summary artifact runtimeTransport");
        if (serverMessagesReceived < 0 || clientMessagesReceived < 0)
        {
            throw new InvalidDataException("Multiplayer summary artifact runtimeTransport contains negative message counters.");
        }

        return new MultiplayerRuntimeTransportSummary(
            enabled,
            succeeded,
            serverMessagesReceived,
            clientMessagesReceived);
    }

    private static string ReadRequiredString(JsonElement root, string propertyName, string context)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement property) || property.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException($"{context} is missing string property '{propertyName}'.");
        }

        string? value = property.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException($"{context} has empty string property '{propertyName}'.");
        }

        return value;
    }

    private static int ReadRequiredInt(JsonElement root, string propertyName, string context)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement property) || !property.TryGetInt32(out int value))
        {
            throw new InvalidDataException($"{context} is missing integer property '{propertyName}'.");
        }

        return value;
    }

    private static long ReadRequiredLong(JsonElement root, string propertyName, string context)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement property) || !property.TryGetInt64(out long value))
        {
            throw new InvalidDataException($"{context} is missing integer property '{propertyName}'.");
        }

        return value;
    }

    private static ulong ReadRequiredUInt64(JsonElement root, string propertyName, string context)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement property) || !property.TryGetUInt64(out ulong value))
        {
            throw new InvalidDataException($"{context} is missing unsigned integer property '{propertyName}'.");
        }

        return value;
    }

    private static double ReadRequiredDouble(JsonElement root, string propertyName, string context)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement property) || !property.TryGetDouble(out double value))
        {
            throw new InvalidDataException($"{context} is missing numeric property '{propertyName}'.");
        }

        return value;
    }

    private static bool ReadRequiredBool(JsonElement root, string propertyName, string context)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement property) ||
            (property.ValueKind != JsonValueKind.True && property.ValueKind != JsonValueKind.False))
        {
            throw new InvalidDataException($"{context} is missing boolean property '{propertyName}'.");
        }

        return property.GetBoolean();
    }

    private ToolVersionCheckResult ExecuteToolVersionCheck(string executable, string workingDirectory)
    {
        try
        {
            int exitCode = _commandRunner.Run(
                executable,
                ["--version"],
                workingDirectory,
                _stdout,
                _stderr);

            return exitCode == 0
                ? ToolVersionCheckResult.Success
                : new ToolVersionCheckResult(IsSuccess: false, ExitCode: exitCode, ErrorMessage: null);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            return new ToolVersionCheckResult(IsSuccess: false, ExitCode: null, ErrorMessage: ex.Message);
        }
    }

    private readonly record struct ToolVersionCheckResult(
        bool IsSuccess,
        int? ExitCode,
        string? ErrorMessage)
    {
        public static ToolVersionCheckResult Success { get; } = new(
            IsSuccess: true,
            ExitCode: 0,
            ErrorMessage: null);

        public string BuildFailureMessage(string executable)
        {
            if (!string.IsNullOrWhiteSpace(ErrorMessage))
            {
                return $"{executable} version check failed: {ErrorMessage}";
            }

            if (ExitCode.HasValue)
            {
                return $"{executable} CLI version check failed with exit code {ExitCode.Value}.";
            }

            return $"{executable} version check failed.";
        }
    }

    private readonly record struct RuntimePerfMetricsSummary(
        string Backend,
        int CaptureSampleCount,
        double AverageCaptureCpuMs,
        long PeakCaptureAllocatedBytes,
        bool ZeroAllocationCapturePath,
        int ReleaseRendererInteropBudgetPerFrame,
        int ReleasePhysicsInteropBudgetPerTick);

    private readonly record struct MultiplayerRuntimeTransportSummary(
        bool Enabled,
        bool Succeeded,
        int ServerMessagesReceived,
        int ClientMessagesReceived);
}
