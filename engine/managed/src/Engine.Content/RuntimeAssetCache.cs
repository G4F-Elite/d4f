namespace Engine.Content;

public sealed class RuntimeAssetCache<TValue>
    where TValue : notnull
{
    private readonly int _capacity;
    private readonly Dictionary<AssetKey, LinkedListNode<CacheEntry>> _entries;
    private readonly LinkedList<CacheEntry> _lruList;

    private sealed record CacheEntry(AssetKey Key, TValue Value);

    public RuntimeAssetCache(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Cache capacity must be greater than zero.");
        }

        _capacity = capacity;
        _entries = new Dictionary<AssetKey, LinkedListNode<CacheEntry>>();
        _lruList = new LinkedList<CacheEntry>();
    }

    public int Capacity => _capacity;

    public int Count => _entries.Count;

    public bool TryGet(AssetKey key, out TValue value)
    {
        if (!_entries.TryGetValue(key, out LinkedListNode<CacheEntry>? node))
        {
            value = default!;
            return false;
        }

        MoveToFront(node);
        value = node.Value.Value;
        return true;
    }

    public void Set(AssetKey key, TValue value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (_entries.TryGetValue(key, out LinkedListNode<CacheEntry>? existingNode))
        {
            existingNode.Value = existingNode.Value with { Value = value };
            MoveToFront(existingNode);
            return;
        }

        var entry = new CacheEntry(key, value);
        LinkedListNode<CacheEntry> node = _lruList.AddFirst(entry);
        _entries.Add(key, node);

        if (_entries.Count > _capacity)
        {
            EvictLeastRecentlyUsed();
        }
    }

    public bool Remove(AssetKey key)
    {
        if (!_entries.TryGetValue(key, out LinkedListNode<CacheEntry>? node))
        {
            return false;
        }

        _entries.Remove(key);
        _lruList.Remove(node);
        return true;
    }

    public void Clear()
    {
        _entries.Clear();
        _lruList.Clear();
    }

    private void EvictLeastRecentlyUsed()
    {
        LinkedListNode<CacheEntry>? node = _lruList.Last;
        if (node is null)
        {
            return;
        }

        _entries.Remove(node.Value.Key);
        _lruList.RemoveLast();
    }

    private void MoveToFront(LinkedListNode<CacheEntry> node)
    {
        _lruList.Remove(node);
        _lruList.AddFirst(node);
    }
}
