namespace CachingAOP;

public class CacheService
{
    private Dictionary<string, object?> Cache { get; } = new();

    public bool Contains(string key)
    {
        return Cache.ContainsKey(key);
    }

    public object? Get(string key)
    {
        return Cache.TryGetValue(key, out var value) ? value : null;
    }

    public void Set(string key, object? value)
    {
        Cache[key] = value;
    }

    public async Task<(bool, object?)> GetAsync(string key)
    {
        return Cache.TryGetValue(key, out var value) ? (true, value) : (false, null);
    }

    public async Task SetAsync(string key, object? value)
    {
        Cache[key] = value;
    }

    public async Task RemoveAsync()
    {
        Cache.Clear();
    }
}