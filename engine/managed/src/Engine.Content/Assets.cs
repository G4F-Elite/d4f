namespace Engine.Content;

public interface IAssetsProvider
{
    T Load<T>(string path);

    T GetOrCreate<T>(IAssetRecipe recipe);

    void BakeAll();
}

public static class Assets
{
    private static readonly object SyncRoot = new();
    private static IAssetsProvider? _provider;

    public static void Configure(IAssetsProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        lock (SyncRoot)
        {
            _provider = provider;
        }
    }

    public static void Reset()
    {
        lock (SyncRoot)
        {
            _provider = null;
        }
    }

    public static T Load<T>(string path)
    {
        return GetProvider().Load<T>(path);
    }

    public static T GetOrCreate<T>(IAssetRecipe recipe)
    {
        return GetProvider().GetOrCreate<T>(recipe);
    }

    public static void BakeAll()
    {
        GetProvider().BakeAll();
    }

    private static IAssetsProvider GetProvider()
    {
        lock (SyncRoot)
        {
            return _provider ?? throw new InvalidOperationException(
                "Assets provider is not configured. Call Assets.Configure(...) before using Assets APIs.");
        }
    }
}
