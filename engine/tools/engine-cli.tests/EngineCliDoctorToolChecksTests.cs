using Engine.Cli;

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
