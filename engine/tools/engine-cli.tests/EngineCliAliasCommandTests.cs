using Engine.Cli;

namespace Engine.Cli.Tests;

public sealed class EngineCliAliasCommandTests
{
    [Fact]
    public void Run_NewCommand_ShouldAcceptPositionalName()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            using var output = new StringWriter();
            using var error = new StringWriter();
            EngineCliApp app = new(output, error);

            int code = app.Run(["new", "AliasGame", "--output", tempRoot]);

            Assert.Equal(0, code);
            Assert.True(Directory.Exists(Path.Combine(tempRoot, "AliasGame")));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_BuildCommand_ShouldAcceptShortConfigurationOption()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            string runtimeDirectory = Path.Combine(tempRoot, "src", "AliasRuntime");
            Directory.CreateDirectory(runtimeDirectory);
            string runtimeProjectPath = Path.Combine(runtimeDirectory, "AliasRuntime.csproj");
            File.WriteAllText(
                runtimeProjectPath,
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net9.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);
            var runner = new RecordingCommandRunner();
            using var output = new StringWriter();
            using var error = new StringWriter();
            EngineCliApp app = new(output, error, runner);

            int code = app.Run(["build", "-p", tempRoot, "-c", "Release"]);

            Assert.Equal(0, code);
            Assert.Single(runner.Invocations);
            CommandInvocation invocation = runner.Invocations[0];
            Assert.Equal("dotnet", invocation.ExecutablePath);
            Assert.Contains("build", invocation.Arguments);
            Assert.Contains(runtimeProjectPath, invocation.Arguments);
            Assert.Contains("Release", invocation.Arguments);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"engine-cli-alias-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class RecordingCommandRunner : IExternalCommandRunner
    {
        public List<CommandInvocation> Invocations { get; } = [];

        public int Run(
            string executablePath,
            IReadOnlyList<string> arguments,
            string workingDirectory,
            TextWriter stdout,
            TextWriter stderr)
        {
            Invocations.Add(new CommandInvocation(executablePath, arguments.ToArray(), workingDirectory));
            return 0;
        }
    }

    private sealed record CommandInvocation(string ExecutablePath, string[] Arguments, string WorkingDirectory);
}
