namespace Engine.Content;

public enum AssetsRuntimeMode
{
    Development = 0,
    PakOnly = 1
}

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
    private static AssetsRuntimeMode _runtimeMode = AssetsRuntimeMode.Development;

    public static void Configure(IAssetsProvider provider)
    {
        Configure(provider, AssetsRuntimeMode.Development);
    }

    public static void Configure(IAssetsProvider provider, AssetsRuntimeMode runtimeMode)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ValidateRuntimeMode(runtimeMode);

        lock (SyncRoot)
        {
            _provider = provider;
            _runtimeMode = runtimeMode;
        }
    }

    public static void ConfigurePakOnly(IAssetsProvider provider)
    {
        Configure(provider, AssetsRuntimeMode.PakOnly);
    }

    public static AssetsRuntimeMode GetRuntimeMode()
    {
        lock (SyncRoot)
        {
            return _runtimeMode;
        }
    }

    public static void Reset()
    {
        lock (SyncRoot)
        {
            _provider = null;
            _runtimeMode = AssetsRuntimeMode.Development;
        }
    }

    public static T Load<T>(string path)
    {
        IAssetsProvider provider = GetConfiguredProvider(out _);
        return provider.Load<T>(path);
    }

    public static T GetOrCreate<T>(IAssetRecipe recipe)
    {
        IAssetsProvider provider = GetConfiguredProvider(out AssetsRuntimeMode mode);
        EnsureRuntimeMutationAllowed(mode, nameof(GetOrCreate));
        return provider.GetOrCreate<T>(recipe);
    }

    public static void BakeAll()
    {
        IAssetsProvider provider = GetConfiguredProvider(out AssetsRuntimeMode mode);
        EnsureRuntimeMutationAllowed(mode, nameof(BakeAll));
        provider.BakeAll();
    }

    private static IAssetsProvider GetConfiguredProvider(out AssetsRuntimeMode runtimeMode)
    {
        lock (SyncRoot)
        {
            runtimeMode = _runtimeMode;
            return _provider ?? throw new InvalidOperationException(
                "Assets provider is not configured. Call Assets.Configure(...) before using Assets APIs.");
        }
    }

    private static void EnsureRuntimeMutationAllowed(AssetsRuntimeMode runtimeMode, string operation)
    {
        if (runtimeMode != AssetsRuntimeMode.PakOnly)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Assets.{operation} is disabled in '{AssetsRuntimeMode.PakOnly}' runtime mode. " +
            "Release runtime must load content from pak only.");
    }

    private static void ValidateRuntimeMode(AssetsRuntimeMode runtimeMode)
    {
        if (!Enum.IsDefined(runtimeMode))
        {
            throw new ArgumentOutOfRangeException(nameof(runtimeMode), runtimeMode, "Unsupported assets runtime mode.");
        }
    }
}
