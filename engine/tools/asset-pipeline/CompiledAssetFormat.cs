namespace Engine.AssetPipeline;

public static class CompiledAssetFormat
{
    public const uint TextureMagic = 0x58544644; // DFTX

    public const uint TextureVersion = 1;

    public const uint MeshMagic = 0x48534644; // DFSH

    public const uint MeshVersion = 1;

    public const uint RawMagic = 0x57524644; // DFRW

    public const uint RawVersion = 1;

    public const uint SceneMagic = 0x4E435346; // FSCN

    public const uint SceneVersion = 1;

    public const uint PrefabMagic = 0x42465046; // PFPB

    public const uint PrefabVersion = 1;
}
