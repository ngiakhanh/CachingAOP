namespace CachingAOP
{
    public class CacheService
    {
        private Dictionary<string, object?> Cache { get; set; } = new Dictionary<string, object?>();

        public bool Contains(string key)
        {
            return Cache.ContainsKey(key);
        }

        public object? Get(string key)
        {
            return Cache[key];
        }

        public void Set(string key, object? value)
        {
            Cache[key] = value;
        }
    }
}
