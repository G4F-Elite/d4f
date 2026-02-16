using Engine.Testing;

namespace Engine.Tests.Testing;

public sealed class GoldenImageBufferFileCodecTests
{
    [Fact]
    public void WriteAndRead_ShouldRoundtripImage()
    {
        string root = CreateTempDirectory();
        try
        {
            string path = Path.Combine(root, "capture.bin");
            var image = new GoldenImageBuffer(2, 1, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });

            GoldenImageBufferFileCodec.Write(path, image);
            GoldenImageBuffer loaded = GoldenImageBufferFileCodec.Read(path);

            Assert.Equal(image.Width, loaded.Width);
            Assert.Equal(image.Height, loaded.Height);
            Assert.Equal(image.RgbaBytes.ToArray(), loaded.RgbaBytes.ToArray());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Read_ShouldFail_WhenFileMissing()
    {
        string path = Path.Combine(Path.GetTempPath(), $"missing-golden-{Guid.NewGuid():N}.bin");
        Assert.Throws<FileNotFoundException>(() => GoldenImageBufferFileCodec.Read(path));
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"engine-testing-golden-file-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
