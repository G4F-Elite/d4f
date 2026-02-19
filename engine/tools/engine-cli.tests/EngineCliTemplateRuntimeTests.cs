using Engine.Cli;
using System.Xml.Linq;

namespace Engine.Cli.Tests;

public sealed class EngineCliTemplateRuntimeTests
{
    [Fact]
    public void NewCommand_ShouldGenerateRuntimeTemplateWithResolvedEngineReferences()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            using var output = new StringWriter();
            using var error = new StringWriter();
            EngineCliApp app = new(output, error);

            int code = app.Run(["new", "--name", "DemoGame", "--output", tempRoot]);

            Assert.Equal(0, code);
            string runtimeDirectory = Path.Combine(tempRoot, "DemoGame", "src", "DemoGame.Runtime");
            string runtimeProjectPath = Path.Combine(runtimeDirectory, "DemoGame.Runtime.csproj");
            string runtimeProgramPath = Path.Combine(runtimeDirectory, "Program.cs");
            Assert.True(File.Exists(runtimeProjectPath));
            Assert.True(File.Exists(runtimeProgramPath));

            string projectText = File.ReadAllText(runtimeProjectPath);
            Assert.DoesNotContain("__ENGINE_MANAGED_SRC_RELATIVE__", projectText, StringComparison.Ordinal);
            Assert.Contains("ProjectReference", projectText, StringComparison.Ordinal);
            Assert.Contains("Engine.Content/Engine.Content.csproj", projectText, StringComparison.Ordinal);
            Assert.Contains("Engine.NativeBindings/Engine.NativeBindings.csproj", projectText, StringComparison.Ordinal);
            Assert.Contains("Engine.Procedural/Engine.Procedural.csproj", projectText, StringComparison.Ordinal);
            Assert.Contains("Engine.Net/Engine.Net.csproj", projectText, StringComparison.Ordinal);

            XDocument runtimeProject = XDocument.Load(runtimeProjectPath);
            List<string> includes = runtimeProject
                .Descendants("ProjectReference")
                .Select(static element => element.Attribute("Include")?.Value)
                .Where(static include => !string.IsNullOrWhiteSpace(include))
                .Select(static include => include!)
                .ToList();

            Assert.NotEmpty(includes);
            foreach (string include in includes)
            {
                string normalizedInclude = include.Replace('/', Path.DirectorySeparatorChar);
                string absoluteReferencePath = Path.GetFullPath(Path.Combine(runtimeDirectory, normalizedInclude));
                Assert.True(File.Exists(absoluteReferencePath), $"Resolved ProjectReference path does not exist: {absoluteReferencePath}");
            }

            string programText = File.ReadAllText(runtimeProgramPath);
            Assert.DoesNotContain("__GAME_NAME__", programText, StringComparison.Ordinal);
            Assert.Contains("TryConfigurePackagedContent", programText, StringComparison.Ordinal);
            Assert.Contains("PackagedRuntimeNativeBootstrap.ConfigureEnvironmentFromRuntimeConfig", programText, StringComparison.Ordinal);
            Assert.Contains("NativeFacadeFactory.CreateNativeFacadeSet", programText, StringComparison.Ordinal);
            Assert.Contains("nativeFacades?.Dispose()", programText, StringComparison.Ordinal);
            Assert.Contains("PackagedRuntimeContentBootstrap.ConfigureFromRuntimeConfig", programText, StringComparison.Ordinal);
            Assert.Contains("LevelGenerator.Generate", programText, StringComparison.Ordinal);
            Assert.Contains("UiPreviewHost", programText, StringComparison.Ordinal);
            Assert.Contains("InMemoryNetSession", programText, StringComparison.Ordinal);
            Assert.Contains("ProceduralSoundBlobBuilder.BuildMonoPcmBlob", programText, StringComparison.Ordinal);
            Assert.Contains("DemoGame runtime demo", programText, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"engine-cli-template-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
