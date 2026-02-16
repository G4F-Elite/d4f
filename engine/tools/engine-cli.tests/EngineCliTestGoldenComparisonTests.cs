using Engine.Cli;

namespace Engine.Cli.Tests;

public sealed class EngineCliTestGoldenComparisonTests
{
    [Fact]
    public void Run_TestCommand_ShouldPassGoldenComparison_WhenBuffersMatch()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            var runner = new RecordingCommandRunner();
            using var output = new StringWriter();
            using var error = new StringWriter();
            EngineCliApp app = new(output, error, runner);

            int firstRunCode = app.Run(["test", "--project", tempRoot, "--out", "artifacts/tests-source"]);
            Assert.Equal(0, firstRunCode);

            string sourceArtifacts = Path.Combine(tempRoot, "artifacts", "tests-source");
            string goldenDirectory = Path.Combine(tempRoot, "goldens");
            CopyDirectory(sourceArtifacts, goldenDirectory);

            int secondRunCode = app.Run(
            [
                "test",
                "--project", tempRoot,
                "--out", "artifacts/tests-target",
                "--golden", "goldens",
                "--comparison", "tolerant"
            ]);

            Assert.Equal(0, secondRunCode);
            Assert.Contains("Golden comparison passed", output.ToString(), StringComparison.Ordinal);
            Assert.Equal(2, runner.Invocations.Count);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_TestCommand_ShouldFailGoldenComparison_WhenGoldenBufferMissing()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(tempRoot, "goldens"));
            var runner = new RecordingCommandRunner();
            using var output = new StringWriter();
            using var error = new StringWriter();
            EngineCliApp app = new(output, error, runner);

            int code = app.Run(
            [
                "test",
                "--project", tempRoot,
                "--out", "artifacts/tests",
                "--golden", "goldens",
                "--comparison", "pixel"
            ]);

            Assert.Equal(1, code);
            Assert.Contains("Golden buffer is missing", error.ToString(), StringComparison.Ordinal);
            Assert.Contains("Golden comparison failed", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_TestCommand_ShouldFailPixelComparison_WhenGoldenBufferDiffers()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            var runner = new RecordingCommandRunner();
            using var output = new StringWriter();
            using var error = new StringWriter();
            EngineCliApp app = new(output, error, runner);

            int firstRunCode = app.Run(["test", "--project", tempRoot, "--out", "artifacts/tests-source"]);
            Assert.Equal(0, firstRunCode);

            string sourceArtifacts = Path.Combine(tempRoot, "artifacts", "tests-source");
            string goldenDirectory = Path.Combine(tempRoot, "goldens");
            CopyDirectory(sourceArtifacts, goldenDirectory);

            string goldenBufferPath = Path.Combine(goldenDirectory, "screenshots", "frame-0001.rgba8.bin");
            byte[] bytes = File.ReadAllBytes(goldenBufferPath);
            bytes[^1] ^= 0xFF;
            File.WriteAllBytes(goldenBufferPath, bytes);

            int secondRunCode = app.Run(
            [
                "test",
                "--project", tempRoot,
                "--out", "artifacts/tests-target",
                "--golden", "goldens",
                "--comparison", "pixel"
            ]);

            Assert.Equal(1, secondRunCode);
            Assert.Contains("Golden compare failed", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);
        foreach (string filePath in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(sourceDirectory, filePath);
            string target = Path.Combine(destinationDirectory, relative);
            string? targetParent = Path.GetDirectoryName(target);
            if (!string.IsNullOrWhiteSpace(targetParent))
            {
                Directory.CreateDirectory(targetParent);
            }

            File.Copy(filePath, target, overwrite: true);
        }
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"engine-cli-golden-tests-{Guid.NewGuid():N}");
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
