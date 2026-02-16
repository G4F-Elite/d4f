namespace Engine.Content;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class DffAssetAttribute : Attribute
{
    private string[] _tags = [];

    public DffAssetAttribute(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Asset path cannot be empty.", nameof(path));
        }

        Path = path;
    }

    public string Path { get; }

    public string? Category { get; init; }

    public string[] Tags
    {
        get => _tags;
        init => _tags = value ?? throw new ArgumentNullException(nameof(value));
    }
}
