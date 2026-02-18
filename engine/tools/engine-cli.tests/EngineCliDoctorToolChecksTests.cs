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
