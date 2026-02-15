namespace Assetc;

public abstract record AssetcCommand;

public sealed record BuildAssetsCommand(string ManifestPath, string OutputPakPath) : AssetcCommand;

public sealed record ListAssetsCommand(string PakPath) : AssetcCommand;
