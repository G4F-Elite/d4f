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
            Directory.CreateDirectory(Path.Combine(tempRoot, "src"));
            using var output = new StringWriter();
            using var error = new StringWriter();
            EngineCliApp app = new(output, error);

            int code = app.Run(["build", "-p", tempRoot, "-c", "Release"]);

            Assert.Equal(0, code);
            Assert.True(File.Exists(Path.Combine(tempRoot, "build", "Release", "game.bin")));
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
}
