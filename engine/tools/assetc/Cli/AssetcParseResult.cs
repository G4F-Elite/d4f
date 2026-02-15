namespace Assetc;

public sealed class AssetcParseResult
{
    public static AssetcParseResult Failure(string error) => new(null, error);

    public static AssetcParseResult Success(AssetcCommand command) => new(command, null);

    private AssetcParseResult(AssetcCommand? command, string? error)
    {
        Command = command;
        Error = error;
    }

    public AssetcCommand? Command { get; }

    public string? Error { get; }

    public bool IsSuccess => Command is not null;
}
