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
        ValidateCaptureRgba16FloatBinary(command, projectDirectory, failures);
        ValidateRenderStatsArtifact(command, projectDirectory, failures);

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
