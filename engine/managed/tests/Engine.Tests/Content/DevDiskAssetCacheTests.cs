using Engine.Content;

namespace Engine.Tests.Content;

public sealed class DevDiskAssetCacheTests
{
    [Fact]
    public void Constructor_ShouldValidateRootDirectory()
    {
        Assert.Throws<ArgumentException>(() => new DevDiskAssetCache(""));
        Assert.Throws<ArgumentException>(() => new DevDiskAssetCache("   "));
    }

    [Fact]
    public void StoreAndTryLoad_ShouldRoundTripPayload()
    {
        string root = CreateTempDirectory();
        try
        {
            var cache = new DevDiskAssetCache(root);
            AssetKey key = CreateKey("01");
            byte[] payload = [1, 2, 3, 4];

            cache.Store(key, payload);
            bool found = cache.TryLoad(key, out byte[] loaded);

            Assert.True(found);
            Assert.Equal(payload, loaded);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void TryLoad_ShouldReturnFalse_WhenEntryMissing()
    {
        string root = CreateTempDirectory();
        try
        {
            var cache = new DevDiskAssetCache(root);
            bool found = cache.TryLoad(CreateKey("missing"), out byte[] loaded);

            Assert.False(found);
            Assert.Empty(loaded);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Remove_ShouldDeleteEntry_WhenPresent()
    {
        string root = CreateTempDirectory();
        try
        {
            var cache = new DevDiskAssetCache(root);
            AssetKey key = CreateKey("01");
            cache.Store(key, [7, 8, 9]);

            bool firstRemove = cache.Remove(key);
            bool secondRemove = cache.Remove(key);

            Assert.True(firstRemove);
            Assert.False(secondRemove);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ResolveEntryPath_ShouldContainVersionedFolders()
    {
        string root = CreateTempDirectory();
        try
        {
            var cache = new DevDiskAssetCache(root);
            AssetKey key = new("proc/test", 9, 2, "AA11", "BB22");

            string path = cache.ResolveEntryPath(key);

            Assert.Contains(Path.Combine("g9", "r2"), path, StringComparison.Ordinal);
            Assert.EndsWith("AA11.bin", path, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ResolveEntryPath_ShouldSanitizeInvalidTokenCharacters()
    {
        string root = CreateTempDirectory();
        try
        {
            char invalidChar = Path.GetInvalidFileNameChars()
                .First(static c => c != Path.DirectorySeparatorChar && c != Path.AltDirectorySeparatorChar);
            string token = $"A{invalidChar}B";
            AssetKey key = new(token, 1, 1, token, token);
            var cache = new DevDiskAssetCache(root);

            string path = cache.ResolveEntryPath(key);

            Assert.DoesNotContain(invalidChar.ToString(), path, StringComparison.Ordinal);
            Assert.Contains("A_B", path, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static AssetKey CreateKey(string suffix)
    {
        return new AssetKey("proc/test", 1, 1, $"HASH{suffix}", "CFG");
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"engine-content-cache-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
