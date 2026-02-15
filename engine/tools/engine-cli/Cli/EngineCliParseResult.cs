namespace Engine.Cli;

public sealed class EngineCliParseResult
{
    public static EngineCliParseResult Failure(string error) => new(null, error);

    public static EngineCliParseResult Success(EngineCliCommand command) => new(command, null);

    private EngineCliParseResult(EngineCliCommand? command, string? error)
    {
        Command = command;
        Error = error;
    }

    public EngineCliCommand? Command { get; }

    public string? Error { get; }

    public bool IsSuccess => Command is not null;
}
