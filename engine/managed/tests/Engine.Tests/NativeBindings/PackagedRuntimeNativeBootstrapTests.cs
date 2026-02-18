using Engine.NativeBindings;

namespace Engine.Tests.NativeBindings;

public sealed class PackagedRuntimeNativeBootstrapTests
{
    [Fact]
    public void ConfigureEnvironmentFromRuntimeConfig_ShouldSetNativeLibraryPath_AndClearSearchPath()
    {
        string packageRoot = CreateTempPackage();
        string runtimeConfigPath = Path.Combine(packageRoot, "config", "runtime.json");
        File.WriteAllText(
            runtimeConfigPath,
            """
            {
              "nativeLibrary": "App/dff_native.dll",
              "nativeLibrarySearchPath": null
            }
            """);

        string previousLibrary = Environment.GetEnvironmentVariable(PackagedRuntimeNativeBootstrap.NativeLibraryPathEnvironmentVariable) ?? string.Empty;
        string previousSearchPath = Environment.GetEnvironmentVariable(PackagedRuntimeNativeBootstrap.NativeLibrarySearchPathEnvironmentVariable) ?? string.Empty;
        Environment.SetEnvironmentVariable(PackagedRuntimeNativeBootstrap.NativeLibrarySearchPathEnvironmentVariable, "stale-value");

        try
        {
            PackagedRuntimeNativeBootstrap.ConfigureEnvironmentFromRuntimeConfig(runtimeConfigPath);

            string expectedNativePath = Path.GetFullPath(Path.Combine(packageRoot, "App", "dff_native.dll"));
            Assert.Equal(
                expectedNativePath,
                Environment.GetEnvironmentVariable(PackagedRuntimeNativeBootstrap.NativeLibraryPathEnvironmentVariable));
            Assert.Null(Environment.GetEnvironmentVariable(PackagedRuntimeNativeBootstrap.NativeLibrarySearchPathEnvironmentVariable));
        }
        finally
        {
            RestoreEnvironmentVariable(PackagedRuntimeNativeBootstrap.NativeLibraryPathEnvironmentVariable, previousLibrary);
            RestoreEnvironmentVariable(PackagedRuntimeNativeBootstrap.NativeLibrarySearchPathEnvironmentVariable, previousSearchPath);
            Directory.Delete(packageRoot, recursive: true);
        }
    }

    [Fact]
    public void ConfigureEnvironmentFromRuntimeConfig_ShouldResolveOriginSearchPath()
    {
        string packageRoot = CreateTempPackage();
        string runtimeConfigPath = Path.Combine(packageRoot, "config", "runtime.json");
        File.WriteAllText(
            runtimeConfigPath,
            """
            {
              "nativeLibrary": "App/dff_native.dll",
              "nativeLibrarySearchPath": "$ORIGIN"
            }
            """);

        string appDirectory = Path.GetFullPath(Path.Combine(packageRoot, "App"));
        string previousLibrary = Environment.GetEnvironmentVariable(PackagedRuntimeNativeBootstrap.NativeLibraryPathEnvironmentVariable) ?? string.Empty;
        string previousSearchPath = Environment.GetEnvironmentVariable(PackagedRuntimeNativeBootstrap.NativeLibrarySearchPathEnvironmentVariable) ?? string.Empty;

        try
        {
            PackagedRuntimeNativeBootstrap.ConfigureEnvironmentFromRuntimeConfig(runtimeConfigPath, appDirectory);

            Assert.Equal(
                appDirectory,
                Environment.GetEnvironmentVariable(PackagedRuntimeNativeBootstrap.NativeLibrarySearchPathEnvironmentVariable));
        }
        finally
        {
            RestoreEnvironmentVariable(PackagedRuntimeNativeBootstrap.NativeLibraryPathEnvironmentVariable, previousLibrary);
            RestoreEnvironmentVariable(PackagedRuntimeNativeBootstrap.NativeLibrarySearchPathEnvironmentVariable, previousSearchPath);
            Directory.Delete(packageRoot, recursive: true);
        }
    }

    [Fact]
    public void ConfigureEnvironmentFromRuntimeConfig_ShouldThrow_WhenNativeLibraryIsMissing()
    {
        string packageRoot = CreateTempPackage(createNativeLibraryFile: false);
        string runtimeConfigPath = Path.Combine(packageRoot, "config", "runtime.json");
        File.WriteAllText(
            runtimeConfigPath,
            """
            {
              "nativeLibrary": "App/dff_native.dll"
            }
            """);

        try
        {
            FileNotFoundException exception = Assert.Throws<FileNotFoundException>(() =>
                PackagedRuntimeNativeBootstrap.ConfigureEnvironmentFromRuntimeConfig(runtimeConfigPath));
            Assert.EndsWith(
                Path.Combine("App", "dff_native.dll"),
                exception.FileName ?? string.Empty,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(packageRoot, recursive: true);
        }
    }

    [Fact]
    public void ConfigureEnvironmentFromRuntimeConfig_ShouldThrow_WhenJsonIsInvalid()
    {
        string packageRoot = CreateTempPackage();
        string runtimeConfigPath = Path.Combine(packageRoot, "config", "runtime.json");
        File.WriteAllText(runtimeConfigPath, "{ \"nativeLibrary\": ");

        try
        {
            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
                PackagedRuntimeNativeBootstrap.ConfigureEnvironmentFromRuntimeConfig(runtimeConfigPath));
            Assert.Contains("invalid JSON", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(packageRoot, recursive: true);
        }
    }

    [Fact]
    public void ApplyConfiguredSearchPath_ShouldPrependAndAvoidDuplicates()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), $"engine-native-bootstrap-path-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        const string loaderVariableName = "DFF_TEST_NATIVE_LOADER_PATH";
        string previousSearchPath = Environment.GetEnvironmentVariable(PackagedRuntimeNativeBootstrap.NativeLibrarySearchPathEnvironmentVariable) ?? string.Empty;
        string previousLoaderPath = Environment.GetEnvironmentVariable(loaderVariableName) ?? string.Empty;

        try
        {
            Environment.SetEnvironmentVariable(PackagedRuntimeNativeBootstrap.NativeLibrarySearchPathEnvironmentVariable, tempRoot);
            Environment.SetEnvironmentVariable(loaderVariableName, "alpha;beta");

            PackagedRuntimeNativeBootstrap.ApplyConfiguredSearchPath(loaderVariableName, pathComparisonIgnoreCase: false);
            string firstValue = Environment.GetEnvironmentVariable(loaderVariableName)
                ?? throw new InvalidDataException("Loader variable was not set.");
            Assert.StartsWith(Path.GetFullPath(tempRoot), firstValue, StringComparison.Ordinal);
            Assert.EndsWith("alpha;beta", firstValue, StringComparison.Ordinal);

            PackagedRuntimeNativeBootstrap.ApplyConfiguredSearchPath(loaderVariableName, pathComparisonIgnoreCase: false);
            string secondValue = Environment.GetEnvironmentVariable(loaderVariableName)
                ?? throw new InvalidDataException("Loader variable was not set.");
            Assert.Equal(firstValue, secondValue);
        }
        finally
        {
            RestoreEnvironmentVariable(PackagedRuntimeNativeBootstrap.NativeLibrarySearchPathEnvironmentVariable, previousSearchPath);
            RestoreEnvironmentVariable(loaderVariableName, previousLoaderPath);
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static string CreateTempPackage(bool createNativeLibraryFile = true)
    {
        string packageRoot = Path.Combine(Path.GetTempPath(), $"engine-native-bootstrap-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(packageRoot);
        Directory.CreateDirectory(Path.Combine(packageRoot, "config"));
        Directory.CreateDirectory(Path.Combine(packageRoot, "App"));

        if (createNativeLibraryFile)
        {
            File.WriteAllBytes(Path.Combine(packageRoot, "App", "dff_native.dll"), [0x00, 0x01, 0x02, 0x03]);
        }

        return packageRoot;
    }

    private static void RestoreEnvironmentVariable(string variableName, string previousValue)
    {
        Environment.SetEnvironmentVariable(
            variableName,
            previousValue.Length == 0 ? null : previousValue);
    }
}
