using System.Text.Json.Nodes;
using Engine.Cli;

namespace Engine.Cli.Tests;

public sealed class EngineCliUpdateCommandTests
{
    [Fact]
    public void Run_UpdateCommand_ShouldRefreshEngineReferencesAndProjectVersion()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            string projectDirectory = Path.Combine(tempRoot, "MyGame");
            string runtimeDirectory = Path.Combine(projectDirectory, "src", "MyGame.Runtime");
            Directory.CreateDirectory(runtimeDirectory);

            string runtimeProjectPath = Path.Combine(runtimeDirectory, "MyGame.Runtime.csproj");
            File.WriteAllText(
                runtimeProjectPath,
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <ItemGroup>
                    <ProjectReference Include="..\..\..\old\Engine.Core\Engine.Core.csproj" />
                    <ProjectReference Include="..\..\..\old\Engine.Audio\Engine.Audio.csproj" />
                  </ItemGroup>
                </Project>
                """);

            string projectJsonPath = Path.Combine(projectDirectory, "project.json");
            Directory.CreateDirectory(projectDirectory);
            File.WriteAllText(projectJsonPath, "{\n  \"name\": \"MyGame\",\n  \"engineVersion\": \"0.0.0\"\n}\n");

            string managedSourceDirectory = Path.Combine(tempRoot, "engine", "managed", "src");
            WriteManagedProject(managedSourceDirectory, "Engine.Core");
            WriteManagedProject(managedSourceDirectory, "Engine.Audio");

            using var output = new StringWriter();
            using var error = new StringWriter();
            EngineCliApp app = new(output, error);

            int code = app.Run([
                "update",
                "--project", projectDirectory,
                "--engine-managed-src", managedSourceDirectory
            ]);

            Assert.Equal(0, code);
            Assert.Equal(string.Empty, error.ToString());

            string relativeManagedPath = Path.GetRelativePath(runtimeDirectory, managedSourceDirectory).Replace('\\', '/');
            string runtimeProjectText = File.ReadAllText(runtimeProjectPath);
            Assert.Contains($"{relativeManagedPath}/Engine.Core/Engine.Core.csproj", runtimeProjectText, StringComparison.Ordinal);
            Assert.Contains($"{relativeManagedPath}/Engine.Audio/Engine.Audio.csproj", runtimeProjectText, StringComparison.Ordinal);

            JsonObject projectJson = JsonNode.Parse(File.ReadAllText(projectJsonPath))!.AsObject();
            string? engineVersion = projectJson["engineVersion"]?.GetValue<string>();
            Assert.False(string.IsNullOrWhiteSpace(engineVersion));
            Assert.NotEqual("0.0.0", engineVersion);

            Assert.Contains("Engine update completed.", output.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_UpdateCommand_ShouldFail_WhenManagedSourcePathInvalid()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            string projectDirectory = Path.Combine(tempRoot, "MyGame");
            string runtimeDirectory = Path.Combine(projectDirectory, "src", "MyGame.Runtime");
            Directory.CreateDirectory(runtimeDirectory);
            Directory.CreateDirectory(projectDirectory);

            string runtimeProjectPath = Path.Combine(runtimeDirectory, "MyGame.Runtime.csproj");
            File.WriteAllText(
                runtimeProjectPath,
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <ItemGroup>
                    <ProjectReference Include="..\..\..\old\Engine.Core\Engine.Core.csproj" />
                  </ItemGroup>
                </Project>
                """);

            File.WriteAllText(Path.Combine(projectDirectory, "project.json"), "{\n  \"name\": \"MyGame\"\n}\n");

            using var output = new StringWriter();
            using var error = new StringWriter();
            EngineCliApp app = new(output, error);

            int code = app.Run([
                "update",
                "--project", projectDirectory,
                "--engine-managed-src", "missing-managed-src"
            ]);

            Assert.Equal(1, code);
            Assert.Contains("Engine managed source directory was not found", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static void WriteManagedProject(string managedSourceDirectory, string projectName)
    {
        string directory = Path.Combine(managedSourceDirectory, projectName);
        Directory.CreateDirectory(directory);
        string projectPath = Path.Combine(directory, $"{projectName}.csproj");
        File.WriteAllText(
            projectPath,
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net9.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"engine-cli-update-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
