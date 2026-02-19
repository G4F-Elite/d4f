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
            Assert.Contains("Doctor checks passed.", outputText, StringComparison.Ordinal);
            Assert.Equal(string.Empty, error.ToString());
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

    private static void WriteCaptureRgba16FloatBinary(string filePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllBytes(filePath, [1, 0, 2, 0, 3, 0, 4, 0]);
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
