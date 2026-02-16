namespace Engine.Cli;

public abstract record EngineCliCommand;

public sealed record InitCommand(string Name, string OutputDirectory) : EngineCliCommand;

public sealed record BuildCommand(string ProjectDirectory, string Configuration) : EngineCliCommand;

public sealed record RunCommand(string ProjectDirectory, string Configuration) : EngineCliCommand;

public sealed record PackCommand(
    string ProjectDirectory,
    string ManifestPath,
    string OutputPakPath,
    string Configuration,
    string RuntimeIdentifier,
    string? PublishProjectPath,
    string? NativeLibraryPath,
    string? ZipOutputPath) : EngineCliCommand;
