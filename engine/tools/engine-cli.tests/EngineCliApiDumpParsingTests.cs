using System.Text.Json;
using Engine.Cli;

namespace Engine.Cli.Tests;

public sealed class EngineCliApiDumpParsingTests
{
    [Fact]
    public void Run_ShouldCaptureMultilineApiDeclaration_InApiDump()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            string headerPath = Path.Combine(tempRoot, "engine_native.h");
            File.WriteAllText(
                headerPath,
                """
                #define ENGINE_NATIVE_API_VERSION 13u
                ENGINE_NATIVE_API int engine_create(
                    const void* desc,
                    int flags);
                ENGINE_NATIVE_API void engine_destroy(void);
                """);

            string outputPath = Path.Combine(tempRoot, "api", "dump.json");
            using var output = new StringWriter();
            using var error = new StringWriter();
            var app = new EngineCliApp(output, error);

            int code = app.Run(["api", "dump", "--header", headerPath, "--out", outputPath]);

            Assert.Equal(0, code);
            using JsonDocument json = JsonDocument.Parse(File.ReadAllText(outputPath));
            JsonElement functions = json.RootElement.GetProperty("functions");
            Assert.Equal(2, functions.GetArrayLength());

            JsonElement first = functions[0];
            Assert.Equal("engine_create", first.GetProperty("name").GetString());
            string declaration = first.GetProperty("declaration").GetString()
                ?? throw new InvalidDataException("API declaration is missing.");
            Assert.Contains("ENGINE_NATIVE_API int engine_create(", declaration, StringComparison.Ordinal);
            Assert.Contains("const void* desc,", declaration, StringComparison.Ordinal);
            Assert.Contains("int flags);", declaration, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"engine-cli-api-dump-parsing-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
