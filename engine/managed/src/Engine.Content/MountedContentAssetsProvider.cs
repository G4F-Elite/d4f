using Engine.Core.Abstractions;

namespace Engine.Content;

public sealed class MountedContentAssetsProvider : IAssetsProvider
{
    private readonly IContentRuntimeFacade _contentRuntime;
    private readonly AssetsRuntimeMode _runtimeMode;

    public MountedContentAssetsProvider(
        IContentRuntimeFacade contentRuntime,
        AssetsRuntimeMode runtimeMode = AssetsRuntimeMode.Development)
    {
        _contentRuntime = contentRuntime ?? throw new ArgumentNullException(nameof(contentRuntime));
        if (!Enum.IsDefined(runtimeMode))
        {
            throw new ArgumentOutOfRangeException(nameof(runtimeMode), runtimeMode, "Unsupported assets runtime mode.");
        }

        _runtimeMode = runtimeMode;
    }

    public void MountPak(string pakPath)
    {
        _contentRuntime.MountPak(pakPath);
    }

    public void MountDirectory(string directoryPath)
    {
        if (_runtimeMode == AssetsRuntimeMode.PakOnly)
        {
            throw new InvalidOperationException(
                "Mounted content provider is running in pak-only mode and cannot mount development directories.");
        }

        _contentRuntime.MountDirectory(directoryPath);
    }

    public T Load<T>(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Asset path cannot be empty.", nameof(path));
        }

        string normalizedPath = path.Trim();
        byte[] bytes = _contentRuntime.ReadFile(normalizedPath);
        object boxed = DecodeAsset<T>(normalizedPath, bytes);
        return (T)boxed;
    }

    public T GetOrCreate<T>(IAssetRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        throw new InvalidOperationException(
            "Mounted content provider does not support runtime recipe generation. " +
            "Use a development assets provider for GetOrCreate.");
    }

    public void BakeAll()
    {
        throw new InvalidOperationException(
            "Mounted content provider does not support BakeAll. Use tooling pipeline to build Game.pak.");
    }

    private static object DecodeAsset<T>(string path, byte[] bytes)
    {
        if (typeof(T) == typeof(byte[]))
        {
            return bytes;
        }

        if (typeof(T) == typeof(MeshBlobData))
        {
            return MeshBlobCodec.Read(bytes);
        }

        if (typeof(T) == typeof(TextureBlobData))
        {
            return TextureBlobCodec.Read(bytes);
        }

        if (typeof(T) == typeof(MaterialBlobData))
        {
            return MaterialBlobCodec.Read(bytes);
        }

        if (typeof(T) == typeof(SoundBlobData))
        {
            return SoundBlobCodec.Read(bytes);
        }

        throw new InvalidDataException(
            $"Asset type '{typeof(T).FullName}' is not supported by mounted content provider for '{path}'.");
    }
}
