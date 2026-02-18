using System.Globalization;
using Engine.Testing;

namespace Engine.Cli;

public static partial class EngineCliParser
{
    private static EngineCliParseResult ParseTest(IReadOnlyDictionary<string, string> options)
    {
        if (!options.TryGetValue("project", out string? project))
        {
            return EngineCliParseResult.Failure("Option '--project' is required for 'test'.");
        }

        string configuration = options.TryGetValue("configuration", out string? configurationValue)
            ? configurationValue
            : "Debug";
        if (!ValidConfigurations.Contains(configuration))
        {
            return EngineCliParseResult.Failure("Option '--configuration' must be 'Debug' or 'Release'.");
        }

        string artifacts = GetOutOrOutputPath(
            options,
            defaultValue: Path.Combine(project, "artifacts", "tests"),
            out string? outError);
        if (outError is not null)
        {
            return EngineCliParseResult.Failure(outError);
        }

        string? goldenDirectory = options.TryGetValue("golden", out string? goldenValue)
            ? goldenValue
            : null;
        if (goldenDirectory is not null && string.IsNullOrWhiteSpace(goldenDirectory))
        {
            return EngineCliParseResult.Failure("Option '--golden' cannot be empty.");
        }

        bool pixelPerfect = false;
        if (options.TryGetValue("comparison", out string? comparisonMode))
        {
            if (string.Equals(comparisonMode, "pixel", StringComparison.OrdinalIgnoreCase))
            {
                pixelPerfect = true;
            }
            else if (string.Equals(comparisonMode, "tolerant", StringComparison.OrdinalIgnoreCase))
            {
                pixelPerfect = false;
            }
            else
            {
                return EngineCliParseResult.Failure("Option '--comparison' must be 'pixel' or 'tolerant'.");
            }
        }

        int captureFrame = 1;
        if (options.TryGetValue("capture-frame", out string? captureFrameValue))
        {
            if (!int.TryParse(captureFrameValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out captureFrame) ||
                captureFrame <= 0 ||
                captureFrame > 100_000)
            {
                return EngineCliParseResult.Failure("Option '--capture-frame' must be an integer in range [1..100000].");
            }
        }

        ulong replaySeed = 1337UL;
        if (options.TryGetValue("seed", out string? seedValue))
        {
            if (!ulong.TryParse(seedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out replaySeed))
            {
                return EngineCliParseResult.Failure("Option '--seed' must be an unsigned integer.");
            }
        }

        double fixedDeltaSeconds = 1.0 / 60.0;
        if (options.TryGetValue("fixed-dt", out string? fixedDtValue))
        {
            if (!double.TryParse(fixedDtValue, NumberStyles.Float, CultureInfo.InvariantCulture, out fixedDeltaSeconds) ||
                fixedDeltaSeconds <= 0.0)
            {
                return EngineCliParseResult.Failure("Option '--fixed-dt' must be a positive number.");
            }
        }

        double tolerantMaxMae = GoldenImageComparisonOptions.TolerantDefault.MaxMeanAbsoluteError;
        if (options.TryGetValue("mae-threshold", out string? maeThresholdValue))
        {
            if (!double.TryParse(maeThresholdValue, NumberStyles.Float, CultureInfo.InvariantCulture, out tolerantMaxMae) ||
                tolerantMaxMae <= 0.0)
            {
                return EngineCliParseResult.Failure("Option '--mae-threshold' must be a positive number.");
            }
        }

        double tolerantMinPsnrDb = GoldenImageComparisonOptions.TolerantDefault.MinPsnrDb;
        if (options.TryGetValue("psnr-threshold", out string? psnrThresholdValue))
        {
            if (!double.TryParse(psnrThresholdValue, NumberStyles.Float, CultureInfo.InvariantCulture, out tolerantMinPsnrDb) ||
                tolerantMinPsnrDb <= 0.0)
            {
                return EngineCliParseResult.Failure("Option '--psnr-threshold' must be a positive number.");
            }
        }

        return EngineCliParseResult.Success(
            new TestCommand(
                project,
                artifacts,
                configuration,
                goldenDirectory,
                pixelPerfect,
                captureFrame,
                replaySeed,
                fixedDeltaSeconds,
                tolerantMaxMae,
                tolerantMinPsnrDb));
    }

    private static EngineCliParseResult ParsePack(IReadOnlyDictionary<string, string> options)
    {
        if (!options.TryGetValue("project", out string? project))
        {
            return EngineCliParseResult.Failure("Option '--project' is required for 'pack'.");
        }

        if (!options.TryGetValue("manifest", out string? manifest))
        {
            return EngineCliParseResult.Failure("Option '--manifest' is required for 'pack'.");
        }

        string output = GetOutOrOutputPath(
            options,
            defaultValue: Path.Combine(project, "dist", "content.pak"),
            out string? outError);
        if (outError is not null)
        {
            return EngineCliParseResult.Failure(outError);
        }

        string configuration = options.TryGetValue("configuration", out string? cfg)
            ? cfg
            : "Release";
        if (!ValidConfigurations.Contains(configuration))
        {
            return EngineCliParseResult.Failure("Option '--configuration' must be 'Debug' or 'Release'.");
        }

        string runtimeIdentifier = options.TryGetValue("runtime", out string? runtimeValue)
            ? runtimeValue
            : "win-x64";
        if (string.IsNullOrWhiteSpace(runtimeIdentifier))
        {
            return EngineCliParseResult.Failure("Option '--runtime' cannot be empty.");
        }
        if (!ValidPackRuntimeIdentifiers.Contains(runtimeIdentifier))
        {
            return EngineCliParseResult.Failure("Option '--runtime' must be one of: win-x64, linux-x64.");
        }

        string? publishProjectPath = options.TryGetValue("publish-project", out string? publishPathValue)
            ? publishPathValue
            : null;
        if (publishProjectPath is not null && string.IsNullOrWhiteSpace(publishProjectPath))
        {
            return EngineCliParseResult.Failure("Option '--publish-project' cannot be empty.");
        }

        string? nativeLibraryPath = options.TryGetValue("native-lib", out string? nativeLibValue)
            ? nativeLibValue
            : null;
        if (nativeLibraryPath is not null && string.IsNullOrWhiteSpace(nativeLibraryPath))
        {
            return EngineCliParseResult.Failure("Option '--native-lib' cannot be empty.");
        }

        string? zipOutputPath = options.TryGetValue("zip", out string? zipValue)
            ? zipValue
            : null;
        if (zipOutputPath is not null && string.IsNullOrWhiteSpace(zipOutputPath))
        {
            return EngineCliParseResult.Failure("Option '--zip' cannot be empty.");
        }

        return EngineCliParseResult.Success(
            new PackCommand(
                project,
                manifest,
                output,
                configuration,
                runtimeIdentifier,
                publishProjectPath,
                nativeLibraryPath,
                zipOutputPath));
    }
}
