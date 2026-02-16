using Engine.Testing;

namespace Engine.Tests.Testing;

public sealed class TestingArtifactManifestCodecTests
{
    [Fact]
    public void WriteAndRead_ShouldRoundtripManifest()
    {
        string root = CreateTempDirectory();
        try
        {
            string manifestPath = Path.Combine(root, "manifest.json");
            var manifest = new TestingArtifactManifest(
                DateTime.UtcNow,
                [
                    new TestingArtifactEntry("screenshot", "screens/frame-1.png", "Frame 1"),
                    new TestingArtifactEntry("dump", "dumps/depth-1.png", "Depth")
                ]);

            TestingArtifactManifestCodec.Write(manifestPath, manifest);
            TestingArtifactManifest loaded = TestingArtifactManifestCodec.Read(manifestPath);

            Assert.Equal(manifest.Artifacts.Count, loaded.Artifacts.Count);
            Assert.Equal(manifest.Artifacts[0], loaded.Artifacts[0]);
            Assert.Equal(manifest.Artifacts[1], loaded.Artifacts[1]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Read_ShouldFail_WhenManifestMissing()
    {
        string path = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.json");
        Assert.Throws<FileNotFoundException>(() => TestingArtifactManifestCodec.Read(path));
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"engine-testing-manifest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
