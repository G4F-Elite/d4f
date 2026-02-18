using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Engine.Cli;

namespace Engine.Cli.Tests;

public sealed class EngineCliPreviewMaterialLightingTests
{
    [Fact]
    public void Run_Preview_ShouldRenderMaterialPreviewWithBrighterCenterThanEdge()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareMaterialManifest(tempRoot);
            using var output = new StringWriter();
            using var error = new StringWriter();
            EngineCliApp app = new(output, error);

            int code = app.Run(
            [
                "preview",
                "--project", tempRoot,
                "--manifest", "assets/manifest.json",
                "--out", "artifacts/preview"
            ]);

            Assert.Equal(0, code);
            byte[] rgba = DecodePngRgba(
                Path.Combine(tempRoot, "artifacts", "preview", "materials", "materials_wall.png"),
                out int width,
                out int height);

            double centerLuma = ReadLuminance(rgba, width, height, width / 2, height / 2);
            double edgeLuma = ReadLuminance(rgba, width, height, 4, 4);
            Assert.True(centerLuma > edgeLuma + 0.06, $"Expected center luma ({centerLuma:F4}) to exceed edge luma ({edgeLuma:F4}).");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static void PrepareMaterialManifest(string rootPath)
    {
        string assetsDirectory = Path.Combine(rootPath, "assets");
        Directory.CreateDirectory(Path.Combine(assetsDirectory, "materials"));
        File.WriteAllText(Path.Combine(assetsDirectory, "materials", "wall.mat"), "material-data");
        File.WriteAllText(
            Path.Combine(assetsDirectory, "manifest.json"),
            """
            {
              "version": 1,
              "assets": [
                { "path": "materials/wall.mat", "kind": "material" }
              ]
            }
            """);
    }

    private static double ReadLuminance(byte[] rgba, int width, int height, int x, int y)
    {
        int clampedX = Math.Clamp(x, 0, width - 1);
        int clampedY = Math.Clamp(y, 0, height - 1);
        int index = ((clampedY * width) + clampedX) * 4;
        return (0.2126 * rgba[index] + 0.7152 * rgba[index + 1] + 0.0722 * rgba[index + 2]) / 255.0;
    }

    private static byte[] DecodePngRgba(string path, out int width, out int height)
    {
        byte[] bytes = File.ReadAllBytes(path);
        Assert.True(bytes.Length > 33);
        Assert.Equal((byte)0x89, bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'N', bytes[2]);
        Assert.Equal((byte)'G', bytes[3]);

        width = 0;
        height = 0;
        int index = 8;
        using var idat = new MemoryStream();
        while (index + 8 <= bytes.Length)
        {
            uint length = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(index, 4));
            index += 4;
            string chunkType = Encoding.ASCII.GetString(bytes, index, 4);
            index += 4;

            Assert.True(index + length + 4 <= bytes.Length);
            ReadOnlySpan<byte> payload = bytes.AsSpan(index, checked((int)length));
            index += checked((int)length);
            index += 4;

            if (chunkType == "IHDR")
            {
                width = (int)BinaryPrimitives.ReadUInt32BigEndian(payload.Slice(0, 4));
                height = (int)BinaryPrimitives.ReadUInt32BigEndian(payload.Slice(4, 4));
            }
            else if (chunkType == "IDAT")
            {
                idat.Write(payload);
            }
            else if (chunkType == "IEND")
            {
                break;
            }
        }

        Assert.True(width > 0);
        Assert.True(height > 0);

        idat.Position = 0;
        using var inflated = new MemoryStream();
        using (var zlib = new ZLibStream(idat, CompressionMode.Decompress, leaveOpen: true))
        {
            zlib.CopyTo(inflated);
        }

        byte[] scanlines = inflated.ToArray();
        int stride = checked(width * 4);
        Assert.Equal((stride + 1) * height, scanlines.Length);
        var rgba = new byte[checked(width * height * 4)];
        for (int y = 0; y < height; y++)
        {
            int scanlineOffset = y * (stride + 1);
            Assert.Equal(0, scanlines[scanlineOffset]);
            Buffer.BlockCopy(scanlines, scanlineOffset + 1, rgba, y * stride, stride);
        }

        return rgba;
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"engine-cli-preview-material-lighting-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
