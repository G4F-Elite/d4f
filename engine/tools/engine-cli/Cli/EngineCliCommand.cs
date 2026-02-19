using Engine.Rendering;

namespace Engine.Cli;

public abstract record EngineCliCommand;

public enum TestHostMode
{
    HiddenWindow = 0,
    HeadlessOffscreen = 1
}

public sealed record InitCommand(string Name, string OutputDirectory) : EngineCliCommand;

public sealed record NewCommand(string Name, string OutputDirectory) : EngineCliCommand;

public sealed record BuildCommand(string ProjectDirectory, string Configuration) : EngineCliCommand;

public sealed record RunCommand(string ProjectDirectory, string Configuration, RenderDebugViewMode DebugViewMode) : EngineCliCommand;

public sealed record BakeCommand(
    string ProjectDirectory,
    string ManifestPath,
    string OutputPakPath) : EngineCliCommand;

public sealed record PreviewCommand(
    string ProjectDirectory,
    string ManifestPath,
    string OutputDirectory) : EngineCliCommand;

public sealed record PreviewAudioCommand(
    string ProjectDirectory,
    string ManifestPath,
    string OutputDirectory) : EngineCliCommand;

public sealed record PreviewDumpCommand(
    string ProjectDirectory,
    string ManifestPath) : EngineCliCommand;

public sealed record TestCommand(
    string ProjectDirectory,
    string ArtifactsDirectory,
    string Configuration,
    TestHostMode HostMode,
    string? GoldenDirectory,
    bool PixelPerfectGolden,
    int CaptureFrame,
    ulong ReplaySeed,
    double FixedDeltaSeconds,
    double TolerantMaxMae,
    double TolerantMinPsnrDb,
    string? ReplayPath) : EngineCliCommand;

public sealed record DoctorCommand(
    string ProjectDirectory,
    string? RuntimePerfMetricsPath,
    double? MaxAverageCaptureCpuMs,
    long? MaxPeakCaptureAllocatedBytes,
    bool RequireZeroAllocationCapturePath,
    bool RequireRuntimeTransportSuccess,
    string? MultiplayerDemoSummaryPath,
    bool VerifyMultiplayerSnapshotBinary,
    string? MultiplayerSnapshotBinaryPath) : EngineCliCommand;

public sealed record ApiDumpCommand(string HeaderPath, string OutputPath) : EngineCliCommand;

public sealed record PackCommand(
    string ProjectDirectory,
    string ManifestPath,
    string OutputPakPath,
    string Configuration,
    string RuntimeIdentifier,
    string? PublishProjectPath,
    string? NativeLibraryPath,
    string? ZipOutputPath) : EngineCliCommand;
