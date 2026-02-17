using Engine.Content;

namespace Engine.AssetPipeline;

public static class CompiledAssetFormat
{
    public const uint TextureMagic = TextureBlobCodec.Magic;

    public const uint TextureVersion = TextureBlobCodec.Version;

    public const uint MeshMagic = MeshBlobCodec.Magic;

    public const uint MeshVersion = MeshBlobCodec.Version;

    public const uint MaterialMagic = MaterialBlobCodec.Magic;

    public const uint MaterialVersion = MaterialBlobCodec.Version;

    public const uint SoundMagic = SoundBlobCodec.Magic;

    public const uint SoundVersion = SoundBlobCodec.Version;

    public const uint RawMagic = 0x57524644; // DFRW

    public const uint RawVersion = 1;

    public const uint SceneMagic = 0x4E435346; // FSCN

    public const uint SceneVersion = 1;

    public const uint PrefabMagic = 0x42465046; // PFPB

    public const uint PrefabVersion = 1;
}
