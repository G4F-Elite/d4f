using Engine.Cli;
using Engine.Net;

namespace Engine.Cli.Tests;

public sealed class EngineCliDoctorToolChecksTests
{
    [Fact]
    public void Run_ShouldFailDoctor_WhenDotnetVersionCheckReturnsNonZero()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareDoctorProject(tempRoot);

            var runner = new SelectiveDoctorRunner
            {
                DotnetExitCode = 17,
                CmakeExitCode = 0
            };
            using var output = new StringWriter();
            using var error = new StringWriter();
            var app = new EngineCliApp(output, error, runner);

            int code = app.Run(["doctor", "--project", tempRoot]);

            Assert.Equal(1, code);
            Assert.Equal(2, runner.Invocations.Count);
            Assert.Equal("dotnet", runner.Invocations[0].ExecutablePath);
            Assert.Equal("cmake", runner.Invocations[1].ExecutablePath);
            Assert.Contains(
                "dotnet CLI version check failed with exit code 17.",
                error.ToString(),
                StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_ShouldFailDoctor_WhenDotnetVersionCheckThrows()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareDoctorProject(tempRoot);

            var runner = new SelectiveDoctorRunner
            {
                ThrowOnDotnet = true,
                CmakeExitCode = 0
            };
            using var output = new StringWriter();
            using var error = new StringWriter();
            var app = new EngineCliApp(output, error, runner);

            int code = app.Run(["doctor", "--project", tempRoot]);

            Assert.Equal(1, code);
            Assert.Equal(2, runner.Invocations.Count);
            Assert.Equal("dotnet", runner.Invocations[0].ExecutablePath);
            Assert.Equal("cmake", runner.Invocations[1].ExecutablePath);
            Assert.Contains(
                "dotnet version check failed: Failed to start process 'dotnet'.",
                error.ToString(),
                StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_ShouldFailDoctor_WhenCmakeVersionCheckReturnsNonZero()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareDoctorProject(tempRoot);

            var runner = new SelectiveDoctorRunner
            {
                DotnetExitCode = 0,
                CmakeExitCode = 9
            };
            using var output = new StringWriter();
            using var error = new StringWriter();
            var app = new EngineCliApp(output, error, runner);

            int code = app.Run(["doctor", "--project", tempRoot]);

            Assert.Equal(1, code);
            Assert.Equal(2, runner.Invocations.Count);
            Assert.Equal("dotnet", runner.Invocations[0].ExecutablePath);
            Assert.Equal("cmake", runner.Invocations[1].ExecutablePath);
            Assert.Contains(
                "cmake CLI version check failed with exit code 9.",
                error.ToString(),
                StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_ShouldFailDoctor_WhenCmakeVersionCheckThrows()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareDoctorProject(tempRoot);

            var runner = new SelectiveDoctorRunner
            {
                DotnetExitCode = 0,
                ThrowOnCmake = true
            };
            using var output = new StringWriter();
            using var error = new StringWriter();
            var app = new EngineCliApp(output, error, runner);

            int code = app.Run(["doctor", "--project", tempRoot]);

            Assert.Equal(1, code);
            Assert.Equal(2, runner.Invocations.Count);
            Assert.Equal("dotnet", runner.Invocations[0].ExecutablePath);
            Assert.Equal("cmake", runner.Invocations[1].ExecutablePath);
            Assert.Contains(
                "cmake version check failed: Failed to start process 'cmake'.",
                error.ToString(),
                StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_ShouldFailDoctor_WhenExplicitRuntimePerfPathMissing()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareDoctorProject(tempRoot);

            var runner = new SelectiveDoctorRunner
            {
                DotnetExitCode = 0,
                CmakeExitCode = 0
            };
            using var output = new StringWriter();
            using var error = new StringWriter();
            var app = new EngineCliApp(output, error, runner);

            int code = app.Run(
            [
                "doctor",
                "--project", tempRoot,
                "--runtime-perf", "artifacts/tests/runtime/missing-perf.json"
            ]);

            Assert.Equal(1, code);
            Assert.Contains("Runtime perf metrics artifact was not found", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_ShouldFailDoctor_WhenRuntimePerfBudgetsExceeded()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareDoctorProject(tempRoot);
            string perfPath = Path.Combine(tempRoot, "artifacts", "tests", "runtime", "perf-metrics.json");
            WriteRuntimePerfMetrics(
                perfPath,
                backend: "native",
                sampleCount: 8,
                averageCaptureCpuMs: 3.2,
                peakCaptureAllocatedBytes: 4096,
                zeroAllocationCapturePath: false,
                releaseRendererBudget: 3,
                releasePhysicsBudget: 3);

            var runner = new SelectiveDoctorRunner
            {
                DotnetExitCode = 0,
                CmakeExitCode = 0
            };
            using var output = new StringWriter();
            using var error = new StringWriter();
            var app = new EngineCliApp(output, error, runner);

            int code = app.Run(
            [
                "doctor",
                "--project", tempRoot,
                "--max-capture-cpu-ms", "2.5",
                "--max-capture-alloc-bytes", "1024",
                "--require-zero-alloc", "true"
            ]);

            Assert.Equal(1, code);
            string errorText = error.ToString();
            Assert.Contains("average capture CPU", errorText, StringComparison.Ordinal);
            Assert.Contains("peak capture allocation", errorText, StringComparison.Ordinal);
            Assert.Contains("not zero-allocation", errorText, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_ShouldPassDoctor_WhenRuntimePerfBudgetsWithinThresholds()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareDoctorProject(tempRoot);
            string perfPath = Path.Combine(tempRoot, "artifacts", "tests", "runtime", "perf-metrics.json");
            WriteRuntimePerfMetrics(
                perfPath,
                backend: "noop",
                sampleCount: 5,
                averageCaptureCpuMs: 0.8,
                peakCaptureAllocatedBytes: 0,
                zeroAllocationCapturePath: true,
                releaseRendererBudget: 3,
                releasePhysicsBudget: 3);

            var runner = new SelectiveDoctorRunner
            {
                DotnetExitCode = 0,
                CmakeExitCode = 0
            };
            using var output = new StringWriter();
            using var error = new StringWriter();
            var app = new EngineCliApp(output, error, runner);

            int code = app.Run(
            [
                "doctor",
                "--project", tempRoot,
                "--max-capture-cpu-ms", "1.2",
                "--max-capture-alloc-bytes", "0",
                "--require-zero-alloc", "true"
            ]);

            Assert.Equal(0, code);
            string outputText = output.ToString();
            Assert.Contains("Runtime perf metrics: backend=noop", outputText, StringComparison.Ordinal);
            Assert.Contains("Doctor checks passed.", outputText, StringComparison.Ordinal);
            Assert.DoesNotContain("Runtime perf metrics artifact was not found", outputText, StringComparison.Ordinal);
            Assert.Equal(string.Empty, error.ToString());
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_ShouldFailDoctor_WhenRuntimeTransportRequiredButSummaryMissing()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareDoctorProject(tempRoot);

            var runner = new SelectiveDoctorRunner
            {
                DotnetExitCode = 0,
                CmakeExitCode = 0
            };
            using var output = new StringWriter();
            using var error = new StringWriter();
            var app = new EngineCliApp(output, error, runner);

            int code = app.Run(
            [
                "doctor",
                "--project", tempRoot,
                "--require-runtime-transport", "true"
            ]);

            Assert.Equal(1, code);
            Assert.Contains("Multiplayer demo summary artifact was not found", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_ShouldFailDoctor_WhenRuntimeTransportSummaryNotSucceeded()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareDoctorProject(tempRoot);
            string summaryPath = Path.Combine(tempRoot, "artifacts", "tests", "net", "multiplayer-demo.json");
            WriteMultiplayerSummary(
                summaryPath,
                enabled: true,
                succeeded: false,
                serverMessagesReceived: 0,
                clientMessagesReceived: 0);

            var runner = new SelectiveDoctorRunner
            {
                DotnetExitCode = 0,
                CmakeExitCode = 0
            };
            using var output = new StringWriter();
            using var error = new StringWriter();
            var app = new EngineCliApp(output, error, runner);

            int code = app.Run(
            [
                "doctor",
                "--project", tempRoot,
                "--require-runtime-transport", "true"
            ]);

            Assert.Equal(1, code);
            Assert.Contains("runtime transport did not succeed", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_ShouldPassDoctor_WhenRuntimeTransportSummarySucceeded()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareDoctorProject(tempRoot);
            string summaryPath = Path.Combine(tempRoot, "artifacts", "tests", "net", "multiplayer-demo.json");
            WriteMultiplayerSummary(
                summaryPath,
                enabled: true,
                succeeded: true,
                serverMessagesReceived: 3,
                clientMessagesReceived: 4);

            var runner = new SelectiveDoctorRunner
            {
                DotnetExitCode = 0,
                CmakeExitCode = 0
            };
            using var output = new StringWriter();
            using var error = new StringWriter();
            var app = new EngineCliApp(output, error, runner);

            int code = app.Run(
            [
                "doctor",
                "--project", tempRoot,
                "--require-runtime-transport", "true"
            ]);

            Assert.Equal(0, code);
            string outputText = output.ToString();
            Assert.Contains("Multiplayer runtime transport: enabled=True, succeeded=True", outputText, StringComparison.Ordinal);
            Assert.Contains("Doctor checks passed.", outputText, StringComparison.Ordinal);
            Assert.Equal(string.Empty, error.ToString());
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_ShouldFailDoctor_WhenMultiplayerSnapshotRequiredButMissing()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareDoctorProject(tempRoot);

            var runner = new SelectiveDoctorRunner
            {
                DotnetExitCode = 0,
                CmakeExitCode = 0
            };
            using var output = new StringWriter();
            using var error = new StringWriter();
            var app = new EngineCliApp(output, error, runner);

            int code = app.Run(
            [
                "doctor",
                "--project", tempRoot,
                "--verify-multiplayer-snapshot", "true"
            ]);

            Assert.Equal(1, code);
            Assert.Contains("Multiplayer snapshot binary artifact was not found", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_ShouldFailDoctor_WhenMultiplayerSnapshotBinaryInvalid()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareDoctorProject(tempRoot);
            string snapshotPath = Path.Combine(tempRoot, "artifacts", "tests", "net", "multiplayer-snapshot.bin");
            Directory.CreateDirectory(Path.GetDirectoryName(snapshotPath)!);
            File.WriteAllBytes(snapshotPath, [1, 2, 3, 4]);

            var runner = new SelectiveDoctorRunner
            {
                DotnetExitCode = 0,
                CmakeExitCode = 0
            };
            using var output = new StringWriter();
            using var error = new StringWriter();
            var app = new EngineCliApp(output, error, runner);

            int code = app.Run(
            [
                "doctor",
                "--project", tempRoot,
                "--verify-multiplayer-snapshot", "true"
            ]);

            Assert.Equal(1, code);
            Assert.Contains("Multiplayer snapshot binary check failed", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_ShouldPassDoctor_WhenMultiplayerSnapshotBinaryValid()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareDoctorProject(tempRoot);
            string snapshotPath = Path.Combine(tempRoot, "artifacts", "tests", "net", "multiplayer-snapshot.bin");
            WriteMultiplayerSnapshotBinary(snapshotPath);

            var runner = new SelectiveDoctorRunner
            {
                DotnetExitCode = 0,
                CmakeExitCode = 0
            };
            using var output = new StringWriter();
            using var error = new StringWriter();
            var app = new EngineCliApp(output, error, runner);

            int code = app.Run(
            [
                "doctor",
                "--project", tempRoot,
                "--verify-multiplayer-snapshot", "true"
            ]);

            Assert.Equal(0, code);
            string outputText = output.ToString();
            Assert.Contains("Multiplayer snapshot binary: tick=", outputText, StringComparison.Ordinal);
            Assert.Contains("Doctor checks passed.", outputText, StringComparison.Ordinal);
            Assert.Equal(string.Empty, error.ToString());
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_ShouldFailDoctor_WhenMultiplayerRpcRequiredButMissing()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareDoctorProject(tempRoot);

            var runner = new SelectiveDoctorRunner
            {
                DotnetExitCode = 0,
                CmakeExitCode = 0
            };
            using var output = new StringWriter();
            using var error = new StringWriter();
            var app = new EngineCliApp(output, error, runner);

            int code = app.Run(
            [
                "doctor",
                "--project", tempRoot,
                "--verify-multiplayer-rpc", "true"
            ]);

            Assert.Equal(1, code);
            Assert.Contains("Multiplayer RPC binary artifact was not found", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_ShouldFailDoctor_WhenMultiplayerRpcBinaryInvalid()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareDoctorProject(tempRoot);
            string rpcPath = Path.Combine(tempRoot, "artifacts", "tests", "net", "multiplayer-rpc.bin");
            Directory.CreateDirectory(Path.GetDirectoryName(rpcPath)!);
            File.WriteAllBytes(rpcPath, [10, 20, 30]);

            var runner = new SelectiveDoctorRunner
            {
                DotnetExitCode = 0,
                CmakeExitCode = 0
            };
            using var output = new StringWriter();
            using var error = new StringWriter();
            var app = new EngineCliApp(output, error, runner);

            int code = app.Run(
            [
                "doctor",
                "--project", tempRoot,
                "--verify-multiplayer-rpc", "true"
            ]);

            Assert.Equal(1, code);
            Assert.Contains("Multiplayer RPC binary check failed", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_ShouldPassDoctor_WhenMultiplayerRpcBinaryValid()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareDoctorProject(tempRoot);
            string rpcPath = Path.Combine(tempRoot, "artifacts", "tests", "net", "multiplayer-rpc.bin");
            WriteMultiplayerRpcBinary(rpcPath);

            var runner = new SelectiveDoctorRunner
            {
                DotnetExitCode = 0,
                CmakeExitCode = 0
            };
            using var output = new StringWriter();
            using var error = new StringWriter();
            var app = new EngineCliApp(output, error, runner);

            int code = app.Run(
            [
                "doctor",
                "--project", tempRoot,
                "--verify-multiplayer-rpc", "true"
            ]);

            Assert.Equal(0, code);
            string outputText = output.ToString();
            Assert.Contains("Multiplayer RPC binary: rpc=", outputText, StringComparison.Ordinal);
            Assert.Contains("Doctor checks passed.", outputText, StringComparison.Ordinal);
            Assert.Equal(string.Empty, error.ToString());
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_ShouldFailDoctor_WhenMultiplayerOrchestrationRequiredButMissing()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareDoctorProject(tempRoot);

            var runner = new SelectiveDoctorRunner
            {
                DotnetExitCode = 0,
                CmakeExitCode = 0
            };
            using var output = new StringWriter();
            using var error = new StringWriter();
            var app = new EngineCliApp(output, error, runner);

            int code = app.Run(
            [
                "doctor",
                "--project", tempRoot,
                "--verify-multiplayer-orchestration", "true"
            ]);

            Assert.Equal(1, code);
            Assert.Contains("Multiplayer orchestration artifact was not found", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_ShouldFailDoctor_WhenMultiplayerOrchestrationInvalid()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareDoctorProject(tempRoot);
            string orchestrationPath = Path.Combine(tempRoot, "artifacts", "runtime-multiplayer-orchestration", "net", "multiplayer-orchestration.json");
            WriteMultiplayerOrchestration(orchestrationPath, allSucceeded: false, requireNativeTransportSuccess: true);

            var runner = new SelectiveDoctorRunner
            {
                DotnetExitCode = 0,
                CmakeExitCode = 0
            };
            using var output = new StringWriter();
            using var error = new StringWriter();
            var app = new EngineCliApp(output, error, runner);

            int code = app.Run(
            [
                "doctor",
                "--project", tempRoot,
                "--verify-multiplayer-orchestration", "true"
            ]);

            Assert.Equal(1, code);
            Assert.Contains("Multiplayer orchestration check failed", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_ShouldPassDoctor_WhenMultiplayerOrchestrationValid()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareDoctorProject(tempRoot);
            string orchestrationPath = Path.Combine(tempRoot, "artifacts", "runtime-multiplayer-orchestration", "net", "multiplayer-orchestration.json");
            WriteMultiplayerOrchestration(orchestrationPath, allSucceeded: true, requireNativeTransportSuccess: true);

            var runner = new SelectiveDoctorRunner
            {
                DotnetExitCode = 0,
                CmakeExitCode = 0
            };
            using var output = new StringWriter();
            using var error = new StringWriter();
            var app = new EngineCliApp(output, error, runner);

            int code = app.Run(
            [
                "doctor",
                "--project", tempRoot,
                "--verify-multiplayer-orchestration", "true"
            ]);

            Assert.Equal(0, code);
            string outputText = output.ToString();
            Assert.Contains("Multiplayer orchestration: nodes=3", outputText, StringComparison.Ordinal);
            Assert.Contains("Doctor checks passed.", outputText, StringComparison.Ordinal);
            Assert.Equal(string.Empty, error.ToString());
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_ShouldFailDoctor_WhenCaptureRgba16FRequiredButMissing()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareDoctorProject(tempRoot);

            var runner = new SelectiveDoctorRunner
            {
                DotnetExitCode = 0,
                CmakeExitCode = 0
            };
            using var output = new StringWriter();
            using var error = new StringWriter();
            var app = new EngineCliApp(output, error, runner);

            int code = app.Run(
            [
                "doctor",
                "--project", tempRoot,
                "--verify-capture-rgba16f", "true"
            ]);

            Assert.Equal(1, code);
            Assert.Contains("Capture RGBA16F binary artifact was not found", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_ShouldFailDoctor_WhenCaptureRgba16FBinaryIsInvalid()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareDoctorProject(tempRoot);
            string capturePath = Path.Combine(tempRoot, "artifacts", "tests", "screenshots", "frame-0001.rgba16f.bin");
            Directory.CreateDirectory(Path.GetDirectoryName(capturePath)!);
            File.WriteAllBytes(capturePath, [1, 2, 3]);

            var runner = new SelectiveDoctorRunner
            {
                DotnetExitCode = 0,
                CmakeExitCode = 0
            };
            using var output = new StringWriter();
            using var error = new StringWriter();
            var app = new EngineCliApp(output, error, runner);

            int code = app.Run(
            [
                "doctor",
                "--project", tempRoot,
                "--verify-capture-rgba16f", "true"
            ]);

            Assert.Equal(1, code);
            Assert.Contains("Capture RGBA16F binary check failed", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_ShouldPassDoctor_WhenCaptureRgba16FBinaryIsValid()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareDoctorProject(tempRoot);
            string capturePath = Path.Combine(tempRoot, "artifacts", "tests", "screenshots", "frame-0001.rgba16f.bin");
            WriteCaptureRgba16FloatBinary(capturePath);
            string exrPath = Path.Combine(tempRoot, "artifacts", "tests", "screenshots", "frame-0001.rgba16f.exr");
            WriteCaptureRgba16FloatExr(exrPath);

            var runner = new SelectiveDoctorRunner
            {
                DotnetExitCode = 0,
                CmakeExitCode = 0
            };
            using var output = new StringWriter();
            using var error = new StringWriter();
            var app = new EngineCliApp(output, error, runner);

            int code = app.Run(
            [
                "doctor",
                "--project", tempRoot,
                "--verify-capture-rgba16f", "true"
            ]);

            Assert.Equal(0, code);
            string outputText = output.ToString();
            Assert.Contains("Capture RGBA16F binary: pixels=", outputText, StringComparison.Ordinal);
            Assert.Contains("Capture RGBA16F EXR: bytes=", outputText, StringComparison.Ordinal);
            Assert.Contains("Doctor checks passed.", outputText, StringComparison.Ordinal);
            Assert.Equal(string.Empty, error.ToString());
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_ShouldFailDoctor_WhenCaptureRgba16FExrMissing()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareDoctorProject(tempRoot);
            string capturePath = Path.Combine(tempRoot, "artifacts", "tests", "screenshots", "frame-0001.rgba16f.bin");
            WriteCaptureRgba16FloatBinary(capturePath);

            var runner = new SelectiveDoctorRunner
            {
                DotnetExitCode = 0,
                CmakeExitCode = 0
            };
            using var output = new StringWriter();
            using var error = new StringWriter();
            var app = new EngineCliApp(output, error, runner);

            int code = app.Run(
            [
                "doctor",
                "--project", tempRoot,
                "--verify-capture-rgba16f", "true"
            ]);

            Assert.Equal(1, code);
            Assert.Contains("Capture RGBA16F EXR artifact was not found", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_ShouldFailDoctor_WhenExplicitCaptureRgba16FExrPathMissing()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareDoctorProject(tempRoot);
            string capturePath = Path.Combine(tempRoot, "artifacts", "tests", "screenshots", "frame-0001.rgba16f.bin");
            WriteCaptureRgba16FloatBinary(capturePath);

            var runner = new SelectiveDoctorRunner
            {
                DotnetExitCode = 0,
                CmakeExitCode = 0
            };
            using var output = new StringWriter();
            using var error = new StringWriter();
            var app = new EngineCliApp(output, error, runner);

            int code = app.Run(
            [
                "doctor",
                "--project", tempRoot,
                "--verify-capture-rgba16f", "true",
                "--capture-rgba16f-exr", "artifacts/tests/screenshots/custom-frame.rgba16f.exr"
            ]);

            Assert.Equal(1, code);
            Assert.Contains("Capture RGBA16F EXR artifact was not found", error.ToString(), StringComparison.Ordinal);
            Assert.Contains("custom-frame.rgba16f.exr", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_ShouldFailDoctor_WhenCaptureRgba16FExrInvalid()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareDoctorProject(tempRoot);
            string capturePath = Path.Combine(tempRoot, "artifacts", "tests", "screenshots", "frame-0001.rgba16f.bin");
            WriteCaptureRgba16FloatBinary(capturePath);
            string exrPath = Path.Combine(tempRoot, "artifacts", "tests", "screenshots", "frame-0001.rgba16f.exr");
            Directory.CreateDirectory(Path.GetDirectoryName(exrPath)!);
            File.WriteAllBytes(exrPath, [1, 2, 3, 4, 5, 6, 7, 8]);

            var runner = new SelectiveDoctorRunner
            {
                DotnetExitCode = 0,
                CmakeExitCode = 0
            };
            using var output = new StringWriter();
            using var error = new StringWriter();
            var app = new EngineCliApp(output, error, runner);

            int code = app.Run(
            [
                "doctor",
                "--project", tempRoot,
                "--verify-capture-rgba16f", "true"
            ]);

            Assert.Equal(1, code);
            Assert.Contains("Capture RGBA16F EXR check failed", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_ShouldFailDoctor_WhenRenderStatsRequiredButMissing()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareDoctorProject(tempRoot);

            var runner = new SelectiveDoctorRunner
            {
                DotnetExitCode = 0,
                CmakeExitCode = 0
            };
            using var output = new StringWriter();
            using var error = new StringWriter();
            var app = new EngineCliApp(output, error, runner);

            int code = app.Run(
            [
                "doctor",
                "--project", tempRoot,
                "--verify-render-stats", "true"
            ]);

            Assert.Equal(1, code);
            Assert.Contains("Render stats artifact was not found", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_ShouldFailDoctor_WhenRenderStatsInvalid()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareDoctorProject(tempRoot);
            string statsPath = Path.Combine(tempRoot, "artifacts", "tests", "render", "frame-stats.json");
            WriteRenderStats(statsPath, drawItemCount: 0, triangleCount: 0UL, uploadBytes: 0UL, gpuMemoryBytes: 0UL, presentCount: 0UL);

            var runner = new SelectiveDoctorRunner
            {
                DotnetExitCode = 0,
                CmakeExitCode = 0
            };
            using var output = new StringWriter();
            using var error = new StringWriter();
            var app = new EngineCliApp(output, error, runner);

            int code = app.Run(
            [
                "doctor",
                "--project", tempRoot,
                "--verify-render-stats", "true"
            ]);

            Assert.Equal(1, code);
            Assert.Contains("Render stats check failed", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_ShouldPassDoctor_WhenRenderStatsValid()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareDoctorProject(tempRoot);
            string statsPath = Path.Combine(tempRoot, "artifacts", "tests", "render", "frame-stats.json");
            WriteRenderStats(statsPath, drawItemCount: 2, triangleCount: 12UL, uploadBytes: 128UL, gpuMemoryBytes: 256UL, presentCount: 1UL);

            var runner = new SelectiveDoctorRunner
            {
                DotnetExitCode = 0,
                CmakeExitCode = 0
            };
            using var output = new StringWriter();
            using var error = new StringWriter();
            var app = new EngineCliApp(output, error, runner);

            int code = app.Run(
            [
                "doctor",
                "--project", tempRoot,
                "--verify-render-stats", "true"
            ]);

            Assert.Equal(0, code);
            string outputText = output.ToString();
            Assert.Contains("Render stats: drawItems=", outputText, StringComparison.Ordinal);
            Assert.Contains("Doctor checks passed.", outputText, StringComparison.Ordinal);
            Assert.Equal(string.Empty, error.ToString());
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_ShouldFailDoctor_WhenTestHostConfigRequiredButMissing()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareDoctorProject(tempRoot);

            var runner = new SelectiveDoctorRunner
            {
                DotnetExitCode = 0,
                CmakeExitCode = 0
            };
            using var output = new StringWriter();
            using var error = new StringWriter();
            var app = new EngineCliApp(output, error, runner);

            int code = app.Run(
            [
                "doctor",
                "--project", tempRoot,
                "--verify-test-host-config", "true"
            ]);

            Assert.Equal(1, code);
            Assert.Contains("Test host config artifact was not found", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_ShouldFailDoctor_WhenTestHostConfigInvalid()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareDoctorProject(tempRoot);
            string testHostConfigPath = Path.Combine(tempRoot, "artifacts", "tests", "runtime", "test-host.json");
            WriteTestHostConfig(testHostConfigPath, mode: "invalid", fixedDeltaSeconds: 0.0);

            var runner = new SelectiveDoctorRunner
            {
                DotnetExitCode = 0,
                CmakeExitCode = 0
            };
            using var output = new StringWriter();
            using var error = new StringWriter();
            var app = new EngineCliApp(output, error, runner);

            int code = app.Run(
            [
                "doctor",
                "--project", tempRoot,
                "--verify-test-host-config", "true"
            ]);

            Assert.Equal(1, code);
            Assert.Contains("Test host config check failed", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_ShouldPassDoctor_WhenTestHostConfigValid()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareDoctorProject(tempRoot);
            string testHostConfigPath = Path.Combine(tempRoot, "artifacts", "tests", "runtime", "test-host.json");
            WriteTestHostConfig(testHostConfigPath, mode: "hidden-window", fixedDeltaSeconds: 0.0166667);

            var runner = new SelectiveDoctorRunner
            {
                DotnetExitCode = 0,
                CmakeExitCode = 0
            };
            using var output = new StringWriter();
            using var error = new StringWriter();
            var app = new EngineCliApp(output, error, runner);

            int code = app.Run(
            [
                "doctor",
                "--project", tempRoot,
                "--verify-test-host-config", "true"
            ]);

            Assert.Equal(0, code);
            string outputText = output.ToString();
            Assert.Contains("Test host config: mode=hidden-window", outputText, StringComparison.Ordinal);
            Assert.Contains("Doctor checks passed.", outputText, StringComparison.Ordinal);
            Assert.Equal(string.Empty, error.ToString());
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_ShouldFailDoctor_WhenNetProfileLogRequiredButMissing()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareDoctorProject(tempRoot);

            var runner = new SelectiveDoctorRunner
            {
                DotnetExitCode = 0,
                CmakeExitCode = 0
            };
            using var output = new StringWriter();
            using var error = new StringWriter();
            var app = new EngineCliApp(output, error, runner);

            int code = app.Run(
            [
                "doctor",
                "--project", tempRoot,
                "--verify-net-profile-log", "true"
            ]);

            Assert.Equal(1, code);
            Assert.Contains("Net profile log artifact was not found", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_ShouldFailDoctor_WhenNetProfileLogInvalid()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareDoctorProject(tempRoot);
            string profilePath = Path.Combine(tempRoot, "artifacts", "tests", "net", "multiplayer-profile.log");
            WriteNetProfileLog(profilePath, ["seed=1", "runtime-transport serverMessages=0 clientMessages=0"]);

            var runner = new SelectiveDoctorRunner
            {
                DotnetExitCode = 0,
                CmakeExitCode = 0
            };
            using var output = new StringWriter();
            using var error = new StringWriter();
            var app = new EngineCliApp(output, error, runner);

            int code = app.Run(
            [
                "doctor",
                "--project", tempRoot,
                "--verify-net-profile-log", "true"
            ]);

            Assert.Equal(1, code);
            Assert.Contains("Net profile log check failed", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_ShouldPassDoctor_WhenNetProfileLogValid()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareDoctorProject(tempRoot);
            string profilePath = Path.Combine(tempRoot, "artifacts", "tests", "net", "multiplayer-profile.log");
            WriteNetProfileLog(
                profilePath,
                [
                    "seed=1 proceduralSeed=1 fixedDt=0.016667 tickRateHz=60 simulatedTicks=3 synchronized=True runtimeTransportEnabled=True runtimeTransportSucceeded=True",
                    "runtime-transport serverMessages=2 clientMessages=2",
                    "server bytesSent=128 bytesReceived=64 messagesSent=2 messagesReceived=2 dropped=0 rttMs=1.000 lossPercent=0.000 sendKbps=1.000 receiveKbps=1.000 peakSendKbps=1.000 peakReceiveKbps=1.000",
                    "client-1 bytesSent=64 bytesReceived=128 messagesSent=2 messagesReceived=2 dropped=0 rttMs=1.000 lossPercent=0.000 sendKbps=1.000 receiveKbps=1.000 peakSendKbps=1.000 peakReceiveKbps=1.000"
                ]);

            var runner = new SelectiveDoctorRunner
            {
                DotnetExitCode = 0,
                CmakeExitCode = 0
            };
            using var output = new StringWriter();
            using var error = new StringWriter();
            var app = new EngineCliApp(output, error, runner);

            int code = app.Run(
            [
                "doctor",
                "--project", tempRoot,
                "--verify-net-profile-log", "true"
            ]);

            Assert.Equal(0, code);
            string outputText = output.ToString();
            Assert.Contains("Net profile log: lines=", outputText, StringComparison.Ordinal);
            Assert.Contains("Doctor checks passed.", outputText, StringComparison.Ordinal);
            Assert.Equal(string.Empty, error.ToString());
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_ShouldFailDoctor_WhenReplayRecordingRequiredButMissing()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareDoctorProject(tempRoot);

            var runner = new SelectiveDoctorRunner
            {
                DotnetExitCode = 0,
                CmakeExitCode = 0
            };
            using var output = new StringWriter();
            using var error = new StringWriter();
            var app = new EngineCliApp(output, error, runner);

            int code = app.Run(
            [
                "doctor",
                "--project", tempRoot,
                "--verify-replay-recording", "true"
            ]);

            Assert.Equal(1, code);
            Assert.Contains("Replay recording artifact was not found", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_ShouldFailDoctor_WhenReplayRecordingInvalid()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareDoctorProject(tempRoot);
            string replayPath = Path.Combine(tempRoot, "artifacts", "tests", "replay", "recording.json");
            WriteReplayRecording(
                replayPath,
                """
                {
                  "seed": 1,
                  "fixedDeltaSeconds": 0,
                  "frames": [],
                  "networkEvents": ["capture.frame=1"],
                  "timedNetworkEvents": []
                }
                """);

            var runner = new SelectiveDoctorRunner
            {
                DotnetExitCode = 0,
                CmakeExitCode = 0
            };
            using var output = new StringWriter();
            using var error = new StringWriter();
            var app = new EngineCliApp(output, error, runner);

            int code = app.Run(
            [
                "doctor",
                "--project", tempRoot,
                "--verify-replay-recording", "true"
            ]);

            Assert.Equal(1, code);
            Assert.Contains("Replay recording check failed", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_ShouldPassDoctor_WhenReplayRecordingValid()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareDoctorProject(tempRoot);
            string replayPath = Path.Combine(tempRoot, "artifacts", "tests", "replay", "recording.json");
            WriteReplayRecording(
                replayPath,
                """
                {
                  "seed": 9001,
                  "fixedDeltaSeconds": 0.0166667,
                  "frames": [
                    { "tick": 0, "buttons": 0, "mouseX": 0.0, "mouseY": 0.0 }
                  ],
                  "networkEvents": [
                    "capture.frame=1",
                    "net.profile=net/multiplayer-profile.log"
                  ],
                  "timedNetworkEvents": [
                    { "tick": 0, "event": "capture.frame=1" }
                  ]
                }
                """);

            var runner = new SelectiveDoctorRunner
            {
                DotnetExitCode = 0,
                CmakeExitCode = 0
            };
            using var output = new StringWriter();
            using var error = new StringWriter();
            var app = new EngineCliApp(output, error, runner);

            int code = app.Run(
            [
                "doctor",
                "--project", tempRoot,
                "--verify-replay-recording", "true"
            ]);

            Assert.Equal(0, code);
            string outputText = output.ToString();
            Assert.Contains("Replay recording: frames=", outputText, StringComparison.Ordinal);
            Assert.Contains("Doctor checks passed.", outputText, StringComparison.Ordinal);
            Assert.Equal(string.Empty, error.ToString());
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_ShouldFailDoctor_WhenArtifactsManifestRequiredButMissing()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareDoctorProject(tempRoot);

            var runner = new SelectiveDoctorRunner
            {
                DotnetExitCode = 0,
                CmakeExitCode = 0
            };
            using var output = new StringWriter();
            using var error = new StringWriter();
            var app = new EngineCliApp(output, error, runner);

            int code = app.Run(
            [
                "doctor",
                "--project", tempRoot,
                "--verify-artifacts-manifest", "true"
            ]);

            Assert.Equal(1, code);
            Assert.Contains("Artifacts manifest was not found", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_ShouldFailDoctor_WhenArtifactsManifestInvalid()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareDoctorProject(tempRoot);
            string manifestPath = Path.Combine(tempRoot, "artifacts", "tests", "manifest.json");
            WriteArtifactsManifest(manifestPath, ["screenshot", "runtime-perf-metrics"]);

            var runner = new SelectiveDoctorRunner
            {
                DotnetExitCode = 0,
                CmakeExitCode = 0
            };
            using var output = new StringWriter();
            using var error = new StringWriter();
            var app = new EngineCliApp(output, error, runner);

            int code = app.Run(
            [
                "doctor",
                "--project", tempRoot,
                "--verify-artifacts-manifest", "true"
            ]);

            Assert.Equal(1, code);
            Assert.Contains("Artifacts manifest check failed", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_ShouldPassDoctor_WhenArtifactsManifestValid()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareDoctorProject(tempRoot);
            string manifestPath = Path.Combine(tempRoot, "artifacts", "tests", "manifest.json");
            WriteArtifactsManifest(
                manifestPath,
                [
                    "screenshot",
                    "screenshot-buffer",
                    "screenshot-buffer-rgba16f",
                    "screenshot-buffer-rgba16f-exr",
                    "multiplayer-demo",
                    "net-profile-log",
                    "multiplayer-snapshot-bin",
                    "multiplayer-rpc-bin",
                    "render-stats-log",
                    "test-host-config",
                    "runtime-perf-metrics",
                    "replay"
                ]);

            var runner = new SelectiveDoctorRunner
            {
                DotnetExitCode = 0,
                CmakeExitCode = 0
            };
            using var output = new StringWriter();
            using var error = new StringWriter();
            var app = new EngineCliApp(output, error, runner);

            int code = app.Run(
            [
                "doctor",
                "--project", tempRoot,
                "--verify-artifacts-manifest", "true"
            ]);

            Assert.Equal(0, code);
            string outputText = output.ToString();
            Assert.Contains("Artifacts manifest: entries=", outputText, StringComparison.Ordinal);
            Assert.Contains("Doctor checks passed.", outputText, StringComparison.Ordinal);
            Assert.Equal(string.Empty, error.ToString());
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_ShouldFailDoctor_WhenReleaseProofRequiredButMissing()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareDoctorProject(tempRoot);

            var runner = new SelectiveDoctorRunner
            {
                DotnetExitCode = 0,
                CmakeExitCode = 0
            };
            using var output = new StringWriter();
            using var error = new StringWriter();
            var app = new EngineCliApp(output, error, runner);

            int code = app.Run(
            [
                "doctor",
                "--project", tempRoot,
                "--verify-release-proof", "true"
            ]);

            Assert.Equal(1, code);
            Assert.Contains("NFR release proof artifact was not found", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_ShouldFailDoctor_WhenReleaseProofInvalid()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareDoctorProject(tempRoot);
            string releaseProofPath = Path.Combine(tempRoot, "artifacts", "nfr", "release-proof.json");
            WriteReleaseProof(releaseProofPath, isSuccess: false, allChecksPassing: false);

            var runner = new SelectiveDoctorRunner
            {
                DotnetExitCode = 0,
                CmakeExitCode = 0
            };
            using var output = new StringWriter();
            using var error = new StringWriter();
            var app = new EngineCliApp(output, error, runner);

            int code = app.Run(
            [
                "doctor",
                "--project", tempRoot,
                "--verify-release-proof", "true"
            ]);

            Assert.Equal(1, code);
            Assert.Contains("NFR release proof check failed", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_ShouldPassDoctor_WhenReleaseProofValid()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareDoctorProject(tempRoot);
            string releaseProofPath = Path.Combine(tempRoot, "artifacts", "nfr", "release-proof.json");
            WriteReleaseProof(releaseProofPath, isSuccess: true, allChecksPassing: true);

            var runner = new SelectiveDoctorRunner
            {
                DotnetExitCode = 0,
                CmakeExitCode = 0
            };
            using var output = new StringWriter();
            using var error = new StringWriter();
            var app = new EngineCliApp(output, error, runner);

            int code = app.Run(
            [
                "doctor",
                "--project", tempRoot,
                "--verify-release-proof", "true"
            ]);

            Assert.Equal(0, code);
            string outputText = output.ToString();
            Assert.Contains("NFR release proof: success=True", outputText, StringComparison.Ordinal);
            Assert.Contains("Doctor checks passed.", outputText, StringComparison.Ordinal);
            Assert.Equal(string.Empty, error.ToString());
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static void PrepareDoctorProject(string rootPath)
    {
        Directory.CreateDirectory(Path.Combine(rootPath, "assets"));
        Directory.CreateDirectory(Path.Combine(rootPath, "src"));
        File.WriteAllText(Path.Combine(rootPath, "project.json"), "{}");
        File.WriteAllText(
            Path.Combine(rootPath, "assets", "manifest.json"),
            """
            {
              "version": 1,
              "assets": [
                {
                  "path": "example.txt",
                  "kind": "text"
                }
              ]
            }
            """);
    }

    private static void WriteRuntimePerfMetrics(
        string filePath,
        string backend,
        int sampleCount,
        double averageCaptureCpuMs,
        long peakCaptureAllocatedBytes,
        bool zeroAllocationCapturePath,
        int releaseRendererBudget,
        int releasePhysicsBudget)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(
            filePath,
            $$"""
            {
              "backend": "{{backend}}",
              "captureSampleCount": {{sampleCount}},
              "averageCaptureCpuMs": {{averageCaptureCpuMs.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)}},
              "peakCaptureCpuMs": {{averageCaptureCpuMs.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)}},
              "averageCaptureAllocatedBytes": {{peakCaptureAllocatedBytes}},
              "peakCaptureAllocatedBytes": {{peakCaptureAllocatedBytes}},
              "totalCaptureAllocatedBytes": {{peakCaptureAllocatedBytes}},
              "zeroAllocationCapturePath": {{zeroAllocationCapturePath.ToString().ToLowerInvariant()}},
              "releaseRendererInteropBudgetPerFrame": {{releaseRendererBudget}},
              "releasePhysicsInteropBudgetPerTick": {{releasePhysicsBudget}}
            }
            """);
    }

    private static void WriteMultiplayerSummary(
        string filePath,
        bool enabled,
        bool succeeded,
        int serverMessagesReceived,
        int clientMessagesReceived)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(
            filePath,
            $$"""
            {
              "seed": 1337,
              "runtimeTransport": {
                "enabled": {{enabled.ToString().ToLowerInvariant()}},
                "succeeded": {{succeeded.ToString().ToLowerInvariant()}},
                "serverMessagesReceived": {{serverMessagesReceived}},
                "clientMessagesReceived": {{clientMessagesReceived}}
              }
            }
            """);
    }

    private static void WriteMultiplayerSnapshotBinary(string filePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        var snapshot = new NetSnapshot(
            tick: 7,
            entities:
            [
                new NetEntityState(
                    entityId: 1u,
                    ownerClientId: 10u,
                    proceduralSeed: 42UL,
                    assetKey: "proc/chunk/1/ABCDEF",
                    components: [new NetComponentState("transform", [1, 2, 3])])
            ]);

        byte[] payload = NetSnapshotBinaryCodec.Encode(snapshot);
        File.WriteAllBytes(filePath, payload);
    }

    private static void WriteMultiplayerRpcBinary(string filePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        var message = new NetRpcMessage(
            entityId: 1u,
            rpcName: "proc.sync.chunk",
            payload: [1, 2, 3, 4],
            channel: NetworkChannel.ReliableOrdered,
            targetClientId: 10u);

        byte[] payload = NetRpcBinaryCodec.Encode(message);
        File.WriteAllBytes(filePath, payload);
    }

    private static void WriteMultiplayerOrchestration(string filePath, bool allSucceeded, bool requireNativeTransportSuccess)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        string lowerAllSucceeded = allSucceeded.ToString().ToLowerInvariant();
        string lowerRequireNativeTransport = requireNativeTransportSuccess.ToString().ToLowerInvariant();
        File.WriteAllText(
            filePath,
            $$"""
            {
              "generatedAtUtc": "2026-01-01T00:00:00Z",
              "configuration": "Release",
              "fixedDeltaSeconds": 0.0166667,
              "requireNativeTransportSuccess": {{lowerRequireNativeTransport}},
              "nodes": [
                {
                  "role": "server",
                  "seed": 9001,
                  "exitCode": 0,
                  "summaryPath": "server/net/multiplayer-demo.json",
                  "runtimeTransportEnabled": true,
                  "runtimeTransportSucceeded": true,
                  "serverMessagesReceived": 3,
                  "clientMessagesReceived": 4
                },
                {
                  "role": "client-1",
                  "seed": 9002,
                  "exitCode": 0,
                  "summaryPath": "client-1/net/multiplayer-demo.json",
                  "runtimeTransportEnabled": true,
                  "runtimeTransportSucceeded": true,
                  "serverMessagesReceived": 3,
                  "clientMessagesReceived": 4
                },
                {
                  "role": "client-2",
                  "seed": 9003,
                  "exitCode": 0,
                  "summaryPath": "client-2/net/multiplayer-demo.json",
                  "runtimeTransportEnabled": true,
                  "runtimeTransportSucceeded": true,
                  "serverMessagesReceived": 3,
                  "clientMessagesReceived": 4
                }
              ],
              "allSucceeded": {{lowerAllSucceeded}}
            }
            """);
    }

    private static void WriteCaptureRgba16FloatBinary(string filePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllBytes(filePath, [1, 0, 2, 0, 3, 0, 4, 0]);
    }

    private static void WriteCaptureRgba16FloatExr(string filePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var payload = new byte[8];
        BitConverter.GetBytes(20000630u).CopyTo(payload, 0);
        BitConverter.GetBytes(2u).CopyTo(payload, 4);
        File.WriteAllBytes(filePath, payload);
    }

    private static void WriteRenderStats(
        string filePath,
        int drawItemCount,
        ulong triangleCount,
        ulong uploadBytes,
        ulong gpuMemoryBytes,
        ulong presentCount)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(
            filePath,
            $$"""
            {
              "drawItemCount": {{drawItemCount}},
              "uiItemCount": 0,
              "triangleCount": {{triangleCount}},
              "uploadBytes": {{uploadBytes}},
              "gpuMemoryBytes": {{gpuMemoryBytes}},
              "presentCount": {{presentCount}}
            }
            """);
    }

    private static void WriteTestHostConfig(string filePath, string mode, double fixedDeltaSeconds)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(
            filePath,
            $$"""
            {
              "mode": "{{mode}}",
              "fixedDeltaSeconds": {{fixedDeltaSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}}
            }
            """);
    }

    private static void WriteNetProfileLog(string filePath, IReadOnlyList<string> lines)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllLines(filePath, lines);
    }

    private static void WriteReplayRecording(string filePath, string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, json);
    }

    private static void WriteArtifactsManifest(string filePath, IReadOnlyList<string> kinds)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        IEnumerable<string> entries = kinds.Select(
            static kind => $"{{ \"kind\": \"{kind}\", \"relativePath\": \"x\", \"description\": \"d\" }}");
        string artifactsJson = string.Join(",\n    ", entries);
        string json =
            $$"""
            {
              "generatedAtUtc": "2026-01-01T00:00:00Z",
              "artifacts": [
                {{artifactsJson}}
              ]
            }
            """;
        File.WriteAllText(filePath, json);
    }

    private static void WriteReleaseProof(string filePath, bool isSuccess, bool allChecksPassing)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        string lowerIsSuccess = isSuccess.ToString().ToLowerInvariant();
        string lowerChecks = allChecksPassing.ToString().ToLowerInvariant();
        File.WriteAllText(
            filePath,
            $$"""
            {
              "generatedAtUtc": "2026-01-01T00:00:00Z",
              "configuration": "Release",
              "runtimeProjectPath": "src/Game.Runtime/Game.Runtime.csproj",
              "build": { "succeeded": true, "exitCode": 0 },
              "tests": { "succeeded": true, "exitCode": 0 },
              "checks": {
                "runtimePerfMetrics": {{lowerChecks}},
                "renderStats": {{lowerChecks}},
                "multiplayerSummary": {{lowerChecks}},
                "netProfileLog": {{lowerChecks}},
                "multiplayerSnapshotBinary": {{lowerChecks}},
                "multiplayerRpcBinary": {{lowerChecks}},
                "replayRecording": {{lowerChecks}},
                "artifactsManifest": {{lowerChecks}},
                "releaseInteropBudgetsMatch": {{lowerChecks}},
                "allArtifactsPresent": {{lowerChecks}}
              },
              "isSuccess": {{lowerIsSuccess}}
            }
            """);
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"engine-cli-doctor-tool-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class SelectiveDoctorRunner : IExternalCommandRunner
    {
        public List<DoctorCommandInvocation> Invocations { get; } = [];

        public int DotnetExitCode { get; init; } = 0;

        public int CmakeExitCode { get; init; } = 0;

        public bool ThrowOnDotnet { get; init; }

        public bool ThrowOnCmake { get; init; }

        public int Run(
            string executablePath,
            IReadOnlyList<string> arguments,
            string workingDirectory,
            TextWriter stdout,
            TextWriter stderr)
        {
            Invocations.Add(new DoctorCommandInvocation(executablePath, arguments.ToArray(), workingDirectory));

            if (string.Equals(executablePath, "dotnet", StringComparison.OrdinalIgnoreCase))
            {
                if (ThrowOnDotnet)
                {
                    throw new InvalidOperationException("Failed to start process 'dotnet'.");
                }

                return DotnetExitCode;
            }

            if (string.Equals(executablePath, "cmake", StringComparison.OrdinalIgnoreCase))
            {
                if (ThrowOnCmake)
                {
                    throw new InvalidOperationException("Failed to start process 'cmake'.");
                }

                return CmakeExitCode;
            }

            return 0;
        }
    }

    private sealed record DoctorCommandInvocation(string ExecutablePath, string[] Arguments, string WorkingDirectory);
}
