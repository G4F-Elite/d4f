using Engine.Cli;

namespace Engine.Cli.Tests;

public sealed class EngineCliApiDumpFailureTests
{
    [Fact]
    public void Parse_ShouldFailApiDump_WhenHeaderIsWhitespace()
    {
        EngineCliParseResult result = EngineCliParser.Parse(["api", "dump", "--header", "   "]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Option '--header' cannot be empty.", result.Error);
    }

    [Fact]
    public void Run_ShouldFailApiDump_WhenHeaderFileMissing()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            string headerPath = Path.Combine(tempRoot, "missing.h");
            using var output = new StringWriter();
            using var error = new StringWriter();
            var app = new EngineCliApp(output, error);

            int code = app.Run(["api", "dump", "--header", headerPath, "--out", Path.Combine(tempRoot, "api.json")]);

            Assert.Equal(1, code);
            Assert.Contains("Native API header was not found", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_ShouldFailApiDump_WhenVersionDefineMissing()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            string headerPath = Path.Combine(tempRoot, "engine_native.h");
            File.WriteAllText(
                headerPath,
                """
                ENGINE_NATIVE_API int engine_create(const void* desc);
                """);

            using var output = new StringWriter();
            using var error = new StringWriter();
            var app = new EngineCliApp(output, error);

            int code = app.Run(["api", "dump", "--header", headerPath, "--out", Path.Combine(tempRoot, "api.json")]);

            Assert.Equal(1, code);
            Assert.Contains("ENGINE_NATIVE_API_VERSION was not found in header.", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_ShouldFailApiDump_WhenNoApiDeclarationsFound()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            string headerPath = Path.Combine(tempRoot, "engine_native.h");
            File.WriteAllText(
                headerPath,
                """
                #define ENGINE_NATIVE_API_VERSION 10u
                int non_exported_function(int x);
                """);

            using var output = new StringWriter();
            using var error = new StringWriter();
            var app = new EngineCliApp(output, error);

            int code = app.Run(["api", "dump", "--header", headerPath, "--out", Path.Combine(tempRoot, "api.json")]);

            Assert.Equal(1, code);
            Assert.Contains("No ENGINE_NATIVE_API declarations were found in header.", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"engine-cli-api-dump-failure-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
