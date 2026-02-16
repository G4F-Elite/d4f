using Engine.Cli;

namespace Engine.Cli.Tests;

public sealed class EngineCliAppValidationTests
{
    [Fact]
    public void Run_ShouldFailBuild_WhenProjectDirectoryMissing()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        EngineCliApp app = new(output, error);

        int code = app.Run(["build", "--project", "missing-project"]);

        Assert.Equal(1, code);
        Assert.Contains("Project directory does not exist", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Run_ShouldFailRun_WhenBuildArtifactMissing()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(tempRoot, "src"));

            using var output = new StringWriter();
            using var error = new StringWriter();
            EngineCliApp app = new(output, error);

            int code = app.Run(["run", "--project", tempRoot]);

            Assert.Equal(1, code);
            Assert.Contains("Build artifact was not found", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public void Run_ShouldFailPack_WhenManifestMissing()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            using var output = new StringWriter();
            using var error = new StringWriter();
            EngineCliApp app = new(output, error);

            int code = app.Run(["pack", "--project", tempRoot, "--manifest", "assets/manifest.json"]);

            Assert.Equal(1, code);
            Assert.Contains("Manifest file was not found", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public void Run_ShouldCreateProjectStructure_WhenInitValid()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            using var output = new StringWriter();
            using var error = new StringWriter();
            EngineCliApp app = new(output, error);

            int code = app.Run(["init", "--name", "DemoGame", "--output", tempRoot]);

            Assert.Equal(0, code);
            Assert.True(Directory.Exists(Path.Combine(tempRoot, "DemoGame", "src")));
            Assert.True(Directory.Exists(Path.Combine(tempRoot, "DemoGame", "assets")));
            Assert.True(Directory.Exists(Path.Combine(tempRoot, "DemoGame", "tests")));
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public void Run_ShouldPackCompiledAssetsAndManifest()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            string assetsDirectory = Path.Combine(tempRoot, "assets");
            Directory.CreateDirectory(assetsDirectory);
            File.WriteAllText(Path.Combine(assetsDirectory, "example.txt"), "content");
            File.WriteAllText(
                Path.Combine(assetsDirectory, "manifest.json"),
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

            using var output = new StringWriter();
            using var error = new StringWriter();
            EngineCliApp app = new(output, error);

            int code = app.Run(["pack", "--project", tempRoot, "--manifest", "assets/manifest.json"]);

            Assert.Equal(0, code);
            Assert.True(File.Exists(Path.Combine(tempRoot, "dist", "content.pak")));
            Assert.True(File.Exists(Path.Combine(tempRoot, "dist", "compiled", "text", "example.txt.bin")));
            Assert.True(File.Exists(Path.Combine(tempRoot, "dist", "compiled.manifest.bin")));
            Assert.True(File.Exists(Path.Combine(tempRoot, "dist", "package", "Content", "Game.pak")));
            Assert.True(File.Exists(Path.Combine(tempRoot, "dist", "package", "Content", "compiled.manifest.bin")));
            Assert.True(File.Exists(Path.Combine(tempRoot, "dist", "package", "Content", "compiled", "text", "example.txt.bin")));
            Assert.True(File.Exists(Path.Combine(tempRoot, "dist", "package", "config", "runtime.json")));
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"engine-cli-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
