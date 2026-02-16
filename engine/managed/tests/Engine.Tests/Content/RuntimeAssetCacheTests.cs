using Engine.Content;

namespace Engine.Tests.Content;

public sealed class RuntimeAssetCacheTests
{
    [Fact]
    public void Constructor_ShouldValidateCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RuntimeAssetCache<string>(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RuntimeAssetCache<string>(-1));
    }

    [Fact]
    public void SetAndTryGet_ShouldReturnStoredValue()
    {
        var cache = new RuntimeAssetCache<string>(capacity: 2);
        AssetKey key = CreateKey("01");
        cache.Set(key, "value");

        bool found = cache.TryGet(key, out string value);

        Assert.True(found);
        Assert.Equal("value", value);
    }

    [Fact]
    public void Set_ShouldEvictLeastRecentlyUsedEntry_WhenCapacityExceeded()
    {
        var cache = new RuntimeAssetCache<string>(capacity: 2);
        AssetKey first = CreateKey("01");
        AssetKey second = CreateKey("02");
        AssetKey third = CreateKey("03");

        cache.Set(first, "a");
        cache.Set(second, "b");
        _ = cache.TryGet(first, out _); // Mark first as recently used.
        cache.Set(third, "c");

        Assert.True(cache.TryGet(first, out _));
        Assert.False(cache.TryGet(second, out _));
        Assert.True(cache.TryGet(third, out _));
    }

    [Fact]
    public void Set_ShouldUpdateExistingEntryWithoutChangingCount()
    {
        var cache = new RuntimeAssetCache<string>(capacity: 2);
        AssetKey key = CreateKey("01");

        cache.Set(key, "a");
        cache.Set(key, "b");

        Assert.Equal(1, cache.Count);
        Assert.True(cache.TryGet(key, out string value));
        Assert.Equal("b", value);
    }

    [Fact]
    public void RemoveAndClear_ShouldUpdateCacheState()
    {
        var cache = new RuntimeAssetCache<string>(capacity: 3);
        AssetKey first = CreateKey("01");
        AssetKey second = CreateKey("02");
        cache.Set(first, "a");
        cache.Set(second, "b");

        bool removed = cache.Remove(first);
        cache.Clear();

        Assert.True(removed);
        Assert.False(cache.TryGet(first, out _));
        Assert.False(cache.TryGet(second, out _));
        Assert.Equal(0, cache.Count);
    }

    private static AssetKey CreateKey(string suffix)
    {
        return new AssetKey("proc/test", 1, 1, $"HASH{suffix}", "CFG");
    }
}
